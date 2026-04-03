# Task 8: Documentazione Monitoring Stack

## Contesto

- Lo stack di monitoring e completamente implementato (Task 1-7 tutti Done)
- Servizi in `docker/docker-compose.deploy.yml`: prometheus, grafana, cadvisor, node-exporter (production)
- Portainer in `docker/docker-compose.portainer.yml` (standalone, server-wide)
- Dev compose in `docker/docker-compose.yml` con profile `monitoring`
- prometheus-net integrato in `backend/src/Seed.Api/Program.cs`
- Config Prometheus in `docker/monitoring/prometheus.yml` (4 scrape targets: prometheus, api, cadvisor, node-exporter)
- Dashboard Grafana pre-provisionate: `docker/monitoring/grafana/dashboards/` (aspnet-core.json, docker-cadvisor.json, node-exporter.json)
- Provisioning Grafana: datasource Prometheus + dashboards auto-load
- `.env.prod.example` gia aggiornato con variabili monitoring (PROMETHEUS_PORT, GRAFANA_PORT, GRAFANA_ADMIN_PASSWORD, GF_SMTP_*)
- `docs/vps-setup-guide.md` ha sezione "12. Monitoraggio" (riga 643) con log container, Seq, docker stats, spazio disco — manca sezione monitoring stack
- `CLAUDE.md` existing docs index (riga 265+) non ha `docs/monitoring.md`

## Piano di esecuzione

### File da creare

1. **`docs/monitoring.md`** (nuovo) — documentazione completa dello stack di monitoring:
   - Architettura: diagramma ASCII dello stack (Prometheus scraping -> api/cadvisor/node-exporter, Grafana -> Prometheus, Portainer -> Docker socket)
   - Servizi: tabella con servizio, porta, accesso, mem_limit
   - Accesso ai servizi: SSH tunnel per Grafana (3001), Prometheus (9090), Portainer (9443)
   - Configurazione variabili .env (riferimento a .env.prod.example)
   - Metriche custom API: come aggiungere metriche prometheus-net custom (Counter, Histogram, Gauge) con esempio codice C#
   - Dashboard Grafana: elenco dashboard pre-installate, come creare nuove dashboard, come esportare/importare JSON
   - Alerting: come configurare alert rules nella UI Grafana, configurazione SMTP per notifiche email
   - Sviluppo locale: come avviare lo stack in dev con `docker compose --profile monitoring up`
   - Troubleshooting: problemi comuni (metriche non visibili, Grafana "No data", Prometheus target down)

### File da modificare

2. **`docs/vps-setup-guide.md`** — aggiungere sottosezione "Monitoring Stack" dentro sezione "12. Monitoraggio" (dopo riga 706, dopo la verifica Seq, prima di "Statistiche dei container" riga 707):
   - Comandi SSH tunnel per Grafana, Prometheus, Portainer
   - Primo accesso Grafana (cambio password admin)
   - Primo accesso Portainer (setup admin entro 5 minuti dal primo avvio)
   - Avvio Portainer standalone: `docker compose -f docker-compose.portainer.yml up -d`
   - Riferimento a `docs/monitoring.md` per dettagli completi

3. **`CLAUDE.md`** — aggiungere entry nella lista "Existing docs:" (dopo riga 276, `adding-collaborators.md`, prima di `docs/plans/`):
   ```
   - `docs/monitoring.md` — Monitoring stack: Prometheus, Grafana, cAdvisor, Node Exporter, Portainer, metriche custom, alerting. Read when touching monitoring config, dashboards, or metrics.
   ```

4. **`README.md`** — verificare se ha tabella docs e aggiungere riga monitoring

### Approccio step-by-step

1. Creare `docs/monitoring.md` con tutte le sezioni
2. Aggiungere sezione "Monitoring Stack" in `docs/vps-setup-guide.md` dentro sezione 12
3. Aggiungere entry `docs/monitoring.md` in `CLAUDE.md` nella lista existing docs
4. Aggiungere riga in `README.md` se ha tabella docs
5. Verificare che `.env.prod.example` sia gia completo (non dovrebbe servire modifica)

### Test da verificare

- Nessun test da scrivere (task di sola documentazione)
- Verificare che comandi SSH tunnel e porte siano coerenti con configurazione compose
- Verificare link relativi tra documenti

## Criteri di completamento

- [ ] `docs/monitoring.md` creato con sezioni: architettura, servizi, accesso SSH tunnel, metriche custom, dashboard, alerting, dev locale, troubleshooting
- [ ] `docs/vps-setup-guide.md` aggiornato con sottosezione "Monitoring Stack" in sezione 12 (SSH tunnel Grafana/Prometheus/Portainer, primo accesso)
- [ ] `CLAUDE.md` aggiornato con entry `docs/monitoring.md` nella lista existing docs
- [ ] `README.md` aggiornato con riga `docs/monitoring.md` nella tabella docs (se presente)
- [ ] `.env.prod.example` verificato come gia completo (nessuna modifica necessaria)
- [ ] Nessun file non previsto dal piano viene modificato

## Risultato

- File creati:
  - `docs/monitoring.md` — documentazione completa con sezioni: architettura (diagramma ASCII), servizi (tabella), accesso SSH tunnel, configurazione variabili, metriche custom API (Counter, Histogram, Gauge con esempi C#), dashboard Grafana (pre-installate + creazione + export/import), alerting (alert rules PromQL + notifiche email), sviluppo locale (profile monitoring), troubleshooting (5 scenari comuni)
- File modificati:
  - `docs/vps-setup-guide.md` — aggiunta sottosezione "Monitoring Stack" dentro sezione 12, dopo la verifica Seq e prima di "Statistiche dei container". Include: comandi SSH tunnel per Grafana/Prometheus/Portainer, primo accesso Grafana, avvio Portainer standalone con nota sui 5 minuti per setup admin, riferimento a docs/monitoring.md
  - `CLAUDE.md` — aggiunta entry `docs/monitoring.md` nella lista "Existing docs:" (dopo adding-collaborators.md, prima di docs/plans/)
  - `README.md` — aggiunta riga `Monitoring` nella tabella Documentation (prima di Troubleshooting)
- File verificati senza modifiche:
  - `docker/.env.prod.example` — gia completo con tutte le variabili monitoring (PROMETHEUS_PORT, GRAFANA_PORT, GRAFANA_ADMIN_PASSWORD, GF_SMTP_*)
- Scelte implementative:
  - Documentazione scritta in italiano coerente con lo stile degli altri doc del progetto
  - Diagramma ASCII scelto per l'architettura perche leggibile direttamente nel markdown senza dipendenze esterne
  - Esempi PromQL per alert rules derivati dalle metriche effettive esposte da prometheus-net e dagli exporter configurati
  - Porte e configurazioni verificate direttamente dai file compose e prometheus.yml per garantire coerenza
- Deviazioni dal piano: nessuna
