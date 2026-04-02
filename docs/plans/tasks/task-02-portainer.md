# Task 2: Aggiungere Portainer CE

## Contesto
- Portainer è un'istanza unica a livello server, separata dai compose di ambiente (production/staging)
- Accede al Docker socket per vedere tutti i container del server
- Deve girare in un compose standalone (`docker-compose.portainer.yml`)
- Il file `.env.prod.example` esiste già in `docker/` e contiene variabili per production
- Nessun file `docker-compose.portainer.yml` esiste ancora

## Piano di esecuzione

### File da creare

1. **`docker/docker-compose.portainer.yml`** (nuovo)
   - Servizio `portainer`:
     - `image: portainer/portainer-ce:latest`
     - `container_name: portainer`
     - `restart: always` (standalone, deve partire sempre)
     - `mem_limit: 128m`
     - Volume bind: `/var/run/docker.sock:/var/run/docker.sock:ro`
     - Volume named: `portainer_data:/data`
     - Porta: `127.0.0.1:9443:9443` (HTTPS, solo localhost — accesso via SSH tunnel)
   - Definizione volume `portainer_data`
   - Nessuna network custom necessaria (Portainer accede al socket, non alla rete app)

### File da modificare

2. **`docker/.env.prod.example`**
   - Aggiungere sezione `# --- Portainer ---` con:
     - `PORTAINER_PORT=9443` (porta HTTPS esposta su localhost)

### Test da verificare
- Validazione visuale YAML (Docker non disponibile in sandbox)
- Verificare che il compose sia autonomo (nessuna dipendenza da altri file compose)
- Verificare che la porta sia bind su `127.0.0.1` (sicurezza)

## Criteri di completamento
- `docker/docker-compose.portainer.yml` creato con servizio Portainer CE configurato come da piano
- Docker socket montato read-only
- Volume named `portainer_data` per persistenza dati
- Porta 9443 esposta solo su localhost (`127.0.0.1:9443:9443`)
- `mem_limit: 128m` e `restart: always` configurati
- `.env.prod.example` aggiornato con variabile porta Portainer
- File YAML sintatticamente corretto (verifica visuale)

## Risultato
- File creati: `docker/docker-compose.portainer.yml`
- File modificati: `docker/.env.prod.example`
- Scelte implementative:
  - Usato `${PORTAINER_PORT:-9443}` nel compose per rendere la porta configurabile via `.env` con default a 9443, coerente con la variabile aggiunta in `.env.prod.example`
  - Aggiunto header con istruzioni di usage e accesso via SSH tunnel per facilitare l'onboarding
  - `restart: always` (non `unless-stopped`) come da piano, perché Portainer è standalone e deve partire anche dopo `docker compose down`
  - Nessuna network custom definita — Portainer accede al Docker socket, non alla rete applicativa
- Nessuna deviazione dal piano
