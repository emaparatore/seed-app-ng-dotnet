# Task 7: Aggiungere monitoring al docker-compose.yml di sviluppo

## Contesto

- `docker/docker-compose.yml` contiene i servizi di sviluppo: postgres, seq, mailpit, api, web, sandbox
- Non ha network custom (usa la default bridge di compose)
- Non ha servizi di monitoring
- `docker/docker-compose.deploy.yml` ha già prometheus, grafana, cadvisor, node-exporter configurati sulla `app-network`
- Le configurazioni di monitoring esistono già in `docker/monitoring/`:
  - `prometheus.yml` — scrape config per prometheus, api, cadvisor, node-exporter
  - `grafana/provisioning/datasources/prometheus.yml` — datasource Prometheus
  - `grafana/provisioning/dashboards/dashboards.yml` — provisioning dashboards
  - `grafana/dashboards/*.json` — 3 dashboard JSON (aspnet-core, docker-cadvisor, node-exporter)
- Il task richiede l'uso di Docker Compose **profiles** (`monitoring`) per attivazione opzionale

## Piano di esecuzione

### File da modificare
- `docker/docker-compose.yml`

### Approccio step-by-step

1. **Aggiungere servizio `prometheus`** con profile `monitoring`:
   - Immagine: `prom/prometheus:latest`
   - Porta: `9090:9090` (dev, non serve localhost-only)
   - Volume: riutilizzare `./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml:ro`
   - Volume named: `prometheus_data` per persistenza
   - Command: stesse flag del deploy (`--storage.tsdb.retention.time=7d`, etc.)
   - `depends_on: api` (service_started, non healthy — dev non ha healthcheck sull'api)
   - `profiles: [monitoring]`

2. **Aggiungere servizio `grafana`** con profile `monitoring`:
   - Immagine: `grafana/grafana:latest`
   - Porta: `3001:3000` (evita conflitto con Angular SSR su 4200, e dev potrebbe avere altro su 3000)
   - Volumi: `grafana_data`, provisioning e dashboards (stessi path del deploy, read-only)
   - Environment: `GF_SECURITY_ADMIN_PASSWORD=admin` (dev, password semplice), `GF_USERS_ALLOW_SIGN_UP=false`
   - `depends_on: prometheus` (service_started)
   - `profiles: [monitoring]`

3. **Aggiungere servizio `cadvisor`** con profile `monitoring`:
   - Immagine: `gcr.io/cadvisor/cadvisor:latest`
   - Volumi bind-mount ro: `/:/rootfs`, `/var/run:/var/run`, `/sys:/sys`, `/var/lib/docker:/var/lib/docker`
   - Nessuna porta esposta (accessibile da Prometheus via nome servizio)
   - `profiles: [monitoring]`

4. **Aggiungere servizio `node-exporter`** con profile `monitoring`:
   - Immagine: `prom/node-exporter:latest`
   - Volumi bind-mount ro: `/proc:/host/proc`, `/sys:/host/sys`, `/:/rootfs`
   - Command: stesse flag del deploy (`--path.procfs`, `--path.sysfs`, `--path.rootfs`, `--collector.filesystem.mount-points-exclude`)
   - Nessuna porta esposta
   - `profiles: [monitoring]`

5. **Aggiungere volumi named** alla sezione `volumes:`:
   - `prometheus_data`
   - `grafana_data`

6. **Nota su prometheus.yml**: La configurazione esistente in `docker/monitoring/prometheus.yml` usa `api:8080` come target per le metriche .NET. Questo funziona anche in dev perché il servizio `api` nel compose di sviluppo espone la porta 8080 internamente (il container del SDK esegue su `0.0.0.0:8080`). I target `cadvisor:8080` e `node-exporter:9100` funzionano tramite DNS interno del compose. Il target `localhost:9090` di Prometheus funziona perché è self-referencing.

### Differenze rispetto al deploy

- **Nessun `mem_limit`**: in dev non servono limiti di memoria
- **Nessun `restart` policy**: in dev i servizi si fermano con `docker compose down`
- **Porte accessibili direttamente**: non serve `127.0.0.1:` prefix (dev è locale)
- **Grafana password hardcoded `admin`**: in dev non serve sicurezza
- **Nessuna config SMTP per Grafana**: alerting via email non necessario in dev
- **Profile `monitoring`**: tutti i 4 servizi sotto profile per non avviarli di default

### Test da verificare

- `docker compose config` deve validare il YAML senza errori (Docker non disponibile in sandbox — verifica visuale)
- `docker compose --profile monitoring config` deve mostrare i servizi di monitoring
- `docker compose up` (senza profile) NON deve avviare prometheus/grafana/cadvisor/node-exporter

## Criteri di completamento

- [ ] 4 servizi aggiunti a `docker/docker-compose.yml`: prometheus, grafana, cadvisor, node-exporter
- [ ] Tutti e 4 hanno `profiles: [monitoring]`
- [ ] Prometheus riutilizza `./monitoring/prometheus.yml` e ha volume `prometheus_data`
- [ ] Grafana riutilizza provisioning e dashboards dal deploy, ha volume `grafana_data`, porta `3001:3000`
- [ ] cAdvisor e node-exporter hanno gli stessi volumi e flag del deploy
- [ ] Volumi `prometheus_data` e `grafana_data` dichiarati nella sezione `volumes:`
- [ ] Nessun servizio esistente è stato modificato
- [ ] Il YAML è sintatticamente corretto (verifica visuale)

## Risultato

### File modificati/creati
- `docker/docker-compose.yml` — aggiunto 4 servizi di monitoring e 2 volumi named

### Scelte implementative e motivazioni
- **`container_name` con suffisso `-dev`**: aggiunto a tutti e 4 i servizi (`seed-prometheus-dev`, `seed-grafana-dev`, `seed-cadvisor-dev`, `seed-node-exporter-dev`) per coerenza con gli altri servizi del compose di sviluppo che usano tutti `container_name`
- **`depends_on: api (service_started)`** per prometheus: in dev l'api non ha healthcheck, quindi si usa `service_started` invece di `service_healthy` come indicato nel mini-plan
- **Porta Grafana `3001:3000`**: evita conflitto con eventuali processi su porta 3000, coerente con il deploy
- **Porte senza `127.0.0.1:` prefix**: in dev non serve restringere a localhost, coerente con gli altri servizi dev (postgres su 5432, seq su 5341/8081, etc.)
- **Nessun `mem_limit`, `restart`, network custom**: come specificato nel piano, in dev non servono limiti di memoria, restart policy, o network custom (usa la default bridge di compose)
- **Nessuna config SMTP per Grafana**: alerting via email non necessario in dev, solo `GF_SECURITY_ADMIN_PASSWORD=admin` e `GF_USERS_ALLOW_SIGN_UP=false`
- **Stessi volumi e command del deploy** per cadvisor e node-exporter: garantisce parità di metriche raccolte tra dev e production

### Eventuali deviazioni dal piano
- Nessuna deviazione dal piano
