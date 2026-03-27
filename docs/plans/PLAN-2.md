# PLAN-2: Staging + Production deploy sullo stesso VPS

## Obiettivo

Configurare deploy automatico su due ambienti (staging e production) sullo stesso VPS:
- PR mergiata in `dev` → deploy in staging (`staging.dominio.com`)
- PR mergiata in `master` → deploy in production (`dominio.com`)
- Staging protetto da Cloudflare Access (non accessibile pubblicamente)
- Zero downtime per production durante l'implementazione

## Architettura target

```
/opt/seed-app/
├── production/
│   ├── docker-compose.deploy.yml   ← copiato dal CI
│   ├── .env                         ← IMAGE_TAG=latest, DOMAIN_NAME=dominio.com
│   ├── nginx/                       ← config nginx (già presente)
│   └── scripts/                     ← migrate.sh, seed.sh, restore.sh
├── staging/
│   ├── docker-compose.deploy.yml   ← copiato dal CI
│   ├── .env                         ← IMAGE_TAG=dev, DOMAIN_NAME=staging.dominio.com
│   ├── nginx/                       ← stessa config, dominio diverso da .env
│   └── scripts/
└── backups/                         ← condiviso (sottocartelle per env)
```

Ogni ambiente è uno stack Docker Compose completamente isolato:
- Container names generati automaticamente (no hardcoded)
- Database separato per staging
- Volumi separati (postgres_data, seq_data, certbot_conf)
- Porte esterne diverse: production 80/443, staging 8080/8443

Cloudflare Origin Rule su `staging.dominio.com` redirige il traffico alla porta 8443 del VPS.

## Decisioni chiave

| Decisione | Scelta | Motivazione |
|-----------|--------|-------------|
| Isolamento ambienti | Due directory + due stack compose | Nessun rischio di interferenza, `.env` separati |
| Protezione staging | Cloudflare Access (free tier) | Già usi Cloudflare, zero config lato VPS, fino a 50 utenti gratis |
| Porte staging | 8080/8443 + Cloudflare Origin Rule | Evita un reverse proxy condiviso, ogni stack è indipendente |
| SSL staging | Stesso Cloudflare Origin Certificate (wildcard `*.dominio.com`) | Il cert generato al setup copre già il wildcard |
| Container names | Rimossi (generati dal compose project name) | Evita conflitti tra stack, il project name fa da prefisso |
| Database staging | DB separato nello stack staging | Isolamento completo, dati di test non inquinano production |
| Backup directory | `/opt/seed-app/backups/{production,staging}/` | Backup separati per ambiente |

---

## Task

### Task 1: Parametrizzare docker-compose.deploy.yml
**File:** `docker/docker-compose.deploy.yml`

Modifiche:
- [x]Rimuovere tutti i `container_name` hardcoded (5 servizi)
- [x]Cambiare `name: seed-app-deploy` in `name: ${COMPOSE_PROJECT_NAME:-seed-production}`
- [x]Parametrizzare le porte nginx: `${NGINX_HTTP_PORT:-80}:80` e `${NGINX_HTTPS_PORT:-443}:443`
- [x]Parametrizzare la porta Seq: `127.0.0.1:${SEQ_PORT:-8081}:80`
- [x]Aggiungere variabile `Client__BaseUrl` che usa `${CLIENT_BASE_URL}` invece di costruirlo da `DOMAIN_NAME` (per supportare `https://` vs `http://` e subdomini)

> **Nota**: I service names interni (postgres, api, web, nginx, seq) restano invariati — sono nomi di rete interni al compose e non confliggono tra stack.

---

### Task 2: Aggiornare .env.prod.example
**File:** `docker/.env.prod.example`

Aggiungere le nuove variabili con commenti:

```env
# --- Stack Configuration ---
# COMPOSE_PROJECT_NAME differenzia i container tra ambienti sullo stesso server.
# Usare "seed-production" per production e "seed-staging" per staging.
COMPOSE_PROJECT_NAME=seed-production

# Porte esterne Nginx (production: 80/443, staging: 8080/8443)
NGINX_HTTP_PORT=80
NGINX_HTTPS_PORT=443

# Porta esterna Seq (production: 8081, staging: 8082)
SEQ_PORT=8081
```

