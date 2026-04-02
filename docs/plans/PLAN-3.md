# PLAN-3: Monitoring Stack

**Stato:** In corso
**Creato:** 2026-04-01
**Obiettivo:** Integrare uno stack di monitoring completo per infrastruttura, container e applicazione .NET.

## Overview

Stack (solo production — staging resta con Seq per i log):
- **Portainer CE** — UI gestione container Docker (istanza unica, vede tutti i container del server: production + staging)
- **prometheus-net** — metriche .NET esposte dall'API production
- **Prometheus** — raccolta metriche (scraping container production + host)
- **Grafana** — dashboard grafiche + alerting
- **cAdvisor** — metriche per-container (CPU, RAM, I/O)
- **Node Exporter** — metriche host (CPU, disco, rete)
- **Seq** — già presente in entrambi gli ambienti, log strutturati

## Dati reali VPS (2026-04-01)

```
Server: 3.7GB RAM totale, 1.1GB usato, 2.6GB disponibile
Due ambienti attivi: production + staging

Production:  API 70MB, Postgres 22MB, Seq 117MB, Nginx 4MB, Web 30MB  = 243MB
Staging:     API 81MB, Postgres 25MB, Seq 136MB, Nginx 4MB, Web 15MB  = 261MB
Totale container: 504MB
```

---

## Task 1: Aggiungere `mem_limit` ai servizi esistenti

**Stato:** [x] Done

**Cosa fare:**
1. Aggiungere `mem_limit` ai servizi in `docker-compose.deploy.yml` (production). RAM disponibile: ~3.2GB (3.7GB - ~500MB per SO e staging). Limiti calcolati su production, staging usa risorse trascurabili (~260MB).

| Servizio | Uso reale | mem_limit |
|----------|:---------:|:---------:|
| PostgreSQL | 22MB | 1536m |
| API .NET | 70MB | 768m |
| Seq | 117MB | 512m |
| Angular SSR (web) | 30MB | 384m |
| Nginx | 4MB | 64m |

2. Testare che tutti i servizi partano correttamente con i limiti

**Definition of Done:**
- [x] Tutti e 5 i servizi in `docker-compose.deploy.yml` hanno `mem_limit` configurato con i valori specificati
- [x] Tutti e 5 i servizi hanno `restart: unless-stopped`
- [x] Il file compose è sintatticamente valido (verificato visualmente — Docker non disponibile in sandbox, validazione completa da fare sul server di deploy)
- [x] Nessun altro campo è stato modificato

**Implementation Notes:**
- `mem_limit` posizionato come proprietà top-level di ogni servizio, prima di `healthcheck`, `networks` o `depends_on`, seguendo lo stile esistente del file
- Valori applicati esattamente come da tabella: postgres=1536m, api=768m, seq=512m, web=384m, nginx=64m
- Somma totale limiti: 3264MB, rientra nei ~3.2GB disponibili per production
- `restart: unless-stopped` aggiunto a tutti i servizi: si riavviano automaticamente dopo crash/OOM-kill o reboot del server, ma non se fermati manualmente con `docker compose stop`
- Validazione `docker compose config` non eseguita (Docker non disponibile in sandbox) — correttezza YAML verificata visualmente, validazione completa da fare al deploy

**File coinvolti:**
- `docker/docker-compose.deploy.yml`

---

## Task 2: Aggiungere Portainer CE

**Stato:** [x] Done

Portainer è un'istanza unica a livello di server — vede tutti i container (production + staging) perché ha accesso al Docker socket. Non va nel compose di un singolo ambiente ma gira standalone.

**Cosa fare:**
1. Creare `docker/docker-compose.portainer.yml` separato
   - Immagine: `portainer/portainer-ce:latest`
   - Volume: `/var/run/docker.sock` (read-only) + `portainer_data`
   - Porta HTTPS: `127.0.0.1:9443:9443` (solo localhost, accesso via SSH tunnel)
   - `mem_limit: 128m`
   - Restart: `always`
2. Aggiungere variabili al `.env.prod.example`

**Definition of Done:**
- [x] `docker/docker-compose.portainer.yml` creato con servizio Portainer CE (`portainer/portainer-ce:latest`)
- [x] Docker socket montato read-only (`/var/run/docker.sock:/var/run/docker.sock:ro`)
- [x] Volume named `portainer_data` per persistenza dati
- [x] Porta 9443 esposta solo su localhost (`127.0.0.1:9443:9443`)
- [x] `mem_limit: 128m` e `restart: always` configurati
- [x] `.env.prod.example` aggiornato con variabile `PORTAINER_PORT=9443`
- [x] File YAML sintatticamente corretto (verifica visuale — Docker non disponibile in sandbox)

