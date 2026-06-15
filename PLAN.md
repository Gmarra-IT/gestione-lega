# Gestione Classifica Lega — Piano di implementazione

Tool web per gestire la classifica di una lega Pauper, in sostituzione del workbook Excel
`Lega_pauper_-_Gestione_classifica.xlsm`. Prima installazione: **Lega Pauper Massarosa**
(`lega-pauper-massarosa.gmarra.it`).

> Questo file è la **specifica operativa** da dare a Claude Code. Ogni fase è autonoma, con
> deliverable e criteri di accettazione (AC). L'Appendice A contiene la logica di dominio
> estratta dal VBA (da non perdere).

---

## 1. Obiettivi

1. **Classifica online** (foglio `Classifica`): posizione, giocatore, Best N Tappe.
2. **Andamento tappe online** (foglio `Tappe`): matrice giocatori × tappe + grafico andamento.
3. **Inserimento risultati online** (foglio `Form` + macro): aggiunta giocatore o
   valorizzazione punteggio per esistente, con calcolo automatico dei bonus.
4. **Import da PDF EventLink** ("Classifica per posizione"): alternativa all'inserimento manuale.

Lettura **pubblica** (1, 2). Scrittura (3, 4) **protetta** da login admin.

**Configurabilità lega in corso:**
- numero **totale tappe previste** (`TotalStages`, es. 12);
- numero di tappe **valide al conteggio classifica** (`CountingStages`, "Best N"),
  default **8**, configurabile da **1 a `TotalStages`**.

---

## 2. Stack — full-stack

**PostgreSQL + .NET 10 API + Angular 20**, come gli altri applicativi sulla VM.

Perché full-stack e non `db + client`:
- La **logica bonus/classifica** non è banale e va testata in un punto solo; nel client si
  duplicherebbe (contro "no ripetizioni").
- L'**import PDF** richiede parsing server-side affidabile (PdfPig); in browser è fragile.
- Serve comunque separare **read pubblico** da **write autenticato**.
- È il pattern già in produzione (`manarank`, `gymshark`, `spese-di-casa`): Docker + nginx +
  Postgres + GitHub Actions ARM64. Zero attrito di deploy.

Repo: **`gestione-classifica-lega`**. Namespace root .NET: **`ClassificaLega`**.
Identificatori in **inglese**, termini di dominio italiani dove non hanno equivalente pulito
(glossario in Appendice B).

---

## 3. Architettura

Monorepo:

```
gestione-classifica-lega/
├─ api/
│  ├─ ClassificaLega.Api/             # ASP.NET Core 10 (Minimal API o Controllers)
│  ├─ ClassificaLega.Domain/          # entità + ScoringService (puro, testabile)
│  ├─ ClassificaLega.Infrastructure/  # EF Core, Npgsql, import PdfPig
│  └─ ClassificaLega.Tests/           # unit test sul dominio (cuore del progetto)
├─ client/                            # Angular 20 (standalone, signals)
├─ docker-compose.yml                 # dev locale (postgres)
├─ docker-compose.prod.yml            # deploy VM
├─ .github/workflows/                 # CI/CD ARM64 → GHCR → SSH deploy
└─ PLAN.md
```

Layering: `Api` → `Domain` ← `Infrastructure`. Il `ScoringService` vive in `Domain`, senza
dipendenze da EF/HTTP: codice puro, interamente unit-testabile.

---

## 4. Modello dati

```
Season (Stagione/edizione della lega)
  Id             int (PK)
  Name           string          -- "Lega Pauper Massarosa 2026"
  TotalStages    int             -- tappe previste (default 12)
  CountingStages int             -- Best N per classifica (default 8, 1..TotalStages)
  IsActive       bool            -- una sola attiva
  CreatedAt      timestamptz

Player
  Id             int (PK)
  DisplayName    string          -- "Michele Pardini"
  NormalizedKey  string          -- lower + trim + collapse spaces (+ no accenti) per matching
  CreatedAt      timestamptz
  UNIQUE (SeasonId, NormalizedKey)   -- se i giocatori sono per-stagione; altrimenti globale (vedi nota)

Stage (Tappa)
  Id             int (PK)
  SeasonId       int (FK)
  Number         int             -- 1..Season.TotalStages
  Name           string?         -- "1° Tappa Della Lega Pauper Nakama Store"
  Date           date?
  EventLinkId    string?         -- "11180194" dal PDF
  CreatedAt      timestamptz
  UNIQUE (SeasonId, Number)

Result (Risultato)
  Id                  int (PK)
  StageId             int (FK)
  PlayerId            int (FK)
  MatchPoints         int        -- 0..12, "Punti" del PDF
  BonusRisultato      int        -- derivato da MatchPoints (Appendice A.1)
  BonusPartecipazione int        -- 1 o 2 (Appendice A.2)
  TotalPoints         int        -- MatchPoints + BonusRisultato + BonusPartecipazione (persistito)
  -- opzionali, solo riferimento (non incidono sulla classifica):
  WinPctOpp / GameWinPct / GameWinPctOpp  int?   -- %VIA / %VP / %VPA dal PDF
  CreatedAt / UpdatedAt timestamptz
  UNIQUE (StageId, PlayerId)
```