Aggiornare `Client__BaseUrl`:
```env
# URL base dell'applicazione (usato nelle email e redirect)
CLIENT_BASE_URL=https://yourdomain.com
```

---

### Task 3: Aggiornare deploy.yml per dual-environment
**File:** `.github/workflows/deploy.yml`

Modifiche:
- [x]Nel job `determine-environment`, aggiungere output `deploy_dir` (`/opt/seed-app/production` o `/opt/seed-app/staging`)
- [x]Aggiornare lo step "Sync deploy files to VPS": creare la directory corretta, copiare nella directory giusta
- [x]Aggiornare lo step "Deploy via SSH": usare `deploy_dir` per `cd`, `COMPOSE_FILE` e `SCRIPT_DIR`
- [x]Aggiornare il path di BACKUP_DIR in base all'environment
- [x]Copiare anche i file nginx (templates + nginx.conf) — attualmente non vengono copiati dal CI, restano dal clone iniziale

Logica path:
```yaml
# production
deploy_dir=/opt/seed-app/production
backup_dir=/opt/seed-app/backups/production

# staging
deploy_dir=/opt/seed-app/staging
backup_dir=/opt/seed-app/backups/staging
```

---

### Task 4: Aggiornare scripts per environment-aware paths
**File:** `docker/scripts/migrate.sh`, `docker/scripts/seed.sh`

Modifiche:
- [x]Cambiare `BACKUP_DIR` default: da `/opt/seed-app/backups` a un path relativo o passato dal deploy workflow
- [x]Il `COMPOSE_FILE` è già parametrizzato via env var, va bene così
- [x]Aggiornare `restore.sh` per supportare i nuovi path dei backup

---

### Task 5: Aggiornare docs/vps-setup-guide.md
**File:** `docs/vps-setup-guide.md`