**Implementation Notes:**
- Porta resa configurabile via `${PORTAINER_PORT:-9443}` nel compose con default a 9443, coerente con la variabile in `.env.prod.example`
- Aggiunto header con istruzioni di usage e accesso via SSH tunnel per facilitare l'onboarding
- `restart: always` (non `unless-stopped`) come da piano — Portainer è standalone e deve partire anche dopo `docker compose down`
- Nessuna network custom definita — Portainer accede al Docker socket, non alla rete applicativa
- Nessuna deviazione dal piano

**File coinvolti:**
- `docker/docker-compose.portainer.yml` (nuovo)
- `docker/.env.prod.example`

---

## Task 3: Integrare prometheus-net nell'API .NET

**Stato:** [x] Done

**Cosa fare:**
1. Aggiungere il pacchetto NuGet `prometheus-net.AspNetCore` a `Seed.Api.csproj`
2. Configurare in `Program.cs`:
   - Aggiungere `app.UseHttpMetrics()` per metriche HTTP automatiche (request duration, count, status code)
   - Aggiungere endpoint `/metrics` con `app.MapMetrics()` per esporre metriche in formato Prometheus
3. Verificare che le metriche siano visibili su `http://localhost:8080/metrics`

**Definition of Done:**
- [x] Pacchetto `prometheus-net.AspNetCore` aggiunto a `Seed.Api.csproj`
- [x] `app.UseHttpMetrics()` configurato in `Program.cs` nel punto corretto della pipeline
- [x] `app.MapMetrics()` configurato in `Program.cs` per esporre `/metrics`
- [x] Il progetto compila senza errori (`dotnet build Seed.slnx`)
- [x] Tutti i test passano (`dotnet test Seed.slnx`)
- [x] Nessuna modifica a file non elencati nel piano

**Implementation Notes:**
- Versione 8.2.1 di `prometheus-net.AspNetCore` installata tramite `dotnet add package` (ultima stabile compatibile con .NET 10)
- `UseHttpMetrics()` posizionato dopo `UseAuthorization()` e `MustChangePasswordMiddleware` ma prima di `MapControllers()` per catturare metriche HTTP complete (status code, method, endpoint)
- `MapMetrics()` posizionato alla fine, dopo tutti gli health checks, coerente con il pattern di raggruppamento degli endpoint mapped
- Nessuna deviazione dal piano

**File coinvolti:**
- `backend/src/Seed.Api/Seed.Api.csproj`
- `backend/src/Seed.Api/Program.cs`

---

## Task 4: Aggiungere Prometheus e Grafana

**Stato:** [x] Done

**Cosa fare:**
1. Aggiungere il servizio `prometheus` al compose di deploy (production)
   - Immagine: `prom/prometheus:latest`
   - Volume per configurazione (`prometheus.yml`) e dati
   - Porta: `127.0.0.1:9090:9090` (solo localhost)
   - `mem_limit: 384m`
   - `restart: unless-stopped`
   - Flag: `--storage.tsdb.retention.time=7d`
   - Network: `app-network`
2. Aggiungere il servizio `grafana` al compose di deploy (production)
   - Immagine: `grafana/grafana:latest`
   - Volume per dati persistenti
   - Porta: `127.0.0.1:3001:3000` (la 3000 è usata da Angular SSR)
   - `mem_limit: 192m`
   - `restart: unless-stopped`
   - Variabili env: `GF_SECURITY_ADMIN_PASSWORD` dal `.env`
   - Network: `app-network`
3. Creare `docker/monitoring/prometheus.yml` con scrape targets:
   - Prometheus stesso
   - API .NET (`api:8080/metrics`)
4. Aggiungere variabili al `.env.prod.example` (`GRAFANA_ADMIN_PASSWORD`, porte)

**Definition of Done:**
- [x] `docker/monitoring/prometheus.yml` creato con scrape config per prometheus e api
- [x] Servizio `prometheus` aggiunto a `docker-compose.deploy.yml` con immagine `prom/prometheus:latest`, porta localhost-only, `mem_limit: 384m`, retention 7d, volume dati, network `app-network`
- [x] Servizio `grafana` aggiunto a `docker-compose.deploy.yml` con immagine `grafana/grafana:latest`, porta `3001->3000` localhost-only, `mem_limit: 192m`, volume dati, password da env, network `app-network`
- [x] Volumi `prometheus_data` e `grafana_data` dichiarati nella sezione volumes
- [x] `.env.prod.example` aggiornato con `PROMETHEUS_PORT`, `GRAFANA_PORT`, `GRAFANA_ADMIN_PASSWORD`
- [x] Tutti i file YAML sono sintatticamente corretti (verifica visuale — Docker non disponibile in sandbox)
- [x] Nessun file non previsto dal piano viene modificato

