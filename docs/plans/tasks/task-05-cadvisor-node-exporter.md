# Task 5: Aggiungere cAdvisor e Node Exporter

## Contesto

- `docker/docker-compose.deploy.yml` ha gia' Prometheus e Grafana (Task 4) sulla `app-network`, con porte localhost-only e `mem_limit`
- `docker/monitoring/prometheus.yml` ha scrape config per `prometheus` (localhost:9090) e `api` (api:8080/metrics)
- cAdvisor e Node Exporter devono essere aggiunti come servizi interni (nessuna porta esposta) e registrati come scrape target in Prometheus
- Volumi del compose attuale: `postgres_data`, `seq_data`, `certbot_conf`, `prometheus_data`, `grafana_data` — non servono nuovi volumi named (cAdvisor e Node Exporter usano bind mount read-only dall'host)

## Piano di esecuzione

### 1. Aggiungere servizio `cadvisor` a `docker-compose.deploy.yml`

**File:** `docker/docker-compose.deploy.yml`
**Posizione:** dopo il servizio `grafana`, prima della sezione `volumes:`

```yaml
  cadvisor:
    image: gcr.io/cadvisor/cadvisor:latest
    restart: unless-stopped
    volumes:
      - /:/rootfs:ro
      - /var/run:/var/run:ro
      - /sys:/sys:ro
      - /var/lib/docker/:/var/lib/docker:ro
    mem_limit: 128m
    networks:
      - app-network
```

- Nessuna porta esposta (solo accesso interno da Prometheus via `cadvisor:8080`)
- Volumi read-only per accesso a metriche container
- Nessun `depends_on` necessario

### 2. Aggiungere servizio `node-exporter` a `docker-compose.deploy.yml`

**File:** `docker/docker-compose.deploy.yml`
**Posizione:** dopo `cadvisor`, prima della sezione `volumes:`

```yaml
  node-exporter:
    image: prom/node-exporter:latest
    restart: unless-stopped
    volumes:
      - /proc:/host/proc:ro
      - /sys:/host/sys:ro
      - /:/rootfs:ro
    command:
      - '--path.procfs=/host/proc'
      - '--path.sysfs=/host/sys'
      - '--path.rootfs=/rootfs'
      - '--collector.filesystem.mount-points-exclude=^/(sys|proc|dev|host|etc)($$|/)'
    mem_limit: 64m
    networks:
      - app-network
```

- Flag `--path.rootfs=/rootfs` come da piano
- Flags aggiuntivi `--path.procfs` e `--path.sysfs` necessari per mappare i path montati
- `--collector.filesystem.mount-points-exclude` per evitare metriche duplicate da mount di sistema
- Nessuna porta esposta (solo accesso interno da Prometheus via `node-exporter:9100`)

### 3. Aggiornare `prometheus.yml` con nuovi scrape target

**File:** `docker/monitoring/prometheus.yml`

Aggiungere due job alla sezione `scrape_configs`:

```yaml
  - job_name: 'cadvisor'
    static_configs:
      - targets: ['cadvisor:8080']

  - job_name: 'node-exporter'
    static_configs:
      - targets: ['node-exporter:9100']
```

### 4. Nessun test da scrivere

- Sono file di configurazione Docker/YAML, non codice applicativo
- Validazione sintattica visuale (Docker non disponibile in sandbox)
- Nessun file backend/frontend modificato, quindi nessun test unitario o di integrazione impattato

## Criteri di completamento

- [ ] Servizio `cadvisor` presente in `docker-compose.deploy.yml` con immagine `gcr.io/cadvisor/cadvisor:latest`, volumi ro (`/`, `/var/run`, `/sys`, `/var/lib/docker`), `mem_limit: 128m`, `restart: unless-stopped`, network `app-network`, nessuna porta esposta
- [ ] Servizio `node-exporter` presente in `docker-compose.deploy.yml` con immagine `prom/node-exporter:latest`, volumi ro (`/proc`, `/sys`, `/`), flag `--path.rootfs=/rootfs`, `mem_limit: 64m`, `restart: unless-stopped`, network `app-network`, nessuna porta esposta
- [ ] `prometheus.yml` aggiornato con scrape target per `cadvisor:8080` e `node-exporter:9100`
- [ ] Nessun volume named aggiuntivo necessario (solo bind mount)
- [ ] Nessuna porta pubblica o localhost esposta per i due nuovi servizi
- [ ] File YAML sintatticamente corretti (verifica visuale)
- [ ] Nessun file non previsto dal piano viene modificato

## Risultato

- **File modificati:**
  - `docker/docker-compose.deploy.yml` — aggiunti servizi `cadvisor` e `node-exporter`
  - `docker/monitoring/prometheus.yml` — aggiunti scrape target per `cadvisor:8080` e `node-exporter:9100`

- **Scelte implementative e motivazioni:**
  - Servizi posizionati dopo `grafana` e prima di `volumes:`, coerente con l'ordine logico (prima i servizi core, poi il monitoring)
  - Nessuna porta esposta per entrambi i servizi — accessibili solo internamente da Prometheus via `app-network`
  - cAdvisor: 4 volumi bind-mount read-only (`/`, `/var/run`, `/sys`, `/var/lib/docker`) per accesso completo alle metriche container
  - Node Exporter: 3 volumi bind-mount read-only (`/proc`, `/sys`, `/`) con flag `--path.procfs`, `--path.sysfs`, `--path.rootfs` per mappare i path host nei mount container; aggiunto `--collector.filesystem.mount-points-exclude` per evitare metriche duplicate da mount di sistema
  - `mem_limit` come da piano: 128m per cAdvisor, 64m per Node Exporter
  - Nessun `depends_on` necessario — entrambi raccolgono metriche dall'host/Docker, non dipendono da altri servizi del compose
  - Nessun volume named aggiuntivo — solo bind mount read-only

- **Deviazioni dal piano:** Nessuna
