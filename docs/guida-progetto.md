# Guida al progetto (versione leggibile)

> Questa è la versione **discorsiva** della guida, pensata per essere letta dalle persone.
> Non viene caricata automaticamente come contesto da Claude: il file di contesto è il
> `CLAUDE.md` nella radice del repo, scritto in forma compressa per risparmiare token.
> Tieni i due file allineati quando cambia l'architettura.

Il progetto è un'applicazione web che gestisce la classifica di leghe di Magic (formato Pauper),
in sostituzione del vecchio workbook Excel. Da qualche tempo l'applicazione è **multi-lega
(multi-tenant)**: una singola installazione ospita più leghe indipendenti, ciascuna con i propri
giocatori, tappe e amministratori. La prima lega caricata (dati di seed) è la **Lega Pauper
Massarosa 2026**, raggiungibile con lo slug `massarosa`.

In deploy l'app vive sotto il percorso base `gmarra.it/lega-pauper/`; ogni lega è poi accessibile
aggiungendo il proprio slug, ad esempio `gmarra.it/lega-pauper/massarosa/`.

## Stack tecnologico

- **API**: .NET 10 con ASP.NET Core Minimal API, organizzata in clean architecture su tre layer:
  Domain (logica pura), Infrastructure (persistenza) e Api (web).
- **Database**: PostgreSQL tramite EF Core (provider Npgsql). All'avvio dell'applicazione vengono
  applicate automaticamente le migration e il seed dei dati iniziali.
- **Client**: Angular 19 con componenti standalone, route lazy e interceptor HTTP che aggiungono
  sia il token JWT sia lo slug della lega corrente.
- **Deploy**: container Docker su una VM ARM64. Le immagini sono pubblicate su GHCR; l'nginx
  dell'host termina l'HTTPS e inoltra al container del client. Una GitHub Action builda e
  rilascia a ogni push su `main`. I dettagli operativi sono in `docs/deploy.md`.

## Struttura del repository

```
api/
  ClassificaLega.Domain/
    Entities/                     # League, LeagueLogo, Season, Player, Stage, Result, User (+ UserRoles)
    Services/ScoringService.cs    # bonus e classifica "Best N tappe" — logica pura, testabile
  ClassificaLega.Infrastructure/
    Persistence/AppDbContext.cs   # DbSet e mapping EF
    Persistence/DatabaseSeeder.cs # seed della lega Massarosa + EnsureUsersAsync per gli utenti
    Persistence/Migrations/       # include ..._MultiLeague (aggiunge League/User e backfill)
    PdfImport/EventLinkPdfParser.cs  # parsing dei PDF EventLink con PdfPig
  ClassificaLega.Api/
    Program.cs                    # DI, autenticazione JWT, CORS, middleware slug, endpoint
    Tenancy/LeagueContext.cs      # lega corrente della richiesta (scoped)
    Auth/                         # JwtOptions, AdminOptions, AuthService
    Services/                     # LeagueReadService, LeagueWriteService, LeagueImportService, LeagueAdminService
    Dtos/                         # ReadDtos, WriteDtos, ImportDtos, LeagueDtos
  ClassificaLega.Tests/           # xUnit, focalizzati sul dominio (ScoringService, parser)
client/
  src/app/core/                   # api.service, auth.service, interceptor JWT, league.interceptor,
                                  #   league-context.service, league-reuse.strategy, guard, models,
                                  #   player-picker.component (typeahead server-side riusabile)
  src/app/features/               # leagues (picker), admin (console super-admin), league-shell,
                                  #   classifica, tappe, inserimento, importazione, impostazioni, login
  public/app-logo.svg             # logo dell'app, mostrato come mark fisso in testata
  nginx.conf                      # proxy /api/ → http://api:8080 sulla rete di compose
```

## Modello di dominio

La gerarchia dei dati è: **League → Seasons → (Players, Stages) → Results**. Un `Result`
rappresenta l'incrocio tra una tappa (`Stage`) e un giocatore (`Player`), con i relativi punteggi.

