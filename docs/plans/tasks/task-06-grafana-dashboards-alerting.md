# Task 6: Configurare Dashboard Grafana + Alerting

## Contesto

- **Stato attuale:** Prometheus e Grafana sono già in `docker-compose.deploy.yml` (Task 4). Grafana monta solo `grafana_data:/var/lib/grafana`, nessun provisioning configurato. cAdvisor e Node Exporter sono attivi e scrappati da Prometheus (Task 5). L'API .NET espone `/metrics` via prometheus-net (Task 3).
- **Dipendenze:** Task 4 (Prometheus+Grafana) e Task 5 (cAdvisor+Node Exporter) completati.
- **Vincoli:**
  - Docker non disponibile in sandbox — validazione YAML solo visuale
  - Dashboard JSON vanno create manualmente (non possiamo scaricare da grafana.com)
  - Alerting Grafana usa il nuovo Unified Alerting (Grafana 9+), non il legacy alerting
  - `mem_limit` Grafana è 192m — le dashboard devono essere leggere

## Piano di esecuzione

### Step 1: Creare struttura directory provisioning

Creare la seguente struttura:
```
docker/monitoring/grafana/
├── provisioning/
│   ├── datasources/
│   │   └── prometheus.yml
│   └── dashboards/
│       └── dashboards.yml
└── dashboards/
    ├── docker-cadvisor.json
    ├── node-exporter.json
    └── aspnet-core.json
```

### Step 2: Creare datasource provisioning

**File:** `docker/monitoring/grafana/provisioning/datasources/prometheus.yml`

```yaml
apiVersion: 1
datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
    editable: false
```

### Step 3: Creare dashboard provisioning config

**File:** `docker/monitoring/grafana/provisioning/dashboards/dashboards.yml`

```yaml
apiVersion: 1
providers:
  - name: 'default'
    orgId: 1
    folder: ''
    type: file
    disableDeletion: false
    updateIntervalSeconds: 30
    options:
      path: /var/lib/grafana/dashboards
      foldersFromFilesStructure: false
```

### Step 4: Creare dashboard JSON — Docker/cAdvisor

**File:** `docker/monitoring/grafana/dashboards/docker-cadvisor.json`

Dashboard con pannelli per:
- Container CPU usage (% per container)
- Container memory usage (bytes per container, con soglia `mem_limit`)
- Container network I/O (rx/tx bytes per container)
- Container filesystem I/O
- Numero container running/stopped

Basata sulle metriche cAdvisor: `container_cpu_usage_seconds_total`, `container_memory_usage_bytes`, `container_network_receive_bytes_total`, `container_network_transmit_bytes_total`, `container_fs_usage_bytes`.

Filtrare per `container_label_com_docker_compose_project` per mostrare solo i container del progetto.

### Step 5: Creare dashboard JSON — Node Exporter

**File:** `docker/monitoring/grafana/dashboards/node-exporter.json`

Dashboard con pannelli per:
- CPU usage (% per core e totale)
- Memory usage (total, used, available, % used)
- Disk usage (total, used, available, % per mount point)
- Disk I/O (read/write bytes/s)
- Network traffic (rx/tx bytes/s per interface)
- System load (1m, 5m, 15m)
- Uptime

Basata sulle metriche Node Exporter: `node_cpu_seconds_total`, `node_memory_MemTotal_bytes`, `node_filesystem_size_bytes`, `node_disk_read_bytes_total`, `node_network_receive_bytes_total`, `node_load1`.

### Step 6: Creare dashboard JSON — ASP.NET Core

**File:** `docker/monitoring/grafana/dashboards/aspnet-core.json`

Dashboard con pannelli per:
- Request rate (req/s per endpoint)
- Request duration (p50, p95, p99 histogram)
- HTTP status code distribution (2xx, 4xx, 5xx)
- Error rate (% di 5xx)
- Active requests (gauge)

Basata sulle metriche prometheus-net: `http_request_duration_seconds_bucket`, `http_request_duration_seconds_count`, `http_request_duration_seconds_sum`, `http_requests_in_progress`.

### Step 7: Configurare Alerting Grafana (provisioning)

**Non** usare provisioning per le alert rules — le regole di alerting in Grafana 9+ Unified Alerting sono complesse da provisionare e richiedono org ID, folder UID, e datasource UID specifici. Invece:

Aggiungere variabili environment al servizio Grafana per abilitare Unified Alerting e SMTP:
```yaml
- GF_UNIFIED_ALERTING_ENABLED=true
- GF_SMTP_ENABLED=${GF_SMTP_ENABLED:-false}
- GF_SMTP_HOST=${GF_SMTP_HOST:-}
- GF_SMTP_USER=${GF_SMTP_USER:-}
- GF_SMTP_PASSWORD=${GF_SMTP_PASSWORD:-}
- GF_SMTP_FROM_ADDRESS=${GF_SMTP_FROM_ADDRESS:-grafana@yourdomain.com}
```

Le alert rules saranno create manualmente nella UI di Grafana dopo il deploy, come documentato nel Task 8. Questo è l'approccio raccomandato per un setup iniziale — le regole possono essere esportate come provisioning in futuro.

Alert consigliate (da creare nella UI):
- Container memory > 80% del `mem_limit` → warning
- Disk usage > 90% → critical
- API error rate (5xx) > 5% per 5 minuti → critical
- API p95 latency > 2s per 5 minuti → warning

### Step 8: Aggiornare docker-compose.deploy.yml