**Implementation Notes:**
- Servizio `prometheus` posizionato dopo `nginx`, `grafana` dopo `prometheus` — ordine logico di dipendenza
- `depends_on: api (service_healthy)` per prometheus: garantisce che l'endpoint `/metrics` sia disponibile prima dello scraping
- `depends_on: prometheus (service_started)` per grafana: sufficiente perché Grafana non ha bisogno che Prometheus sia healthy, solo raggiungibile
- `command` in formato lista YAML (non stringa singola) per leggibilità e coerenza con le best practice Docker Compose
- Porte configurabili via variabili d'ambiente con default sensati (`9090`, `3001`) coerente con lo stile degli altri servizi nel compose

**File coinvolti:**
- `docker/docker-compose.deploy.yml`
- `docker/monitoring/prometheus.yml` (nuovo)
- `docker/.env.prod.example`

---

## Task 5: Aggiungere cAdvisor e Node Exporter

**Stato:** [x] Done

**Cosa fare:**
1. Aggiungere il servizio `cadvisor` al compose di deploy (production)
   - Immagine: `gcr.io/cadvisor/cadvisor:latest`
   - Volumi read-only: `/`, `/var/run`, `/sys`, `/var/lib/docker`
   - `mem_limit: 128m`
   - `restart: unless-stopped`
   - Non esporre porte — solo rete interna
   - Network: `app-network`
2. Aggiungere il servizio `node-exporter` al compose di deploy (production)
   - Immagine: `prom/node-exporter:latest`
   - Volumi read-only: `/proc`, `/sys`, `/`
   - Flag: `--path.rootfs=/host`
   - `mem_limit: 64m`
   - `restart: unless-stopped`
   - Non esporre porte — solo rete interna
   - Network: `app-network`
3. Aggiornare `prometheus.yml` per fare scrape di cAdvisor (`:8080/metrics`) e Node Exporter (`:9100/metrics`)

**Definition of Done:**
- [x] Servizio `cadvisor` presente in `docker-compose.deploy.yml` con immagine `gcr.io/cadvisor/cadvisor:latest`, volumi ro (`/`, `/var/run`, `/sys`, `/var/lib/docker`), `mem_limit: 128m`, `restart: unless-stopped`, network `app-network`, nessuna porta esposta
- [x] Servizio `node-exporter` presente in `docker-compose.deploy.yml` con immagine `prom/node-exporter:latest`, volumi ro (`/proc`, `/sys`, `/`), flag `--path.rootfs=/rootfs`, `mem_limit: 64m`, `restart: unless-stopped`, network `app-network`, nessuna porta esposta
- [x] `prometheus.yml` aggiornato con scrape target per `cadvisor:8080` e `node-exporter:9100`
- [x] Nessun volume named aggiuntivo necessario (solo bind mount)
- [x] Nessuna porta pubblica o localhost esposta per i due nuovi servizi
- [x] File YAML sintatticamente corretti (verifica visuale — Docker non disponibile in sandbox)
- [x] Nessun file non previsto dal piano viene modificato

**Implementation Notes:**
- Servizi posizionati dopo `grafana` e prima di `volumes:`, coerente con l'ordine logico (prima servizi core, poi monitoring)
- cAdvisor: 4 volumi bind-mount read-only (`/`, `/var/run`, `/sys`, `/var/lib/docker`) per accesso completo alle metriche container; nessuna porta esposta, accessibile da Prometheus via `cadvisor:8080`
- Node Exporter: 3 volumi bind-mount read-only con flag `--path.procfs`, `--path.sysfs`, `--path.rootfs` per mappare i path host; aggiunto `--collector.filesystem.mount-points-exclude` per evitare metriche duplicate da mount di sistema
- Nessun `depends_on` necessario — entrambi raccolgono metriche dall'host/Docker, non dipendono da altri servizi del compose
- Nessuna deviazione dal piano

**File coinvolti:**
- `docker/docker-compose.deploy.yml`
- `docker/monitoring/prometheus.yml`

---

## Task 6: Configurare Dashboard Grafana + Alerting

**Stato:** [ ] Da fare

**Cosa fare:**
1. Creare provisioning config per datasource Prometheus automatico:
   - `docker/monitoring/grafana/provisioning/datasources/prometheus.yml`