`TotalPoints` persistito per query/ordinamento, ma **sempre ricalcolato** dal
`ScoringService` a ogni scrittura (single source of truth). Classifica, Best N e Posizione
sono **derivati a runtime** (mai persistiti).

> Nota: `Player` legato o no alla `Season`? Default proposto: **giocatori per-stagione**
> (`Player.SeasonId`), così ogni edizione riparte pulita. Se preferisci un'anagrafica unica
> riusabile tra stagioni, sposta l'unicità su `NormalizedKey` globale e collega il giocatore
> alla stagione tramite i suoi `Result`. Da confermare.

---

## 5. Logica di dominio — `ScoringService`

Funzioni pure (dettaglio numerico in Appendice A):

- `int BonusRisultato(int matchPoints)` — lookup table.
- `int BonusPartecipazione(int previousParticipations)` — `<5 → 1`, `≥5 → 2`.
- `int TotalPoints(matchPoints, bonusRisultato, bonusPartecipazione)`.
- `int BestN(IEnumerable<int> totaliTappa, int n)` — somma dei migliori `n` totali-tappa
  (`n = Season.CountingStages`; se il giocatore ha < n tappe, somma di tutte).
- Calcolo classifica: ordina per `BestN` desc, applica tiebreak (sotto), assegna Posizione.

**Tiebreak classifica:** `BestN` desc → `TotalPoints` assoluto (somma di tutte le tappe) desc
→ `DisplayName` asc. *Niente "Diff Top 8":* essendo `BestN − costante`, è monotòno con `BestN`
e a parità non discrimina (rimosso). Se in futuro serve come info "scarto dalla qualifica",
si calcola a vista senza usarlo per l'ordinamento.

**Ricalcolo bonus partecipazione (deterministico):** a differenza del VBA che lo congela
all'inserimento, ordiniamo i risultati del giocatore per `Stage.Number`: la k-esima
partecipazione ha `previousParticipations = k-1` → prime 5 bonus 1, dalla 6ª bonus 2. Ogni
inserimento/modifica ricalcola i bonus partecipazione di **tutti** i risultati del giocatore.
Elimina la dipendenza dall'ordine di inserimento e rende il sistema idempotente.

---

## 6. API (bozza endpoint)

Pubblici (GET):
- `GET /api/season` → stagione attiva con `TotalStages`, `CountingStages`.
- `GET /api/standings` → classifica (Posizione, DisplayName, BestN).
- `GET /api/stages` → elenco tappe.
- `GET /api/stages/{number}/results` → risultati di una tappa.
- `GET /api/players/{id}/progression` → andamento tappa per tappa (grafico).
- `GET /api/matrix` → matrice giocatori × tappe (vista "Tappe"), colonne `1..TotalStages`.

Protetti (admin, JWT):
- `PUT /api/season` → aggiorna `TotalStages` / `CountingStages` (valida `1 ≤ CountingStages ≤ TotalStages`).
- `POST /api/results` → inserisce/aggiorna un risultato. Body:
  `{ stageNumber, playerId?, newPlayerName?, matchPoints }`. Crea giocatore se `newPlayerName`;
  rifiuta se il nome esiste già (come la macro). Ricalcola bonus.
- `PUT /api/results/{id}` / `DELETE /api/results/{id}`.
- `POST /api/stages` → crea/rinomina tappa (entro `TotalStages`).
- `POST /api/import/pdf` → upload PDF → **preview** (staging), non committa.
- `POST /api/import/commit` → conferma la preview rivista.
- `POST /api/auth/login` → JWT.

