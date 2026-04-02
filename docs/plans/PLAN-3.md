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
- [x] Il file compose è sintatticamente valido (verificato visualmente — Docker non disponibile in sandbox, validazione completa da fare sul server di deploy)
- [x] Nessun altro campo è stato modificato

**Implementation Notes:**
- `mem_limit` posizionato come proprietà top-level di ogni servizio, prima di `healthcheck`, `networks` o `depends_on`, seguendo lo stile esistente del file
- Valori applicati esattamente come da tabella: postgres=1536m, api=768m, seq=512m, web=384m, nginx=64m
- Somma totale limiti: 3264MB, rientra nei ~3.2GB disponibili per production
- Validazione `docker compose config` non eseguita (Docker non disponibile in sandbox) — correttezza YAML verificata visualmente, validazione completa da fare al deploy

**File coinvolti:**
- `docker/docker-compose.deploy.yml`

---

## Task 2: Aggiungere Portainer CE

**Stato:** [ ] Da fare

Portainer è un'istanza unica a livello di server — vede tutti i container (production + staging) perché ha accesso al Docker socket. Non va nel compose di un singolo ambiente ma gira standalone.

**Cosa fare:**
1. Creare `docker/docker-compose.portainer.yml` separato
   - Immagine: `portainer/portainer-ce:latest`
   - Volume: `/var/run/docker.sock` (read-only) + `portainer_data`
   - Porta HTTPS: `127.0.0.1:9443:9443` (solo localhost, accesso via SSH tunnel)
   - `mem_limit: 128m`
   - Restart: `always`
2. Aggiungere variabili al `.env.prod.example`

**File coinvolti:**
- `docker/docker-compose.portainer.yml` (nuovo)
- `docker/.env.prod.example`

---

## Task 3: Integrare prometheus-net nell'API .NET

**Stato:** [ ] Da fare

**Cosa fare:**
1. Aggiungere il pacchetto NuGet `prometheus-net.AspNetCore` a `Seed.Api.csproj`
2. Configurare in `Program.cs`:
   - Aggiungere `app.UseHttpMetrics()` per metriche HTTP automatiche (request duration, count, status code)
   - Aggiungere endpoint `/metrics` con `app.MapMetrics()` per esporre metriche in formato Prometheus
3. Verificare che le metriche siano visibili su `http://localhost:8080/metrics`

**File coinvolti:**
- `backend/src/Seed.Api/Seed.Api.csproj`
- `backend/src/Seed.Api/Program.cs`

---

## Task 4: Aggiungere Prometheus e Grafana

**Stato:** [ ] Da fare

**Cosa fare:**
1. Aggiungere il servizio `prometheus` al compose di deploy (production)
   - Immagine: `prom/prometheus:latest`
   - Volume per configurazione (`prometheus.yml`) e dati
   - Porta: `127.0.0.1:9090:9090` (solo localhost)
   - `mem_limit: 384m`
   - Flag: `--storage.tsdb.retention.time=7d`
   - Network: `app-network`
2. Aggiungere il servizio `grafana` al compose di deploy (production)
   - Immagine: `grafana/grafana:latest`
   - Volume per dati persistenti
   - Porta: `127.0.0.1:3001:3000` (la 3000 è usata da Angular SSR)
   - `mem_limit: 192m`
   - Variabili env: `GF_SECURITY_ADMIN_PASSWORD` dal `.env`
   - Network: `app-network`
3. Creare `docker/monitoring/prometheus.yml` con scrape targets:
   - Prometheus stesso
   - API .NET (`api:8080/metrics`)
4. Aggiungere variabili al `.env.prod.example` (`GRAFANA_ADMIN_PASSWORD`, porte)

**File coinvolti:**
- `docker/docker-compose.deploy.yml`
- `docker/monitoring/prometheus.yml` (nuovo)
- `docker/.env.prod.example`

---

## Task 5: Aggiungere cAdvisor e Node Exporter

**Stato:** [ ] Da fare

**Cosa fare:**
1. Aggiungere il servizio `cadvisor` al compose di deploy (production)
   - Immagine: `gcr.io/cadvisor/cadvisor:latest`
   - Volumi read-only: `/`, `/var/run`, `/sys`, `/var/lib/docker`
   - `mem_limit: 128m`
   - Non esporre porte — solo rete interna
   - Network: `app-network`
2. Aggiungere il servizio `node-exporter` al compose di deploy (production)
   - Immagine: `prom/node-exporter:latest`
   - Volumi read-only: `/proc`, `/sys`, `/`
   - Flag: `--path.rootfs=/host`
   - `mem_limit: 64m`
   - Non esporre porte — solo rete interna
   - Network: `app-network`
3. Aggiornare `prometheus.yml` per fare scrape di cAdvisor (`:8080/metrics`) e Node Exporter (`:9100/metrics`)

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