- **League** è il *tenant*. Ha uno `Slug` (usato nell'URL, in minuscolo), un `Name`, un `Title`
  opzionale usato per il branding in testata e un flag `IsActive`. Una lega contiene più stagioni.
- **LeagueLogo** conserva il logo della lega come blob direttamente sul database. È volutamente una
  **tabella separata** (chiave primaria = `LeagueId`, relazione 1-1 con cascade), così i byte non
  vengono mai caricati dalle normali query su `League` — fondamentale perché il middleware di
  tenancy carica la lega ad ogni richiesta. Contiene i byte (`bytea`), il `ContentType`, un `ETag`
  (hash del contenuto, per cache HTTP e cache-busting) e `UpdatedAt`. Il DTO di lettura espone solo
  un flag `HasLogo`, così il client sa se mostrare l'immagine o l'avatar di ripiego senza scaricare
  i byte negli elenchi.
- **Season** appartiene a una lega (`LeagueId`). Per ogni lega può esserci una sola stagione
  attiva alla volta (`IsActive`).
- **Player** appartiene a una stagione (`SeasonId`): i giocatori sono quindi **per stagione**, non
  per lega — stagioni diverse hanno elenchi giocatori distinti. L'abbinamento per nome usa una
  chiave normalizzata (`NormalizedKey`, accent/case-insensitive — `DatabaseSeeder.Normalize`).
- **User** rappresenta un amministratore. Il campo `Role` distingue tra `SuperAdmin` (globale, con
  `LeagueId` nullo, gestisce tutte le leghe) e `LeagueAdmin` (legato a una singola lega tramite
  `LeagueId`). La password è memorizzata come hash BCrypt. I nomi dei ruoli sono nella classe
  `UserRoles`.
- **Tenancy**: un middleware legge l'header `X-League-Slug` della richiesta, risolve la lega attiva
  corrispondente e la espone tramite `LeagueContext.Current` (servizio *scoped*, una istanza per
  richiesta). I servizi di lettura, scrittura e import lavorano sempre a partire da
  `RequireLeagueId()` e dalla stagione attiva di quella lega: l'isolamento tra leghe passa di qui.
- **Scoring** (`ScoringService`): il punteggio è
  `TotalPoints = MatchPoints + BonusRisultato + BonusPartecipazione`. Il bonus partecipazione
  dipende dalla storia ordinata delle tappe del giocatore, perciò ogni modifica a un risultato deve
  richiamare `RecomputePlayerAsync`.
- **Classifica**: criterio "Best N tappe" — si sommano le migliori `CountingStages` tappe (di
  default 8) sul totale `TotalStages` (12).

## API (endpoint)

Tutti gli endpoint sono sotto `/api`. La **lettura è pubblica**, la **scrittura è protetta** da
JWT. La lega di riferimento viene risolta dall'header `X-League-Slug`.

- **Pubblici**: `GET /leagues` (elenco delle leghe attive, per il picker),
  `GET /leagues/{slug}/logo` (logo della lega, con header `ETag` e `Cache-Control` e risposta 304
  condizionale; 404 se la lega non ha logo), `/season`, `/standings`, `/stages`,
  `/stages/{n}/results`, `/players?search=&skip=&take=` (giocatori della stagione corrente,
  filtrati per nome e paginati — alimenta il player picker; `take` di default 20, massimo 100),
  `/players/{id}/progression`, `/matrix`.
- **Admin di lega** (richiedono autenticazione, più un filtro: il super-admin passa sempre,
  altrimenti il claim `leagueId` del token deve coincidere con la lega del contesto, altrimenti
  risposta 403): `PUT /season`, `POST /stages`, `POST|PUT|DELETE /results`, `POST /import/pdf`
  (anteprima dell'import), `POST /import/commit`, `POST|DELETE /logo` (carica o rimuove il logo
  della lega corrente; valida formato PNG/JPEG/WebP/SVG e dimensione max 1 MB come backstop.
  Il client ridimensiona e ricomprime l'immagine in WebP prima dell'upload — `core/image-compress.ts`,
  lato massimo 512px — così l'admin può caricare anche foto pesanti dal telefono senza preparare il file).
- **Super-admin** (gruppo `/leagues`, filtro che richiede `Role == SuperAdmin`):
  `GET /leagues/all`, `POST /leagues` (crea una lega, generando anche la sua prima stagione attiva),
  `PUT /leagues/{id}`, `GET /leagues/{id}/admins`, `POST /leagues/{id}/admins`,
  `POST|DELETE /leagues/{id}/logo` (gestisce il logo di una lega qualsiasi).
- **Autenticazione**: `POST /auth/login` restituisce un JWT con i claim `role` e `leagueId`. In
  `AuthService.LoginAsync` viene scelto l'admin della lega indicata dal contesto e, in mancanza, un
  super-admin globale; la verifica della password con BCrypt è **attiva** (non più commentata come
  in passato). Il super-admin di seed è configurato da variabili d'ambiente (`Admin__Username`,
  `Admin__PasswordHash` in formato BCrypt). All'avvio `DatabaseSeeder.EnsureUsersAsync` garantisce
  l'esistenza del super-admin e dell'admin della lega di seed anche su database già popolati.

## Comandi

```bash
# DB locale
docker compose up -d                       # postgres su :5432 (db/user/pass: classifica_lega)

# API
cd api && dotnet run --project ClassificaLega.Api    # :5188 (vedi proxy.conf.json)
dotnet test                                # unit test del dominio

# Client
cd client && npm start                     # ng serve, proxy /api → :5188
npm run build                              # output in dist/classifica-lega/browser
npm test                                   # karma/jasmine
```

## Convenzioni

- **Documentazione allineata**: `CLAUDE.md` (compresso, usato come contesto) e questo file
  `docs/guida-progetto.md` (discorsivo, per lettori umani) descrivono lo stesso contenuto. Ogni
  modifica che tocca stack, struttura, modello di dominio, API, comandi o convenzioni va riportata
  su **entrambi** i file. A fine modifica, quando è rilevante, va chiesto all'utente se aggiornare
  le due guide.
- **Routing del client**: la route `''` mostra il picker delle leghe; `gestione` apre la console
  super-admin (è uno slug riservato, elencato in `RESERVED_SLUGS`); `:slug` carica la `league-shell`
  con le sue pagine figlie (classifica, tappe, inserimento, importazione, impostazioni, login).
  Il `league.interceptor` ricava lo slug dal primo segmento dell'URL corrente e lo inserisce
  nell'header `X-League-Slug` di ogni richiesta. L'`auth.service` salva il token separatamente per
  ciascuno scope (lo slug della lega, oppure `__super` per la console super-admin). Quando il login
  è di un **super-admin**, il token viene replicato anche sotto lo scope `__super` (globale): così
  `isSuperAdmin()` lo valuta solo da lì — indipendente dalla lega corrente — e sia `token()` sia
  `isAdminForScope()` ripiegano sul token `__super` se manca quello della lega. In pratica un
  super-admin, anche se ha fatto login dalla pagina di una lega, è riconosciuto come tale ovunque e
  può amministrare qualsiasi lega senza dover rifare l'accesso.
- **Testata della `league-shell`**: a sinistra c'è un blocco-brand con il logo dell'app
  (`app-logo.svg`), poi il logo della lega — l'immagine vera se la lega ne ha uno, altrimenti un
  avatar con le iniziali e un colore derivato dallo slug — e il nome della lega. La navigazione
  collassa in un menu **hamburger a comparsa** (drawer) sotto i 768px, per restare leggibile su
  mobile. Per chi ha effettuato l'accesso come super-admin compare in menu la voce **"Gestione
  leghe"** che porta alla console `/gestione`. Il logo della lega si carica o si rimuove dalla
  pagina Impostazioni; dopo un cambio, `LeagueContextService.logoVersion` forza il refresh
  dell'immagine in testata (cache-busting).
- **Player picker** (`core/player-picker.component`): è il componente unico per **selezionare o
  aggiungere** un giocatore, riusato ovunque serva. È un typeahead che interroga il server
  (`GET /players`) con ricerca e paginazione ("carica altri"): non scarica l'intero elenco, quindi
  scala bene con l'arrivo di nuovi giocatori. Emette una `PlayerSelection`, che può essere un
  giocatore esistente (`{id, name}`), un nuovo nome da creare (`{name}`) o `null`. Il menu a tendina
  usa `position: fixed` calcolato dal campo, così non viene tagliato quando il picker vive dentro un
  contenitore scrollabile (es. la tabella dell'import). È usato in due punti: in **inserimento**
  manuale sostituisce il vecchio toggle Esistente/Nuovo con un solo campo; in **importazione**, per
  ogni riga della revisione, permette di abbinare un giocatore esistente oppure crearne uno nuovo —
  con il nome del PDF *oppure con un nome modificato a mano* — e svuotando il campo si ignora la riga.
- Gli **identificatori** sono in inglese; i **termini di dominio** restano in italiano dove non
  esiste un equivalente pulito (Tappa/Stage, BonusRisultato, BonusPartecipazione). L'interfaccia è
  in italiano.
- La **logica di scoring** vive esclusivamente nel `ScoringService` del dominio: non va duplicata
  nel client (`client/src/app/core/scoring.ts` serve solo alla presentazione).
- L'API base del client è relativa (`'api'`), così funziona anche servita da una sottocartella.
- Nessun segreto nel repo: la chiave JWT e l'hash dell'admin vanno passati via variabili d'ambiente
  (vedi `.env.prod.example`).
