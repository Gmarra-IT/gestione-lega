# CLAUDE.md

Guida per lavorare in questo repo. Tool web per gestire la classifica di una lega Pauper
(Magic), sostituto del workbook Excel. Prima installazione: **Lega Pauper Massarosa 2026**
(`lega-pauper-massarosa.gmarra.it`).

## Stack

- **API**: .NET 10, ASP.NET Core Minimal API. Clean architecture (Domain / Infrastructure / Api).
- **DB**: PostgreSQL via EF Core (Npgsql). Migration + seed automatici all'avvio.
- **Client**: Angular 19 (standalone components, lazy routes, interceptor JWT).
- **Deploy**: Docker su VM ARM64; immagini su GHCR; nginx host termina HTTPS → container client;
  GitHub Actions builda e deploya su push a `main`. Dettagli in `docs/deploy.md`.

## Struttura

​```
api/
  ClassificaLega.Domain/          # entità + ScoringService (logica pura, testabile)
    Entities/                     # Season, Player, Stage, Result
    Services/ScoringService.cs    # bonus + classifica "Best N tappe"
  ClassificaLega.Infrastructure/
    Persistence/AppDbContext.cs   # DbSet + mapping EF
    Persistence/DatabaseSeeder.cs # seed iniziale (dati Massarosa 2026)
    Persistence/Migrations/
    PdfImport/EventLinkPdfParser.cs  # parsing PDF EventLink (PdfPig)
  ClassificaLega.Api/
    Program.cs                    # DI, auth JWT, CORS, endpoint Minimal API
    Auth/                         # JwtOptions, AdminOptions, AuthService
    Services/                     # LeagueReadService, LeagueWriteService, LeagueImportService
    Dtos/                         # ReadDtos, WriteDtos, ImportDtos
  ClassificaLega.Tests/           # xUnit, focus sul dominio (ScoringService, parser)
client/
  src/app/core/                   # api.service, auth.service, interceptor, guard, models, scoring
  src/app/features/               # classifica, tappe, inserimento, importazione, impostazioni, login
  nginx.conf                      # proxy /api/ → http://api:8080 (rete compose)
​```

## Modello dominio

Gerarchia: **Season → (Players, Stages) → Results**. `Result` = (Stage × Player) con punteggi.

- Tutto è **single-tenant**: la lettura/scrittura pivota sull'**unica Season con `IsActive = true`**
  (`LeagueReadService.ActiveSeasonAsync`, `LeagueWriteService.ActiveSeasonAsync`). Non esiste
  ancora un concetto di "lega"/tenant sopra Season.
- **Scoring** (`ScoringService`): `TotalPoints = MatchPoints + BonusRisultato + BonusPartecipazione`.
  Il bonus partecipazione dipende dalla storia ordinata delle tappe del giocatore, quindi ogni
  modifica a un risultato richiama `RecomputePlayerAsync`.
- **Classifica**: "Best N tappe" — somma le migliori `CountingStages` tappe (default 8 su
  `TotalStages` = 12).

## API (endpoint)

Base `/api`. Lettura **pubblica**, scrittura **protetta** (JWT, ruolo admin).

- Public: `GET /season`, `/standings`, `/stages`, `/stages/{n}/results`,
  `/players/{id}/progression`, `/matrix`.
- Admin (`RequireAuthorization`): `PUT /season`, `POST /stages`, `POST|PUT|DELETE /results`,
  `POST /import/pdf` (preview), `POST /import/commit`.
- Auth: `POST /auth/login` → JWT. Credenziali admin da env (`Admin__Username`,
  `Admin__PasswordHash` BCrypt). ⚠️ In `AuthService.Login` la verifica username/password è
  **commentata** (accetta qualsiasi login) — va riabilitata prima di un uso reale.

## Comandi

​```bash
# DB locale
docker compose up -d                       # postgres su :5432 (db/user/pass: classifica_lega)

# API
cd api && dotnet run --project ClassificaLega.Api    # :5188 (vedi proxy.conf.json)
dotnet test                                # unit test dominio

# Client
cd client && npm start                     # ng serve, proxy /api → :5188
npm run build                              # output in dist/classifica-lega/browser
npm test                                   # karma/jasmine
​```

## Convenzioni

- Identificatori in **inglese**; termini di dominio in **italiano** dove non c'è equivalente
  pulito (Tappa/Stage, BonusRisultato, BonusPartecipazione). UI in italiano.
- Logica di scoring **solo** nel `ScoringService` di dominio — non duplicare nel client
  (`client/src/app/core/scoring.ts` è solo presentazione).
- API base nel client è relativa (`'api'`) → funziona anche sotto sottocartella.
- Niente segreti nel repo: JWT key e hash admin via env (`.env.prod.example`).