Validazione: `matchPoints ∈ [0,12]`; nome nuovo non duplicato; `Number ≤ TotalStages`;
`CountingStages` nel range.

---

## 7. Import PDF EventLink

Formato (vedi `Turno_4_Classifica_per_posizione.pdf`): colonne fisse
`Pos | Nome | Punti | %VIA | %VP | %VPA` + metadati (Evento, event id, Data evento). Per la
lega servono **Nome + Punti**; le percentuali sono tiebreak interni a EventLink,
opzionalmente memorizzabili come riferimento.

Pipeline (server-side, `Infrastructure`):
1. **PdfPig** (managed-only, gira su ARM64 / .NET 10 senza dipendenze native — preferibile a
   IronPDF, che è per HTML→PDF e qui sarebbe sovradimensionato) estrae il testo.
2. Parsing righe dati con regex:
   `^\s*(\d+)\s+(.+?)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*$` → (pos, nome, punti, via, vp, vpa);
   metadati da `Evento:` / `Data evento:`.
3. **Name matching** contro `NormalizedKey` (case/accent-insensitive: nel PDF la casing è
   incoerente, es. `michele pardini`): ogni riga `Matched(existing)` o `Unmatched(new)`.
4. **Preview/staging**: l'utente rivede in UI (conferma match, rimappa, sceglie i nuovi da
   creare), poi `commit`. Mai scrittura diretta senza conferma. Se la tappa ha già risultati,
   il commit chiede sovrascrittura (come la macro).

---

## 8. Decisioni

| # | Tema | Scelta |
|---|------|--------|
| 1 | Bonus partecipazione | **Ricalcolo deterministico** per `Stage.Number` (§5) |
| 2 | Tiebreak classifica | `BestN` desc → `TotalPoints` desc → nome. **Diff Top 8 rimosso** |
| 3 | Auth | JWT singolo utente admin (hash password da env) |
| 4 | Postgres | DB dedicato nell'istanza Postgres esistente |
| 5 | Hosting | Subdomain `lega-pauper-massarosa.gmarra.it` (prima installazione) |
| 6 | Repo | Monorepo (1 repo, 2 immagini) |
| 7 | Naming entità | Inglese + glossario IT |
| 8 | Anagrafica giocatori | *Da confermare:* per-stagione (default) vs globale (§4 nota) |

---

## 9. Fasi (per sessioni Claude Code)

### Fase 0 — Scaffolding & infra locale
- Monorepo, solution .NET 10 (4 progetti), workspace Angular 20 standalone.
- `docker-compose.yml` con Postgres; `.editorconfig`; README; connection string via config.
- EF Core + Npgsql configurati, prima migration applicabile.
- **AC:** `docker compose up` avvia Postgres; `dotnet run` espone `/health`; `ng serve` carica.

### Fase 1 — Dominio & DB (cuore)
- Entità `Season`, `Player`, `Stage`, `Result` + migration. `NormalizedKey` computata.
  Seed `Season` attiva: `TotalStages=12`, `CountingStages=8`.
- `ScoringService` completo (Appendice A) in `Domain`, **senza dipendenze**, con `BestN`
  parametrico.
- `ClassificaLega.Tests`: unit test su ogni regola (lookup bonus; soglia partecipazione;
  `BestN` con `n` variabile e con < n / > n tappe; ranking con pareggi e tiebreak su
  `TotalPoints`). Casi dai dati reali del workbook.
- Seeder opzionale: importa lo stato attuale del foglio `Tappe` (22 giocatori, 7 tappe).
- **AC:** test verdi; con `CountingStages=8` il seed riproduce la classifica del foglio
  `Classifica` (Marraccini 1° con `BestN=89`, ecc.); cambiando `CountingStages` l'ordine si
  ricalcola coerentemente.

### Fase 2 — API read pubblica
- Endpoint §6 GET: season, standings, stages, results, matrix, progression. DTO dedicati.
- `BestN`/Posizione calcolati in `Domain` usando `Season.CountingStages`.
- **AC:** `GET /api/standings` ritorna la classifica corretta sui dati di seed; `GET /api/matrix`
  rende `TotalStages` colonne.

### Fase 3 — API write + auth
- `POST/PUT/DELETE /api/results`, `POST /api/stages`, `PUT /api/season`. Crea giocatore inline
  con guardia "nome già esistente". Ricalcolo bonus del giocatore a ogni scrittura.
