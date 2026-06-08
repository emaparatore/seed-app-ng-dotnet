# Monitoring Stack

Stack di monitoring per l'ambiente di production. Raccoglie metriche da applicazione, container e host; le visualizza in dashboard Grafana con alerting opzionale via email.

## Architettura

```
                    +------------------+
                    |     Grafana      |  Dashboard + Alerting
                    |  (localhost:3001)|
                    +--------+---------+
                             |
                    +--------v---------+
                    |    Prometheus     |  Time-series DB + Scraping
                    |  (localhost:9090) |
                    +--+-----+------+--+
                       |     |      |
          +------------+  +--+--+  +------------+
          |               |     |               |
+---------v--+  +---------v-+  +v-----------+  +v-----------+
|  API .NET  |  |  cAdvisor  |  |Node Exporter|  | Prometheus |
| :8080      |  |  :8080     |  |  :9100      |  | (self)     |
| /metrics   |  |            |  |             |  |            |
+------------+  +------------+  +-------------+  +------------+
  metriche       metriche        metriche host
  applicazione   container       (CPU, disco, rete)

+------------------+
|    Portainer CE   |  Gestione container (standalone, server-wide)
| (localhost:9443)  |  Accede al Docker socket, vede tutti i container
+------------------+
```

- **Prometheus** fa scraping ogni 15 secondi su 4 target (se stesso, API, cAdvisor, Node Exporter)
- **Grafana** legge da Prometheus e visualizza 3 dashboard pre-provisionate
- **Portainer** e indipendente dallo stack — compose separato, istanza unica a livello server

## Servizi

| Servizio | Immagine | Porta | Accesso | mem_limit | Compose file |
|---|---|---|---|---|---|
| Prometheus | `prom/prometheus:latest` | 9090 | localhost (SSH tunnel) | 384m | `docker-compose.deploy.yml` |
| Grafana | `grafana/grafana:latest` | 3001 | localhost (SSH tunnel) | 192m | `docker-compose.deploy.yml` |
| cAdvisor | `gcr.io/cadvisor/cadvisor:latest` | — | solo rete interna | 128m | `docker-compose.deploy.yml` |
| Node Exporter | `prom/node-exporter:latest` | — | solo rete interna | 64m | `docker-compose.deploy.yml` |
| Portainer CE | `portainer/portainer-ce:latest` | 9443 (HTTPS) | localhost (SSH tunnel) | 128m | `docker-compose.portainer.yml` |

Tutti i servizi espongono porte solo su `127.0.0.1` — nessuna porta pubblica. Accesso esclusivamente via SSH tunnel.

## Accesso ai servizi (SSH tunnel)

```bash
# Grafana (dal tuo PC locale)
ssh -L 3001:127.0.0.1:3001 deploy@TUO_IP_VPS
# Poi apri: http://localhost:3001

# Prometheus (dal tuo PC locale)
ssh -L 9090:127.0.0.1:9090 deploy@TUO_IP_VPS
# Poi apri: http://localhost:9090

# Portainer (dal tuo PC locale)
ssh -L 9443:127.0.0.1:9443 deploy@TUO_IP_VPS
# Poi apri: https://localhost:9443
```

Puoi combinare piu tunnel in un unico comando:

```bash
ssh -L 3001:127.0.0.1:3001 -L 9090:127.0.0.1:9090 -L 9443:127.0.0.1:9443 deploy@TUO_IP_VPS
```

## Configurazione

### Variabili d'ambiente

Le variabili di configurazione sono in `docker/.env.prod.example`. Copia in `.env` e compila:

| Variabile | Default | Descrizione |
|---|---|---|
| `PROMETHEUS_PORT` | `9090` | Porta locale Prometheus |
| `GRAFANA_PORT` | `3001` | Porta locale Grafana |
| `GRAFANA_ADMIN_PASSWORD` | — | Password admin Grafana (obbligatoria) |
| `PORTAINER_PORT` | `9443` | Porta HTTPS Portainer |
| `GF_SMTP_ENABLED` | `false` | Abilita SMTP per alerting Grafana |
| `GF_SMTP_HOST` | — | Host SMTP (es. `smtp-relay.brevo.com:587`) |
| `GF_SMTP_USER` | — | Username SMTP |
| `GF_SMTP_PASSWORD` | — | Password SMTP |
| `GF_SMTP_FROM_ADDRESS` | `grafana@yourdomain.com` | Indirizzo mittente alert email |

### Prometheus