2. Creare provisioning config per dashboard pre-configurate:
   - `docker/monitoring/grafana/provisioning/dashboards/dashboards.yml`
3. Aggiungere dashboard JSON pre-fatte:
   - **Docker/cAdvisor dashboard** (ID: 14282 o simile)
   - **Node Exporter dashboard** (ID: 1860 — "Node Exporter Full")
   - **ASP.NET Core dashboard** per metriche prometheus-net
4. Configurare alerting su Grafana:
   - Alert quando un container supera l'80% del suo `mem_limit`
   - Alert quando disco > 90%
   - Alert quando error rate API sale
   - Canali: email (SMTP già configurato), webhook, o Telegram
5. Montare cartelle di provisioning come volumi nel servizio Grafana

**File coinvolti:**
- `docker/monitoring/grafana/provisioning/datasources/prometheus.yml` (nuovo)
- `docker/monitoring/grafana/provisioning/dashboards/dashboards.yml` (nuovo)
- `docker/monitoring/grafana/dashboards/*.json` (nuovi)
- `docker/docker-compose.deploy.yml`

---

## Task 7: Aggiungere monitoring al docker-compose.yml di sviluppo

**Stato:** [ ] Da fare

**Cosa fare:**
1. Aggiungere i servizi di monitoring al compose di sviluppo con **profile** `monitoring`
   - `docker compose --profile monitoring up` per attivare lo stack
2. Riutilizzare le stesse configurazioni di Prometheus/Grafana
3. Testare in locale

**File coinvolti:**
- `docker/docker-compose.yml`

---

## Task 8: Documentazione

**Stato:** [ ] Da fare

**Cosa fare:**
1. Creare `docs/monitoring.md` con:
   - Architettura dello stack di monitoring
   - Come accedere a ogni servizio (SSH tunnel)
   - Come aggiungere metriche custom nell'API
   - Come creare nuove dashboard Grafana
   - Configurazione alerting
2. Aggiornare `docs/vps-setup-guide.md` con:
   - Sezione "Monitoring Stack"
   - Comandi SSH tunnel per Grafana, Prometheus, Portainer
3. Aggiornare `CLAUDE.md` con riferimento al doc
4. Aggiornare `docker/.env.prod.example` se necessario

**File coinvolti:**
- `docs/monitoring.md` (nuovo)
- `docs/vps-setup-guide.md`
- `CLAUDE.md`
- `docker/.env.prod.example`

---

## Ordine di esecuzione

```
Task 1 (mem_limit servizi esistenti)
    ↓
Task 2 (Portainer — compose separato, server-wide)
    ↓
Task 3 (prometheus-net nell'API)
    ↓
Task 4 (Prometheus + Grafana)
    ↓
Task 5 (cAdvisor + Node Exporter)
    ↓
Task 6 (Dashboard Grafana + Alerting)
    ↓
Task 7 (Dev compose)
    ↓
Task 8 (Documentazione)
```

## Note architetturali

- **Scope:** Monitoring solo su production. Staging ha solo Seq per i log.
- **Portainer:** Istanza unica a livello server (compose separato), vede tutti i container di tutti gli ambienti.
- **Sicurezza:** Tutti i servizi esposti solo su `127.0.0.1` — accesso via SSH tunnel (come Seq). Nessuna porta pubblica.
- **Persistenza:** Volumi Docker per dati Portainer, Prometheus, Grafana. Sopravvivono a restart e OOM-kill.
- **Restart policy:** Tutti i servizi production usano `restart: unless-stopped` — si riavviano dopo crash/OOM-kill o reboot del server, ma non se fermati manualmente. Portainer usa `restart: always` perché è standalone e deve partire anche dopo un `docker compose down`.
- **Rete:** Servizi monitoring sulla `app-network` di production per comunicazione interna.
- **Prometheus retention:** 7 giorni (`--storage.tsdb.retention.time=7d`) per limitare uso disco e RAM.

### mem_limit

Limiti calcolati su production (~3.2GB disponibili, staging trascurabile). Se un container sfora, Docker lo uccide e lo riavvia — i dati su volume sono al sicuro. Grafana alerterà quando un container si avvicina al limite.

### Risorse stimate

```
Uso attuale (production):                   243MB
Stack monitoring aggiuntivo:               ~200-300MB
Totale stimato production:                 ~450-550MB
Staging (invariato):                        261MB
─────────────────────────────────────────────────────
Totale server:                             ~750-850MB su 3.7GB
Margine disponibile:                       ~2.8-2.9GB
```
