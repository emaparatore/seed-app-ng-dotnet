# Task 4: Aggiungere Prometheus e Grafana

## Contesto

- `docker/docker-compose.deploy.yml` contiene 5 servizi production (postgres, seq, api, web, nginx) tutti con `mem_limit` e `restart: unless-stopped`, collegati a `app-network`
- L'API .NET espone metriche su `/metrics` (porta 8080 interna) tramite prometheus-net (Task 3 completato)
- `.env.prod.example` ha variabili per Portainer, Seq, PostgreSQL, ecc.
- La directory `docker/monitoring/` non esiste ancora — va creata
- Volumi dichiarati in fondo al compose: `postgres_data`, `seq_data`, `certbot_conf`
- Network: `app-network` bridge
- Porta 3000 usata da Angular SSR, quindi Grafana va esposta su 3001 esternamente

## Piano di esecuzione

### Step 1: Creare `docker/monitoring/prometheus.yml`

Creare la directory `docker/monitoring/` e il file di configurazione Prometheus:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'prometheus'
    static_configs:
      - targets: ['localhost:9090']

  - job_name: 'api'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['api:8080']
```

### Step 2: Aggiungere servizio `prometheus` a `docker/docker-compose.deploy.yml`

Aggiungere dopo il servizio `nginx`:

- Immagine: `prom/prometheus:latest`
- Porta: `127.0.0.1:${PROMETHEUS_PORT:-9090}:9090` (solo localhost)
- Volumi: `./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml:ro` + `prometheus_data:/prometheus`
- `mem_limit: 384m`
- `restart: unless-stopped`
- Command: `--config.file=/etc/prometheus/prometheus.yml --storage.tsdb.retention.time=7d --web.console.libraries=/etc/prometheus/console_libraries --web.console.templates=/etc/prometheus/consoles`
- Network: `app-network`
- `depends_on`: api (service_healthy) — per poter scrape-are le metriche

### Step 3: Aggiungere servizio `grafana` a `docker/docker-compose.deploy.yml`

Aggiungere dopo `prometheus`:

- Immagine: `grafana/grafana:latest`
- Porta: `127.0.0.1:${GRAFANA_PORT:-3001}:3000` (solo localhost, 3001 esterna)
- Volume: `grafana_data:/var/lib/grafana`
- `mem_limit: 192m`
- `restart: unless-stopped`
- Environment: `GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_ADMIN_PASSWORD}`, `GF_USERS_ALLOW_SIGN_UP=false`
- Network: `app-network`
- `depends_on`: prometheus (service_started)

### Step 4: Aggiungere volumi `prometheus_data` e `grafana_data`

Alla sezione `volumes:` in fondo al compose, aggiungere:
- `prometheus_data:`
- `grafana_data:`

### Step 5: Aggiornare `docker/.env.prod.example`

Aggiungere sezione monitoring:

```
# --- Monitoring ---
PROMETHEUS_PORT=9090
GRAFANA_PORT=3001
GRAFANA_ADMIN_PASSWORD=<strong-password-here>
```

### File da creare/modificare (path esatti)

- `docker/monitoring/prometheus.yml` (nuovo)
- `docker/docker-compose.deploy.yml` (modificare: aggiungere 2 servizi + 2 volumi)
- `docker/.env.prod.example` (modificare: aggiungere 3 variabili)

### Test da verificare

- Il YAML di `docker-compose.deploy.yml` e `prometheus.yml` deve essere sintatticamente corretto (verifica visuale, Docker non disponibile in sandbox)
- I servizi prometheus e grafana devono avere `mem_limit`, `restart: unless-stopped`, network `app-network`
- Le porte devono essere esposte solo su `127.0.0.1`
- Il prometheus.yml deve avere scrape target per prometheus stesso e per l'API .NET
- Le variabili in `.env.prod.example` devono avere default sensati nel compose

## Criteri di completamento

- [ ] `docker/monitoring/prometheus.yml` creato con scrape config per prometheus e api
- [ ] Servizio `prometheus` aggiunto a `docker-compose.deploy.yml` con immagine `prom/prometheus:latest`, porta localhost-only, `mem_limit: 384m`, retention 7d, volume dati, network `app-network`
- [ ] Servizio `grafana` aggiunto a `docker-compose.deploy.yml` con immagine `grafana/grafana:latest`, porta `3001->3000` localhost-only, `mem_limit: 192m`, volume dati, password da env, network `app-network`
- [ ] Volumi `prometheus_data` e `grafana_data` dichiarati nella sezione volumes
- [ ] `.env.prod.example` aggiornato con `PROMETHEUS_PORT`, `GRAFANA_PORT`, `GRAFANA_ADMIN_PASSWORD`
- [ ] Tutti i file YAML sono sintatticamente corretti (verifica visuale)
- [ ] Nessun file non previsto dal piano viene modificato

## Risultato

- File creati: `docker/monitoring/prometheus.yml`
- File modificati: `docker/docker-compose.deploy.yml`, `docker/.env.prod.example`
- Scelte implementative:
  - Servizio `prometheus` posizionato dopo `nginx`, `grafana` dopo `prometheus` — ordine logico di dipendenza
  - `depends_on: api (service_healthy)` per prometheus: garantisce che l'endpoint `/metrics` sia disponibile prima dello scraping
  - `depends_on: prometheus (service_started)` per grafana: sufficiente perche' Grafana non ha bisogno che Prometheus sia healthy, solo raggiungibile
  - `command` in formato lista YAML (non stringa singola) per leggibilita' e coerenza con le best practice Docker Compose
  - Porte configurabili via variabili d'ambiente con default sensati (`9090`, `3001`) coerente con lo stile degli altri servizi nel compose
- Nessuna deviazione dal piano