Configurazione scrape in `docker/monitoring/prometheus.yml`:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'prometheus'       # Prometheus self-monitoring
    static_configs:
      - targets: ['localhost:9090']

  - job_name: 'api'              # Metriche .NET (prometheus-net)
    metrics_path: '/metrics'
    static_configs:
      - targets: ['api:8080']

  - job_name: 'cadvisor'         # Metriche container Docker
    static_configs:
      - targets: ['cadvisor:8080']

  - job_name: 'node-exporter'    # Metriche host (CPU, disco, rete)
    static_configs:
      - targets: ['node-exporter:9100']
```

Retention dati: 7 giorni (`--storage.tsdb.retention.time=7d`).

## Metriche custom API (.NET)

L'API espone metriche Prometheus su `/metrics` tramite il pacchetto `prometheus-net.AspNetCore`. Le metriche HTTP automatiche (`UseHttpMetrics()`) includono:

- `http_request_duration_seconds` — durata richieste (histogram, label: `code`, `method`, `endpoint`)
- `http_requests_in_progress` — richieste in corso (gauge)
- `http_requests_received_total` — totale richieste (counter)

### Aggiungere metriche custom

Per aggiungere metriche personalizzate, usa le classi di `Prometheus` nel codice C#:

```csharp
using Prometheus;

// Counter — conta eventi (es. email inviate, login falliti)
private static readonly Counter EmailsSent = Metrics
    .CreateCounter("app_emails_sent_total", "Total emails sent",
        new CounterConfiguration { LabelNames = new[] { "type" } });

// Uso:
EmailsSent.WithLabels("password_reset").Inc();
```

```csharp
// Histogram — misura distribuzioni (es. tempo di elaborazione)
private static readonly Histogram ProcessingDuration = Metrics
    .CreateHistogram("app_processing_duration_seconds", "Processing time",
        new HistogramConfiguration { Buckets = Histogram.LinearBuckets(0.1, 0.1, 10) });

// Uso:
using (ProcessingDuration.NewTimer())
{
    await ProcessAsync();
}
```

```csharp
// Gauge — valore corrente (es. job in coda, connessioni attive)
private static readonly Gauge QueueSize = Metrics
    .CreateGauge("app_queue_size", "Current queue size");

// Uso:
QueueSize.Set(queue.Count);
```

Le metriche custom appaiono automaticamente su `/metrics` e sono scrappabili da Prometheus senza configurazione aggiuntiva.

## Dashboard Grafana

### Dashboard pre-installate

Le dashboard sono provisionate automaticamente al primo avvio da `docker/monitoring/grafana/dashboards/`:

| Dashboard | File | Pannelli principali |
|---|---|---|
| **ASP.NET Core** | `aspnet-core.json` | Request rate, error rate, p95 latency, duration percentili, status codes |
| **Docker / cAdvisor** | `docker-cadvisor.json` | Container running, CPU, memory, memory %, network rx/tx, filesystem |
| **Node Exporter** | `node-exporter.json` | Uptime, load, memory, disk, CPU, network, disk I/O |

### Creare una nuova dashboard

1. Apri Grafana via SSH tunnel (`http://localhost:3001`)
2. Crea la dashboard nella UI (+ > New Dashboard)
3. Aggiungi pannelli usando query PromQL sulla datasource "Prometheus" (gia configurata)
4. Salva la dashboard

### Esportare/importare dashboard JSON

**Esportare:**
1. Apri la dashboard in Grafana
2. Vai in Settings (icona ingranaggio) > JSON Model
3. Copia il JSON e salvalo in `docker/monitoring/grafana/dashboards/<nome>.json`

**Importare:**
1. Salva il file JSON in `docker/monitoring/grafana/dashboards/`
2. Riavvia Grafana: `docker compose -f docker-compose.deploy.yml restart grafana`
3. La dashboard appare automaticamente grazie al provisioning

Per dashboard dalla community (grafana.com/grafana/dashboards):
1. Copia l'ID della dashboard (es. 1860 per Node Exporter Full)
2. In Grafana: + > Import > inserisci l'ID > seleziona datasource "Prometheus"

## Alerting

Gli alert sono configurati nella UI di Grafana (non via provisioning file). Unified Alerting e abilitato di default (`GF_UNIFIED_ALERTING_ENABLED=true`).

### Configurare alert rules

1. Apri Grafana > Alerting > Alert rules > New alert rule
2. Definisci la query PromQL (esempi sotto)
3. Imposta soglia e durata (es. "for 5m" per evitare falsi positivi)
4. Assegna un contact point per le notifiche

### Esempi di alert rules utili

**Container vicino al memory limit (>80%):**
```promql
container_memory_usage_bytes{name=~"seed-.*"} / container_spec_memory_limit_bytes{name=~"seed-.*"} > 0.8
```

**Disco host >90%:**
```promql
(1 - node_filesystem_avail_bytes{mountpoint="/"} / node_filesystem_size_bytes{mountpoint="/"}) > 0.9
```

