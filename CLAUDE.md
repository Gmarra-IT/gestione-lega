# CLAUDE.md

Tool web gestione classifica leghe Pauper (Magic), sostituto Excel. **Multi-lega (multi-tenant)**.
Lega seed: **Massarosa 2026** (slug `massarosa`). Base path deploy: `gmarra.it/lega-pauper/`,
lega sotto `/:slug` (es. `/lega-pauper/massarosa/`).

## Stack

- **API**: .NET 10, ASP.NET Core Minimal API. Clean arch (Domain / Infrastructure / Api).
- **DB**: PostgreSQL via EF Core (Npgsql). Migration + seed auto all'avvio.
- **Client**: Angular 19 (standalone, lazy routes, interceptor JWT + slug).
- **Deploy**: Docker VM ARM64; immagini GHCR; nginx host termina HTTPS → container client;
  GitHub Actions builda+deploya su push `main`. Dettagli `docs/deploy.md`.

## Struttura

```
api/
  ClassificaLega.Domain/
    Entities/                     # League, LeagueLogo, Season, Player, Stage, Result, User+UserRoles
    Services/ScoringService.cs    # bonus + classifica "Best N tappe" (logica pura)
  ClassificaLega.Infrastructure/
    Persistence/AppDbContext.cs   # DbSet + mapping EF
    Persistence/DatabaseSeeder.cs # seed Massarosa (SeedLeagueSlug) + EnsureUsersAsync
    Persistence/Migrations/       # ..._MultiLeague aggiunge League/User + backfill
    PdfImport/EventLinkPdfParser.cs  # parsing PDF EventLink (PdfPig)
  ClassificaLega.Api/
    Program.cs                    # DI, JWT, CORS, middleware slug, endpoint Minimal API
    Tenancy/LeagueContext.cs      # lega corrente (scoped), RequireLeagueId()
    Auth/                         # JwtOptions, AdminOptions, AuthService
    Services/                     # LeagueRead/Write/Import + LeagueAdminService
    Dtos/                         # ReadDtos, WriteDtos, ImportDtos, LeagueDtos
  ClassificaLega.Tests/           # xUnit, focus dominio (ScoringService, parser)
client/
  src/app/core/                   # api, auth, interceptor, league.interceptor,
                                  #   league-context.service, league-reuse.strategy, guard, models,
                                  #   player-picker.component (typeahead server-side riusabile)
  src/app/features/               # leagues(picker), admin(super-admin), league-shell,
                                  #   classifica, tappe, inserimento, importazione, impostazioni, login
  public/app-logo.svg             # logo app (mark fisso in testata)
  nginx.conf                      # proxy /api/ → http://api:8080 (rete compose)
```

## Modello dominio

Gerarchia: **League → Seasons → (Players, Stages) → Results**. `Result` = (Stage × Player) punteggi;
campi input `Wins?/Draws?/Losses?/Position?` + `MatchPoints`, componenti calcolati
`ScoreBonus/PositionBonus/ParticipationPoints/TotalPoints`.

- **League** = tenant. `Slug` (url, lowercase), `Name`, `Title?` (branding), `IsActive`.
- **LeagueLogo** = logo lega, blob su DB. **Tabella separata** (PK=`LeagueId`, 1-1, cascade) così i
  byte non si caricano mai nelle query su League (il middleware tenant carica `Leagues` ad ogni
  richiesta). Campi: `Bytes` (bytea), `ContentType`, `ETag` (hash), `UpdatedAt`. `LeagueDto.HasLogo`
  espone la presenza senza scaricare i byte.
- **Season** ha `LeagueId`. Una sola attiva per lega (`IsActive`).
- **Player** ha `SeasonId` → i giocatori sono **per stagione** (non per lega). Match per
  `NormalizedKey` (accent/case-insensitive, `DatabaseSeeder.Normalize`).
- **User**: `Role` = `SuperAdmin` (globale, `LeagueId=null`) o `LeagueAdmin` (legato a `LeagueId`).
  `PasswordHash` BCrypt. Vedi `UserRoles`.
- **Tenancy**: middleware legge header `X-League-Slug` → risolve lega attiva → `LeagueContext.Current`
  (scoped). Read/Write/Import services pivotano su `RequireLeagueId()` + Season attiva di quella lega.
- **Scoring** (`ScoringService`, puro, parametrizzato su `ScoringRule`):
  `tournamentTotal = matchPoints + scoreBonus + positionBonus + participationPoints`.
  - `matchPoints = Wins*PointsPerWin + Draws*PointsPerDraw + Losses*PointsPerLoss` (W/D/L opzionali
    sul `Result`; se assenti si usa il `MatchPoints` diretto, es. import). `Position` opzionale (da PDF).
  - `scoreBonus`: voce `ScoreBonuses` con `FromMatchPoints` più alta ≤ matchPoints (a **soglia**).
  - `positionBonus`: `PositionBonuses[Position]`. `participationPoints`: voce `ParticipationTiers`
    con `FromTournament` più alta ≤ indice progressivo 1-based (storia ordinata data→numero→id).
  - Ogni modifica risultato richiama `RecomputePlayerAsync` (ricalcola i componenti su tutti i
    `Result` del giocatore). I componenti sono persistiti sul `Result`
    (`ScoreBonus`/`PositionBonus`/`ParticipationPoints`/`TotalPoints`).
- **`ScoringRule`** = config 1:1 con la stagione (`Season.ScoringRule`, **jsonb** via value converter):
  `PointsPerWin/Draw/Loss`, `PositionBonuses[]`, `ScoreBonuses[]`, `ParticipationTiers[]` (+`Validate()`).
  `CountBestN` = `Season.CountingStages` (niente campo duplicato). Default = cablato storico Massarosa
  (`ScoringRule.Default()`): scoreBonus 6→1,7→2,8→3,9→4,10→6,12→8; presenza 1ª–5ª +1, dalla 6ª +2.