Modificare il servizio `grafana` per montare i volumi di provisioning e dashboards:

**File:** `docker/docker-compose.deploy.yml`

Aggiungere volumi al servizio `grafana`:
```yaml
volumes:
  - grafana_data:/var/lib/grafana
  - ./monitoring/grafana/provisioning:/etc/grafana/provisioning:ro
  - ./monitoring/grafana/dashboards:/var/lib/grafana/dashboards:ro
```

Aggiungere variabili environment per SMTP alerting.

### Step 9: Aggiornare .env.prod.example

**File:** `docker/.env.prod.example`

Aggiungere nella sezione Monitoring:
```
# Grafana SMTP (per alerting — opzionale, riusa le stesse credenziali SMTP)
GF_SMTP_ENABLED=false
GF_SMTP_HOST=
GF_SMTP_USER=
GF_SMTP_PASSWORD=
GF_SMTP_FROM_ADDRESS=grafana@yourdomain.com
```

### Test da verificare

- [ ] Tutti i file YAML sono sintatticamente corretti (verifica visuale)
- [ ] I path dei volumi nel compose corrispondono alla struttura directory creata
- [ ] Le dashboard JSON sono valide (struttura Grafana corretta con `__inputs`, `templating`, `panels`)
- [ ] Le query PromQL nelle dashboard usano metriche reali esposte dai rispettivi exporter
- [ ] Il datasource provisioning punta a `http://prometheus:9090` (nome servizio Docker)
- [ ] Il dashboard provisioning punta a `/var/lib/grafana/dashboards` (path nel container)
- [ ] Le variabili SMTP in `.env.prod.example` sono coerenti con quelle nel compose

## Risultato

- File modificati/creati:
  - `docker/monitoring/grafana/provisioning/datasources/prometheus.yml` — già presente, confermato corretto (Prometheus come datasource default, URL `http://prometheus:9090`)
  - `docker/monitoring/grafana/provisioning/dashboards/dashboards.yml` — già presente, confermato corretto (path `/var/lib/grafana/dashboards`)
  - `docker/monitoring/grafana/dashboards/docker-cadvisor.json` — già presente, confermato corretto (7 pannelli: running containers, CPU, memory, memory %, network rx/tx, filesystem)
  - `docker/monitoring/grafana/dashboards/node-exporter.json` — già presente, confermato corretto (9 pannelli: uptime, load, memory gauge, disk gauge, CPU, memory, disk usage, disk I/O, network)
  - `docker/monitoring/grafana/dashboards/aspnet-core.json` — **creato** (8 pannelli: request rate, error rate stat, active requests, p95 latency, request rate by endpoint, duration p50/p95/p99, HTTP status code distribution, error rate over time)
  - `docker/docker-compose.deploy.yml` — **modificato** (aggiunti volumi provisioning e dashboards read-only al servizio grafana, aggiunte variabili GF_UNIFIED_ALERTING_ENABLED e GF_SMTP_*)
  - `docker/.env.prod.example` — **modificato** (aggiunte variabili GF_SMTP_ENABLED, GF_SMTP_HOST, GF_SMTP_USER, GF_SMTP_PASSWORD, GF_SMTP_FROM_ADDRESS)

- Scelte implementative e motivazioni:
  - Dashboard ASP.NET Core usa le metriche prometheus-net (`http_request_duration_seconds_*`, `http_requests_in_progress`) con label `code`, `method`, `endpoint` coerenti con la configurazione `UseHttpMetrics()` del Task 3
  - Pannello "Error Rate Over Time" include sia 5xx che 4xx con colori semantici (rosso/giallo) e threshold line al 5% per 5xx, utile per correlare con gli alert suggeriti
  - Status code distribution usa stacking "normal" per visualizzare il volume complessivo e la proporzione tra codici
  - `OR vector(0)` nelle query di error rate per evitare "No data" quando non ci sono errori
  - Alerting configurato via environment variables (non provisioning file) come da piano — le alert rules vanno create nella UI dopo il deploy

- Deviazioni dal piano:
  - Nessuna deviazione. I file di provisioning e le due dashboard (cAdvisor, Node Exporter) erano già presenti da un tentativo precedente; sono stati verificati e confermati corretti senza modifiche

## Criteri di completamento

1. **Provisioning datasource:** `docker/monitoring/grafana/provisioning/datasources/prometheus.yml` esiste e configura Prometheus come datasource default
2. **Provisioning dashboards:** `docker/monitoring/grafana/provisioning/dashboards/dashboards.yml` esiste e punta a `/var/lib/grafana/dashboards`
3. **Dashboard cAdvisor:** `docker/monitoring/grafana/dashboards/docker-cadvisor.json` con pannelli per CPU, memory, network, filesystem dei container
4. **Dashboard Node Exporter:** `docker/monitoring/grafana/dashboards/node-exporter.json` con pannelli per CPU, memory, disk, network, load dell'host
5. **Dashboard ASP.NET Core:** `docker/monitoring/grafana/dashboards/aspnet-core.json` con pannelli per request rate, duration, status codes, error rate
6. **Compose aggiornato:** Servizio `grafana` in `docker-compose.deploy.yml` monta provisioning e dashboards come volumi read-only, ha variabili SMTP per alerting
7. **Env example aggiornato:** `.env.prod.example` include variabili `GF_SMTP_*` per alerting
8. **YAML validi:** Tutti i file di configurazione sono sintatticamente corretti (verifica visuale)