**Error rate API (5xx >5% negli ultimi 5 minuti):**
```promql
sum(rate(http_request_duration_seconds_count{code=~"5.."}[5m])) / sum(rate(http_request_duration_seconds_count[5m])) > 0.05
```

**API target down:**
```promql
up{job="api"} == 0
```

### Configurare notifiche email

1. Imposta le variabili SMTP in `.env` (vedi sezione Configurazione sopra)
2. In Grafana: Alerting > Contact points > New contact point
3. Tipo: Email, inserisci gli indirizzi destinatari
4. Testa con "Test" per verificare la configurazione SMTP

## Sviluppo locale

In locale lo stack di monitoring e opzionale, attivabile tramite Docker Compose profile:

```bash
cd docker

# Avvia tutto (app + monitoring)
docker compose --profile monitoring up

# Avvia solo monitoring (se l'app e gia attiva)
docker compose --profile monitoring up prometheus grafana cadvisor node-exporter

# Ferma tutto
docker compose --profile monitoring down
```

Accesso in locale (senza SSH tunnel):
- Grafana: `http://localhost:3001` (admin / admin)
- Prometheus: `http://localhost:9090`

I servizi di monitoring in dev usano le stesse configurazioni e dashboard della production (`docker/monitoring/`).

### Portainer in produzione (VPS)

Portainer è un servizio standalone server-wide — **non fa parte del compose principale** (`docker-compose.deploy.yml`) e va avviato manualmente una volta sola sul VPS.

Il file `docker-compose.portainer.yml` viene copiato automaticamente dalla CI in `/opt/seed-app/` ad ogni deploy.

```bash
# Sul VPS (prima volta, o dopo un reset)
cd /opt/seed-app
docker compose -f docker-compose.portainer.yml up -d
```

Grazie a `restart: always`, si riavvia automaticamente dopo ogni reboot del server — non serve rieseguire il comando ai deploy successivi.

Per accedere dal tuo PC locale via SSH tunnel:

```bash
ssh -L 9443:127.0.0.1:9443 deploy@TUO_IP_VPS
# Poi apri: https://localhost:9443
```

### Portainer in locale

Portainer non e incluso nel compose di sviluppo (e un servizio standalone server-wide). Se serve in locale:

```bash
cd docker
docker compose -f docker-compose.portainer.yml up -d
# Apri: https://localhost:9443
```

## Troubleshooting

### Metriche API non visibili in Prometheus

1. Verifica che l'API sia avviata e healthy:
   ```bash
   docker compose -f docker-compose.deploy.yml ps api
   ```
2. Verifica che l'endpoint `/metrics` risponda:
   ```bash
   docker exec <api-container> curl -s http://localhost:8080/metrics | head -5
   ```
3. In Prometheus (Status > Targets), verifica che il target `api` sia "UP"

### Grafana mostra "No data"

1. Verifica che Prometheus sia raggiungibile: in Grafana vai in Configuration > Data sources > Prometheus > "Test"
2. Verifica che le metriche esistano: in Prometheus, prova la query manualmente (es. `http_request_duration_seconds_count`)
3. Controlla il time range della dashboard (default: Last 1 hour) — se l'API non ha ricevuto traffico, le metriche saranno vuote

### Prometheus target "DOWN"

1. Controlla i log di Prometheus:
   ```bash
   docker compose -f docker-compose.deploy.yml logs --tail 50 prometheus
   ```
2. Verifica che il servizio target sia sulla stessa network (`app-network`):
   ```bash
   docker network inspect seed-production_app-network
   ```
3. Testa la connettivita dal container Prometheus:
   ```bash
   docker exec <prometheus-container> wget -qO- http://api:8080/metrics | head -5
   ```

### Grafana non si avvia

1. Controlla i log:
   ```bash
   docker compose -f docker-compose.deploy.yml logs --tail 50 grafana
   ```
2. Verifica che la password admin sia impostata in `.env` (`GRAFANA_ADMIN_PASSWORD`)
3. Se i volumi sono corrotti, rimuovi e ricrea:
   ```bash
   docker compose -f docker-compose.deploy.yml down grafana
   docker volume rm seed-production_grafana_data
   docker compose -f docker-compose.deploy.yml up -d grafana
   ```
   **Attenzione:** questo cancella dashboard create manualmente e alert rules. Le dashboard provisionate vengono ricreate automaticamente.

### cAdvisor alto uso CPU

cAdvisor puo consumare CPU significativa su host con molti container. Se necessario, aumenta l'intervallo di raccolta aggiungendo il flag `--housekeeping_interval=30s` nel `command` del servizio in `docker-compose.deploy.yml`.