- **Classifica**: "Best N tappe" — somma migliori `CountingStages` (default 8) su `TotalStages` (12).
  Ordinamento (invariato): `TotalPoints` (best N) desc → `AbsoluteTotal` desc → nome asc; pari merito
  stesso `Rank`. Le righe espongono breakdown (`BestResults[]`, `TournamentsPlayed/CountedForTotal`).

## API (endpoint)

Base `/api`. Lettura **pubblica**, scrittura **protetta** (JWT). Lega risolta da header `X-League-Slug`.

- Public: `GET /leagues` (leghe attive), `GET /leagues/{slug}/logo` (logo lega, ETag+Cache-Control,
  404 se assente), `/season`, `/standings`, `/stages`, `/stages/{n}/results`,
  `/players?search=&skip=&take=` (giocatori della season, filtrati+paginati per il picker;
  default take=20, max 100), `/players/{id}/progression`, `/matrix`.
- Admin lega (`RequireAuthorization` + filtro: super-admin passa sempre, altrimenti claim `leagueId`
  del token deve == lega del contesto, sennò 403): `PUT /season`,
  `PUT /season/scoring-rule` (sostituisce la `ScoringRule` e ricalcola tutti i risultati), `POST /stages`,
  `POST|PUT|DELETE /results` (accettano `Wins/Draws/Losses/Position` opzionali oltre a `MatchPoints`),
  `POST /import/pdf` (preview), `POST /import/commit` (propaga `Position` dal PDF),
  `POST|DELETE /logo` (logo lega corrente; valida PNG/JPEG/WebP/SVG, max 1 MB
  backstop — il client comprime/ridimensiona prima dell'upload, vedi `core/image-compress.ts`).
- Super-admin (`/leagues`, filtro `Role==SuperAdmin`): `GET /leagues/all`, `POST /leagues`,
  `PUT /leagues/{id}`, `GET /leagues/{id}/admins`, `POST /leagues/{id}/admins`,
  `POST|DELETE /leagues/{id}/logo` (logo di una lega qualsiasi).
- Auth: `POST /auth/login` → JWT (claim `role` + `leagueId`). `AuthService.LoginAsync` sceglie
  admin della lega del contesto, altrimenti super-admin globale; verifica BCrypt **attiva**.
  Super-admin seed da env (`Admin__Username`, `Admin__PasswordHash` BCrypt).

## Comandi

```bash
# DB locale
docker compose up -d                       # postgres su :5432 (db/user/pass: classifica_lega)

# API
cd api && dotnet run --project ClassificaLega.Api    # :5188 (vedi proxy.conf.json)
dotnet test                                # unit test dominio

# Client
cd client && npm start                     # ng serve, proxy /api → :5188
npm run build                              # output in dist/classifica-lega/browser
npm test                                   # karma/jasmine
```

## Convenzioni

- **Doc allineate**: `CLAUDE.md` (compresso, contesto) e `docs/guida-progetto.md` (discorsivo,
  lettori umani) coprono stesso contenuto. Se una modifica tocca stack/struttura/dominio/API/
  comandi/convenzioni, aggiornarli **entrambi**. A fine modifica, se rilevante, **chiedere**
  all'utente se aggiornare le due guide.
- **Routing client**: `''` = picker leghe; `gestione` = console super-admin (slug riservato,
  `RESERVED_SLUGS`); `:slug` = `league-shell` (children classifica/tappe/inserimento/importazione/
  impostazioni/login). `league.interceptor` deriva slug da 1° segmento URL → header `X-League-Slug`.
  `auth.service` salva token per scope (slug o `__super`). Login **super-admin** replica il token
  anche sotto `__super` (globale): `isSuperAdmin()` legge solo `__super`, e `token()`/`isAdminForScope()`
  fanno fallback su `__super` → super-admin vale su qualsiasi lega senza re-login.
- **Testata `league-shell`**: brand-block = logo app (`app-logo.svg`) + logo lega (`<img>` se
  `current().hasLogo`, sennò avatar iniziali con colore derivato dallo slug) + nome. Nav con
  **hamburger drawer** sotto 768px. Link **"Gestione leghe"** → `/gestione` visibile solo se
  `auth.isSuperAdmin()`. Upload/rimozione logo lega in `impostazioni`; cache-bust via
  `LeagueContextService.logoVersion`.
- **Player picker** (`core/player-picker.component`): unico punto per **selezionare/aggiungere** un
  giocatore. Typeahead server-side (`GET /players`, debounce + "carica altri") → scala con i nuovi
  player senza scaricare l'intera lista. Emette `PlayerSelection` = esistente `{id,name}` | nuovo
  `{name}` | `null`. Menu `position: fixed` (no clipping in contenitori scrollabili). Usato in
  `inserimento` (un solo campo, no toggle Esistente/Nuovo) e in `importazione` (un picker per riga;
  nuovo player con **nome editabile** anche diverso da quello del PDF; campo svuotato = riga ignorata).
- Identificatori **inglese**; termini dominio **italiano** dove non c'è equivalente pulito
  (Tappa/Stage, ScoreBonus, PositionBonus, ParticipationPoints). UI italiano.
- Scoring **solo** in `ScoringService` dominio — non duplicare nel client
  (`client/src/app/core/scoring.ts` solo presentazione: funzioni parametrizzate sulla `ScoringRule`
  della stagione per l'anteprima live in `inserimento`).
- API base client relativa (`'api'`) → funziona sotto sottocartella.
- Niente segreti nel repo: JWT key + hash admin via env (`.env.prod.example`).