Riscrittura parziale per la nuova struttura:
- [x]**Sezione 5** (Directory di Deploy): riscrivere completamente — non più "clona il repo", ma struttura `production/` + `staging/`
- [x]**Sezione 6** (Variabili d'Ambiente): aggiungere le nuove variabili, mostrare esempio `.env` per entrambi gli ambienti
- [x]**Sezione 7** (Cloudflare + DNS): aggiungere record DNS per `staging.dominio.com` + Origin Rule per porta 8443
- [x]**Nuova sezione**: Configurazione Cloudflare Access per proteggere staging
- [x]**Sezione 8** (SSL): spiegare che il wildcard certificate copre anche staging, come creare il volume per staging
- [x]**Sezione 9** (Primo Deploy): aggiornare per la nuova struttura
- [x]**Sezione 4** (Firewall): aggiungere porta 8443 per staging
- [x]**Sezione 13** (Troubleshooting): aggiornare riferimenti ai path
- [x]**Cheat sheet**: aggiornare per dual-environment
- [x]**Nuova sezione**: Guida migrazione per chi ha la struttura vecchia (repo clonato)

---

### Task 6: Aggiornare docs/ci-cd.md
**File:** `docs/ci-cd.md`

- [x]Aggiornare la sezione "Deploy" con i nuovi path e la logica dual-environment
- [x]Aggiornare la sezione "GitHub Settings Required" (nuovi secrets se necessari, firewall)
- [x]Aggiornare i riferimenti al percorso `/opt/seed-app/`

---

### Task 7: Aggiornare CLAUDE.md
- [x]Aggiornare la entry di `docs/vps-setup-guide.md` nella lista docs se la descrizione cambia

---

### Task 8 (manuale, post-deploy): Migrazione VPS
Questa è un'operazione manuale da fare sul VPS. Va documentata nella guida ma non è codice.

Passi:
1. Fermare lo stack corrente: `cd /opt/seed-app/docker && docker compose -f docker-compose.deploy.yml down`
2. Creare la nuova struttura:
   ```bash
   mkdir -p /opt/seed-app/{production,staging}/{docker/nginx/templates,scripts}
   mkdir -p /opt/seed-app/backups/{production,staging}
   ```
3. Spostare i file production:
   ```bash
   cp /opt/seed-app/docker/docker-compose.deploy.yml /opt/seed-app/production/docker/
   cp /opt/seed-app/docker/.env /opt/seed-app/production/docker/
   cp -r /opt/seed-app/docker/nginx/ /opt/seed-app/production/docker/nginx/
   cp /opt/seed-app/scripts/* /opt/seed-app/production/scripts/
   mv /opt/seed-app/backups/*.sql.gz /opt/seed-app/backups/production/ 2>/dev/null || true
   ```
4. Aggiornare il `.env` di production con le nuove variabili:
   ```bash
   echo "COMPOSE_PROJECT_NAME=seed-production" >> /opt/seed-app/production/docker/.env
   echo "NGINX_HTTP_PORT=80" >> /opt/seed-app/production/docker/.env
   echo "NGINX_HTTPS_PORT=443" >> /opt/seed-app/production/docker/.env
   echo "SEQ_PORT=8081" >> /opt/seed-app/production/docker/.env
   echo "CLIENT_BASE_URL=https://tuodominio.com" >> /opt/seed-app/production/docker/.env
   ```
5. Creare il volume SSL per staging (copia lo stesso wildcard cert):
   ```bash
   docker volume create seed-staging_certbot_conf
   sudo cp -r /var/lib/docker/volumes/seed-app-deploy_certbot_conf/_data/* \
     /var/lib/docker/volumes/seed-staging_certbot_conf/_data/
   ```
   Rinominare/symlink la directory del cert per il subdomain staging se necessario.
6. Creare `.env` per staging (copiare da production, modificare valori)
7. Avviare production con il nuovo compose: `cd /opt/seed-app/production/docker && docker compose up -d`
8. Verificare che production funzioni
9. Rimuovere il vecchio repo clonato (dopo aver verificato che tutto funziona):
   ```bash
   rm -rf /opt/seed-app/backend /opt/seed-app/frontend /opt/seed-app/.git /opt/seed-app/.github
   rm -rf /opt/seed-app/docs /opt/seed-app/.claude /opt/seed-app/CLAUDE.md /opt/seed-app/README.md
   rm -rf /opt/seed-app/.gitignore /opt/seed-app/Seed.slnx
   ```
10. Aprire porta firewall: `sudo ufw allow 8443/tcp`
11. Configurare Cloudflare: DNS record + Access policy + Origin Rule

---

## Ordine di esecuzione

1. **Task 1-2** — Compose + .env (nessun impatto su production finché non si fa deploy)
2. **Task 3** — Deploy workflow (cambio path, compatibile col vecchio finché non si migra il VPS)
3. **Task 4** — Scripts (backward compatible grazie ai default)
4. **Task 5-7** — Documentazione
5. **Task 8** — Migrazione manuale VPS (da fare DOPO aver mergiato i cambi e PRIMA del prossimo deploy)

> ⚠️ **DECISION REQUIRED**: Il Task 8 (migrazione VPS) va fatto in una finestra di manutenzione. Production avrà ~1 minuto di downtime durante lo spostamento dei file e il riavvio dello stack. Confermi che va bene?

---

## Status

| Task | Stato |
|------|-------|
| Task 1: docker-compose.deploy.yml | ✅ Done |
| Task 2: .env.prod.example | ✅ Done |
| Task 3: deploy.yml | ✅ Done |
| Task 4: Scripts | ✅ Done |
| Task 5: vps-setup-guide.md | ✅ Done |
| Task 6: ci-cd.md | ✅ Done |
| Task 7: CLAUDE.md | ✅ Done (nessuna modifica necessaria) |
| Task 8: Migrazione VPS (manuale) | ⬜ Pending (operazione manuale — documentata nella guida) |
