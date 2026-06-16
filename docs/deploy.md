# Fase 6 — Deploy VM

Pipeline: push su `main` → GitHub Actions builda immagini **ARM64** → push su **GHCR** →
SSH sulla VM → `docker compose pull && up -d`.

- Subdomain: `lega-pauper-massarosa.gmarra.it` (HTTPS dal reverse proxy host esistente).
- DB: database dedicato `classifica_lega` nell'istanza Postgres esistente (container `postgres`).
- Immagini: `ghcr.io/gmarra-it/gestione-lega-api`, `ghcr.io/gmarra-it/gestione-lega`.

---

## 1. Secret GitHub (repo → Settings → Secrets → Actions)

| Secret | Valore |
|---|---|
| `VM_HOST` | IP/host della VM |
| `VM_USER` | utente SSH deploy |
| `VM_SSH_KEY` | chiave privata SSH (stessa rotazione di manarank/gymshark) |

`GITHUB_TOKEN` è automatico (push su GHCR). Rendi i package GHCR leggibili dalla VM:
se privati, fai `docker login ghcr.io` sulla VM con un PAT `read:packages`.

## 2. Setup VM (una tantum)

```bash
# 2.1 — DB dedicato nell'istanza Postgres esistente
docker exec -it postgres psql -U postgres -c "CREATE USER classifica_lega WITH PASSWORD '<password>';"
docker exec -it postgres psql -U postgres -c "CREATE DATABASE classifica_lega OWNER classifica_lega;"

# 2.2 — nome della rete dove gira il container postgres (serve a .env → POSTGRES_NETWORK)
docker inspect -f '{{range $k,$v := .NetworkSettings.Networks}}{{$k}}{{end}}' postgres

# 2.3 — cartella deploy
sudo mkdir -p /opt/gestione-lega && cd /opt/gestione-lega
# copia qui docker-compose.prod.yml (come docker-compose.yml) e crea .env
```

`/opt/gestione-lega/.env` da `.env.prod.example` (vedi §3 per l'hash admin).
L'API applica le migration EF e fa il seed all'avvio (`db.Database.MigrateAsync()`),
quindi il DB vuoto si popola da solo al primo `up`.

Primo avvio manuale:
```bash
cd /opt/gestione-lega
docker login ghcr.io   # se package privati
docker compose pull
docker compose up -d
docker compose logs -f api   # verifica migration + seed
```

## 3. Hash password admin (BCrypt)

L'API valida con BCrypt (`Admin__PasswordHash`). Genera l'hash dalla password scelta:

```bash
docker run --rm mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet fsi --use:/dev/stdin <<'EOF'
#r "nuget: BCrypt.Net-Next, 4.2.0"
printfn "%s" (BCrypt.Net.BCrypt.HashPassword "LA_TUA_PASSWORD")
EOF
```

Metti l'output in `ADMIN_PASSWORD_HASH` nel `.env` (attento all'escaping dei `$`:
in un file `.env` lasciali letterali, non servono virgolette).

## 4. Reverse proxy host (nginx) → porta 8090

Il container client pubblica su `127.0.0.1:8090`. L'nginx dell'host termina HTTPS e proxa:

```nginx
server {
    server_name lega-pauper-massarosa.gmarra.it;

    location / {
        proxy_pass http://127.0.0.1:8090;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
    # certificati gestiti dal proxy esistente (certbot/Let's Encrypt)
}
```

`/api` non va instradato qui: lo gestisce l'nginx **dentro** il container client
(`client/nginx.conf`), che proxa `/api/` → `http://api:8080` sulla rete compose.

## 5. Flusso a regime

Push su `main`:
- modifiche in `api/**` → workflow **Deploy API**;
- modifiche in `client/**` → workflow **Deploy Client**.

Ogni workflow builda ARM64, pusha su GHCR, poi via SSH `docker compose pull <svc> && up -d <svc>`.

**AC:** push su `main` deploya; `https://lega-pauper-massarosa.gmarra.it` risponde,
classifica/tappe pubbliche visibili, login admin funzionante.