- JWT admin. Write protetto, read libero.
- **AC:** inserendo i 12 risultati del PDF Tappa 1 via API, i totali tappa combaciano col
  modello Excel (es. michele pardini: 12 + bonus 8 + partecipazione); modifica di
  `CountingStages` via `PUT /api/season` aggiorna la classifica.

### Fase 4 — Client Angular
- Standalone components, signals, lazy routes. Read pubblico, write dietro login.
- Viste: **Classifica** · **Tappe** (matrice a `TotalStages` colonne + grafico andamento, riuso
  pattern Chart.js già usato altrove) · **Form inserimento** (giocatore esistente/nuovo, punti,
  anteprima bonus live come il foglio Form) · **Impostazioni** (`TotalStages`/`CountingStages`).
- HTTP interceptor JWT; guard sulle route admin.
- **AC:** flusso manuale end-to-end dal browser; classifica e matrice si adattano alla config.

### Fase 5 — Import PDF
- `POST /api/import/pdf` (PdfPig + parser) → preview con match nomi; UI di review; `commit`.
- Gestione sovrascrittura tappa esistente.
- **AC:** caricando `Turno_4_Classifica_per_posizione.pdf` si ottengono 12 righe corrette, i
  nomi noti matchano gli esistenti, i nuovi sono evidenziati; commit popola la tappa.

### Fase 6 — Deploy VM
- `Dockerfile` API multi-stage ARM64; build Angular statico servito da nginx.
- `docker-compose.prod.yml`; blocco nginx (`/` → client, `/api` → api); DB dedicato
  nell'istanza Postgres esistente.
- GitHub Actions: build immagini ARM64 → push GHCR → deploy SSH (pattern di `manarank`/`gymshark`;
  secret SSH come da rotazione chiavi già in uso).
- Subdomain `lega-pauper-massarosa.gmarra.it`, HTTPS via reverse proxy esistente.
- **AC:** push su `main` deploya; sito raggiungibile e funzionante in prod.

---

## Appendice A — Logica di dominio (dal VBA `Modulo1`)

### A.1 Bonus risultato (lookup su match points)
```
matchPoints → bonus     record (W-L-D, punti = 3·W + 1·D)
   12       →  8         4-0-0
   10       →  6         3-0-1
    9       →  4         3-1-0
    8       →  3         2-0-2
    7       →  2         2-1-1
    6       →  1         2-2-0   (ambiguo con 1-0-3→bonus 0; si assume 2-2-0)
  else      →  0
```

### A.2 Bonus partecipazione
`previousParticipations < 5 → 1`, altrimenti `2`.
(VBA: conta le altre tappe con punti > 0, esclusa la corrente; soglia `< 5`.)

### A.3 Totale tappa
`TotalPoints = MatchPoints + BonusRisultato + BonusPartecipazione`.

### A.4 Best N Tappe
Somma degli `n` totali-tappa più alti del giocatore (`n = Season.CountingStages`, default 8;
se ha < n tappe, somma di tutte). È **la metrica della classifica**, non il totale assoluto.
Nel VBA era fisso a 8 (`SUMPRODUCT(LARGE(..,{1..8}))`); ora `n` è configurabile.

### A.5 Posizione
Rank per `BestN` desc con tiebreak `TotalPoints` desc → nome (Decisione #2).
*(Diff Top 8 del foglio Excel rimosso: ridondante col Best N.)*

---

## Appendice B — Glossario dominio

| IT (dominio) | EN (codice) | Note |
|---|---|---|
| Stagione/edizione | Season | contenitore config: TotalStages, CountingStages |
| Giocatore | Player | |
| Tappa | Stage | evento singolo (1..TotalStages) |
| Risultato | Result | una riga giocatore×tappa |
| Punti effettivi | MatchPoints | 0–12, "Punti" del PDF |
| Bonus risultato | BonusRisultato | da match points (A.1) |
| Bonus partecipazione | BonusPartecipazione | fedeltà, 1 o 2 (A.2) |
| Totale tappa | TotalPoints | A.3 |
| Tappe valide al conteggio | CountingStages | "Best N", default 8 |
| Best N Tappe | BestN | A.4, metrica classifica |
| Classifica | Standings | derivata, mai persistita |
