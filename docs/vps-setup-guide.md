# Guida: Deploy su VPS con Docker Compose

Questa guida spiega come deployare l'applicazione Seed App su un VPS (Virtual Private Server) usando Docker Compose, Nginx come reverse proxy, Cloudflare come CDN/protezione e SSL con Let's Encrypt.

## Architettura

```
Utente --> Cloudflare (CDN, DDoS protection, SSL edge)
              --> VPS (HTTPS/443) --> Nginx --> Angular SSR (frontend)
                                           --> .NET API (backend)
                                  Certbot (rinnovo automatico SSL)
                                  PostgreSQL (solo rete interna)
                                  Seq (log, accessibile via SSH tunnel)
```

---

## 1. Scelta del Provider VPS

### Hetzner Cloud (raccomandato)

- **CX22**: 2 vCPU, 4 GB RAM, 40 GB SSD — ~4,50 EUR/mese
- Datacenter in Germania e Finlandia
- Accetta carte Revolut/prepagate
- Ottimo rapporto qualita/prezzo
- Sito: https://www.hetzner.com/cloud

### Alternative

| Provider | Piano consigliato | Prezzo | Note |
|----------|------------------|--------|------|
| DigitalOcean | Basic Droplet 2 vCPU / 4 GB | ~24 USD/mese | Ottima documentazione |
| Contabo | VPS S (4 vCPU / 8 GB) | ~6 EUR/mese | Piu economico, meno supporto |
| OVH | VPS Starter | ~7 EUR/mese | Datacenter EU |

### Requisiti minimi

- 2 vCPU, 4 GB RAM, 40 GB SSD
- Ubuntu 24.04 LTS
- IPv4 pubblico

---

## 2. Setup Iniziale del Server

### 2.1 Primo accesso

Dopo aver creato il VPS, accedi come root con la password ricevuta via email (o SSH key se configurata dal provider):

```bash
ssh root@TUO_IP_VPS
```

### 2.2 Aggiornare il sistema

```bash
apt update && apt upgrade -y
```

### 2.3 Creare un utente non-root

Non usare mai `root` per le operazioni quotidiane:

```bash
adduser deploy
usermod -aG sudo deploy
```

### 2.4 Configurare le chiavi SSH

Dal tuo **computer locale**, copia la tua chiave pubblica sul server:

```bash
# Dal tuo PC locale (se non hai una chiave SSH, creala con: ssh-keygen -t ed25519)
ssh-copy-id deploy@TUO_IP_VPS
```

Verifica di poter accedere senza password:

```bash
ssh deploy@TUO_IP_VPS
```

### 2.5 Disabilitare l'accesso con password

Sul server, modifica la configurazione SSH:

```bash
sudo nano /etc/ssh/sshd_config
```

Trova e modifica queste righe:

```
PasswordAuthentication no
PermitRootLogin no
```

Riavvia SSH:

```bash
sudo systemctl restart ssh
```

> **ATTENZIONE**: Prima di chiudere la sessione corrente, apri un nuovo terminale e verifica di poter accedere con la chiave SSH. Se qualcosa va storto e ti chiudi fuori, dovrai usare la console web del provider.

---

## 3. Installazione Docker

```bash
# Installa le dipendenze
sudo apt install -y ca-certificates curl gnupg

# Aggiungi la chiave GPG di Docker
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

# Aggiungi il repository Docker
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Installa Docker
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Aggiungi l'utente deploy al gruppo docker
sudo usermod -aG docker deploy
```

**Esci e rientra** per applicare il gruppo:

```bash
exit
ssh deploy@TUO_IP_VPS
```

Verifica:

```bash
docker --version
docker compose version
```

---

## 4. Configurazione Firewall

```bash
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

Verifica:

```bash
sudo ufw status
```

Output atteso:

```
To                         Action      From
--                         ------      ----
OpenSSH                    ALLOW       Anywhere
80/tcp                     ALLOW       Anywhere
443/tcp                    ALLOW       Anywhere
```

---

## 5. Preparazione della Directory di Deploy

```bash
sudo mkdir -p /opt/seed-app
sudo chown deploy:deploy /opt/seed-app
cd /opt/seed-app
```

### Copia i file necessari dal repository

Puoi clonare il repo o copiare solo i file necessari. Ecco i file minimi:

```
/opt/seed-app/
  docker-compose.deploy.yml
  .env
  nginx/
    nginx.conf
    templates/
      default.conf.template
```

**Opzione 1 — Clona il repo** (consigliato per la prima volta):

```bash
cd /opt/seed-app
git clone https://github.com/TUO_USERNAME/seed-app-ng-dotnet.git .

# I file sono nella cartella docker/, spostiamoci
cd docker
```

> Nota: se cloni il repo, i comandi docker compose vanno eseguiti dalla cartella `docker/`.

**Opzione 2 — Copia solo i file necessari** (con scp dal tuo PC locale):

```bash
# Dal tuo PC locale
scp -r docker/docker-compose.deploy.yml docker/nginx deploy@TUO_IP_VPS:/opt/seed-app/
scp docker/.env.prod.example deploy@TUO_IP_VPS:/opt/seed-app/.env
```

---

## 6. Configurazione delle Variabili d'Ambiente

```bash
cd /opt/seed-app    # o /opt/seed-app/docker se hai clonato il repo
cp .env.prod.example .env  # se non l'hai gia copiato
nano .env
```

Compila tutti i valori:

```env
# --- PostgreSQL ---
POSTGRES_DB=seeddb
POSTGRES_USER=seed
POSTGRES_PASSWORD=UnaPasswordMoltoForte123!

# --- ASP.NET Core API ---
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=postgres;Database=seeddb;Username=seed;Password=UnaPasswordMoltoForte123!
JwtSettings__Secret=UnaSuperSecretKeyRandomDiAlmeno32Caratteri!abc123
AllowedHosts=tuodominio.com

# --- VPS Deployment ---
DOMAIN_NAME=tuodominio.com
GHCR_OWNER=tuo-github-username
IMAGE_TAG=latest
CERTBOT_EMAIL=tua-email@example.com
```

> **IMPORTANTE**: usa password forti e uniche. Non committare mai il file `.env` su git.

Per generare una password sicura:

```bash
openssl rand -base64 32
```

---

## 7. Configurazione Cloudflare + DNS

### 7.1 Creare un account Cloudflare

1. Vai su https://dash.cloudflare.com e crea un account gratuito
2. Clicca **Add a site** e inserisci il tuo dominio
3. Seleziona il piano **Free**
4. Cloudflare importera automaticamente i record DNS esistenti

### 7.2 Configurare i record DNS

Nel pannello Cloudflare > **DNS** > **Records**, crea:

| Tipo | Nome | Valore | Proxy | TTL |
|------|------|--------|-------|-----|
| A | @ | TUO_IP_VPS | Proxied (nuvola arancione) | Auto |
| A | www | TUO_IP_VPS | Proxied (nuvola arancione) | Auto |

> **Proxied (nuvola arancione)** significa che il traffico passa attraverso Cloudflare (CDN, DDoS protection, caching). Se la nuvola e grigia, il traffico va diretto al server.

### 7.3 Cambiare i nameserver

> **Se hai registrato il dominio direttamente con Cloudflare, puoi saltare questo passaggio.** I nameserver sono già configurati automaticamente.

Se invece hai registrato il dominio con un registrar esterno, Cloudflare ti dara 2 nameserver (es. `anna.ns.cloudflare.com`, `bob.ns.cloudflare.com`). Vai nel pannello del tuo registrar di dominio e sostituisci i nameserver con quelli di Cloudflare.

> La propagazione dei nameserver puo richiedere fino a 24 ore, ma di solito e molto piu veloce.

### 7.4 Configurare SSL/TLS su Cloudflare

Nel pannello Cloudflare > **SSL/TLS** > **Overview**:

- Imposta la modalita su **Full (Strict)**

Questo significa:
- Utente <-> Cloudflare: HTTPS (certificato Cloudflare, automatico)
- Cloudflare <-> Tuo VPS: HTTPS (certificato Let's Encrypt, configurato al punto 8)

### 7.5 Impostazioni consigliate

In **SSL/TLS > Edge Certificates**:
- **Always Use HTTPS**: ON
- **Minimum TLS Version**: TLS 1.2

In **Speed > Optimization > Content Optimization**:
- **Auto Minify**: attiva JS, CSS, HTML
- **Brotli**: ON

In **Caching > Configuration**:
- **Browser Cache TTL**: rispetta gli header esistenti
- **Caching Level**: Standard

In **Security > Settings**:
- **Security Level**: Medium
- **Challenge Passage**: 30 minuti

### 7.6 Vantaggi ottenuti (gratis)

- **CDN globale**: asset statici serviti dal nodo Cloudflare piu vicino all'utente
- **Protezione DDoS**: filtra attacchi prima che raggiungano il tuo server
- **SSL edge**: certificato HTTPS gestito da Cloudflare lato utente
- **Caching**: riduce il carico sul tuo VPS
- **Analytics**: dashboard gratuita sul traffico
- **IP nascosto**: l'IP reale del VPS non e esposto pubblicamente

### 7.7 Verifica

Dopo la propagazione DNS:

```bash
# Deve restituire un IP di Cloudflare, NON il tuo VPS
dig tuodominio.com +short

# Per verificare che Cloudflare sia attivo, controlla gli header
curl -I https://tuodominio.com 2>/dev/null | grep -i cf-ray
# Se vedi "cf-ray: ..." allora Cloudflare e attivo
```

---

## 8. Certificato SSL Iniziale

Prima di avviare lo stack completo, dobbiamo ottenere il certificato SSL per il tratto Cloudflare <-> VPS (modalita Full Strict).

> **Nota con Cloudflare**: dato che Cloudflare proxifica il traffico HTTP, per la prima volta dobbiamo temporaneamente disabilitare il proxy (nuvola grigia) nel pannello DNS di Cloudflare, ottenere il certificato, e poi riattivare il proxy.

### 8.1 Disabilita temporaneamente il proxy Cloudflare

Nel pannello Cloudflare > **DNS** > **Records**:
- Clicca sul record A del tuo dominio
- Cambia da **Proxied** (nuvola arancione) a **DNS only** (nuvola grigia)
- Salva e attendi 1-2 minuti

### 8.2 Ottieni il certificato

```bash
# Installa certbot
sudo apt install -y certbot

# Ottieni il certificato (la porta 80 deve essere libera)
sudo certbot certonly --standalone \
  -d tuodominio.com \
  --email tua-email@example.com \
  --agree-tos \
  --no-eff-email
```

### 8.3 Copia i certificati nel volume Docker

```bash
# Crea il volume e copia i certificati
docker volume create seed-app-deploy_certbot_conf

# Copia i certificati nel volume Docker
sudo cp -rL /etc/letsencrypt/live /etc/letsencrypt/archive /etc/letsencrypt/renewal \
  /var/lib/docker/volumes/seed-app-deploy_certbot_conf/_data/ 2>/dev/null || true

# Metodo alternativo se il precedente non funziona: usa un container temporaneo
sudo docker run --rm \
  -v seed-app-deploy_certbot_conf:/etc/letsencrypt \
  -v /etc/letsencrypt:/source:ro \
  alpine sh -c "cp -a /source/* /etc/letsencrypt/"
```

> **Nota**: il nome del volume Docker include il nome del progetto compose (`seed-app-deploy_certbot_conf`).

### 8.4 Riattiva il proxy Cloudflare

Nel pannello Cloudflare > **DNS** > **Records**:
- Riporta il record A a **Proxied** (nuvola arancione)
- Salva

---

## 9. Primo Deploy Manuale

### 9.1 Login a GitHub Container Registry

Crea un Personal Access Token (PAT) su GitHub:
1. Vai su GitHub > Settings > Developer settings > Personal access tokens > Tokens (classic)
2. Crea un nuovo token con lo scope `read:packages`
3. Copia il token

```bash
echo "IL_TUO_GITHUB_PAT" | docker login ghcr.io -u TUO_GITHUB_USERNAME --password-stdin
```

### 9.2 Avvia lo stack

```bash
cd /opt/seed-app    # o /opt/seed-app/docker se hai clonato il repo

# Pull delle immagini
docker compose -f docker-compose.deploy.yml pull

# Avvia tutti i servizi
docker compose -f docker-compose.deploy.yml up -d
```

### 9.3 Verifica

```bash
# Controlla che tutti i container siano running
docker compose -f docker-compose.deploy.yml ps

# Controlla i log (Ctrl+C per uscire)
docker compose -f docker-compose.deploy.yml logs -f

# Testa l'applicazione
curl https://tuodominio.com/health/ready
curl https://tuodominio.com
```

Se qualcosa non funziona, controlla i log del singolo servizio:

```bash
docker compose -f docker-compose.deploy.yml logs nginx
docker compose -f docker-compose.deploy.yml logs api
docker compose -f docker-compose.deploy.yml logs web
```

---

## 10. Configurazione GitHub Actions Secrets

Per abilitare il deploy automatico dal CI/CD:

1. Vai nel tuo repository GitHub > **Settings** > **Secrets and variables** > **Actions**
2. Aggiungi questi **Repository secrets**:

| Secret | Valore | Descrizione |
|--------|--------|-------------|
| `DEPLOY_HOST` | IP del tuo VPS | Indirizzo del server |
| `DEPLOY_USER` | `deploy` | Utente SSH |
| `DEPLOY_SSH_KEY` | Contenuto di `~/.ssh/id_ed25519` | Chiave privata SSH |
| `GHCR_TOKEN` | Il PAT creato al punto 9.1 | Token per pull immagini |

Per ottenere la chiave privata SSH (dal tuo PC locale):

```bash
cat ~/.ssh/id_ed25519
```

> Copia **tutto** il contenuto, incluse le righe `-----BEGIN` e `-----END`.

### Ambienti (opzionale ma consigliato)

In **Settings > Environments**, configura:
- **staging**: nessuna approvazione richiesta (auto-deploy su push a `dev`)
- **production**: aggiungi reviewer richiesti (deploy su push a `master`)

Se usi VPS separati per staging e production, configura secrets diversi per ogni environment.

---

## 11. Rinnovo Automatico dei Certificati SSL

Il container `certbot` nel compose file controlla il rinnovo ogni 12 ore. Con Cloudflare in modalita proxy, il rinnovo HTTP-01 potrebbe non funzionare direttamente perche Cloudflare intercetta le richieste sulla porta 80.

### Opzione consigliata: Cloudflare Origin Certificate (alternativa semplice)

Se non vuoi gestire il rinnovo Let's Encrypt con Cloudflare, puoi usare un **Origin Certificate** di Cloudflare (valido 15 anni, gratis):

1. Nel pannello Cloudflare > **SSL/TLS** > **Origin Server** > **Create Certificate**
2. Lascia le impostazioni di default e clicca **Create**
3. Copia il certificato e la chiave privata
4. Sul server, salvali nel volume Docker:

```bash
# Crea la directory per i certificati
sudo mkdir -p /var/lib/docker/volumes/seed-app-deploy_certbot_conf/_data/live/tuodominio.com/

# Salva il certificato
sudo nano /var/lib/docker/volumes/seed-app-deploy_certbot_conf/_data/live/tuodominio.com/fullchain.pem
# Incolla il certificato

sudo nano /var/lib/docker/volumes/seed-app-deploy_certbot_conf/_data/live/tuodominio.com/privkey.pem
# Incolla la chiave privata
```

Con questa opzione il certbot container non serve e puoi rimuoverlo dal compose se preferisci. Il certificato dura 15 anni.

### Opzione alternativa: Rinnovo Let's Encrypt con Cloudflare

Se preferisci continuare con Let's Encrypt, configura un cron job che:
1. Mette temporaneamente Cloudflare in "DNS only" via API
2. Rinnova il certificato
3. Riattiva il proxy Cloudflare

Configura un cron job per ricaricare Nginx dopo il rinnovo:

```bash
crontab -e
```

Aggiungi questa riga:

```
0 0 * * * docker exec seed-nginx nginx -s reload 2>/dev/null
```

Per testare il rinnovo manualmente:

```bash
docker compose -f docker-compose.deploy.yml exec certbot certbot renew --dry-run
```

---

## 12. Monitoraggio

### Log dei container

```bash
# Tutti i servizi
docker compose -f docker-compose.deploy.yml logs -f

# Solo un servizio specifico
docker compose -f docker-compose.deploy.yml logs -f api

# Ultime 100 righe
docker compose -f docker-compose.deploy.yml logs --tail 100 api
```

### Seq (dashboard log strutturati)

Seq e accessibile solo da localhost (127.0.0.1:8081) per sicurezza. Accedi tramite SSH tunnel:

```bash
# Dal tuo PC locale
ssh -L 8081:localhost:8081 deploy@TUO_IP_VPS
```

Poi apri nel browser: http://localhost:8081

### Statistiche dei container

```bash
docker stats
```

### Spazio disco

```bash
df -h
docker system df
```

### Pulizia risorse Docker inutilizzate

```bash
docker system prune -f          # Container, network, immagini dangling
docker volume prune -f          # Volumi non utilizzati (ATTENZIONE: puo cancellare dati)
```

---

## 13. Troubleshooting

### Nginx non si avvia

**Causa**: i file del certificato SSL non esistono.

```bash
# Verifica che i certificati esistano
docker compose -f docker-compose.deploy.yml exec nginx ls -la /etc/letsencrypt/live/
```

Se non ci sono, ripeti il punto 8.

### Errore 502 Bad Gateway

**Causa**: l'API o il frontend non sono ancora pronti.

```bash
# Controlla lo stato dei container
docker compose -f docker-compose.deploy.yml ps

# Controlla i log dell'API
docker compose -f docker-compose.deploy.yml logs api
```

### Database connection refused

**Causa**: PostgreSQL non e healthy o la connection string non corrisponde.

```bash
# Verifica che postgres sia healthy
docker compose -f docker-compose.deploy.yml ps postgres

# Testa la connessione
docker compose -f docker-compose.deploy.yml exec postgres pg_isready
```

### Pull delle immagini fallisce

**Causa**: token GHCR scaduto o scope insufficiente.

```bash
# Verifica il login
docker login ghcr.io -u TUO_USERNAME --password-stdin <<< "TUO_PAT"

# Verifica che il PAT abbia lo scope read:packages
```

### Il sito non e raggiungibile

Verifica in ordine:

1. **Cloudflare**: controlla che il dominio sia attivo nel pannello Cloudflare e che il record A punti all'IP corretto
2. **DNS**: `dig tuodominio.com` — con Cloudflare attivo restituira un IP Cloudflare (non il tuo VPS, ed e corretto)
3. **Firewall**: `sudo ufw status` deve mostrare 80 e 443 aperti
4. **Container**: `docker compose -f docker-compose.deploy.yml ps` deve mostrare tutti i servizi "Up"
5. **Nginx**: `docker compose -f docker-compose.deploy.yml logs nginx` per errori
6. **Bypass Cloudflare** (per debug): metti temporaneamente il record A in "DNS only" (nuvola grigia) e prova ad accedere direttamente

### Riavvio completo dello stack

```bash
cd /opt/seed-app
docker compose -f docker-compose.deploy.yml down
docker compose -f docker-compose.deploy.yml up -d
```

> **ATTENZIONE**: `down` ferma i container ma i dati sono preservati nei volumi Docker. Non usare `down -v` altrimenti perderai i dati del database!

---

## Comandi Utili — Cheat Sheet

```bash
# Avvia lo stack
docker compose -f docker-compose.deploy.yml up -d

# Ferma lo stack
docker compose -f docker-compose.deploy.yml down

# Aggiorna le immagini e riavvia
docker compose -f docker-compose.deploy.yml pull api web
docker compose -f docker-compose.deploy.yml up -d --remove-orphans
docker image prune -f

# Log in tempo reale
docker compose -f docker-compose.deploy.yml logs -f

# Stato dei servizi
docker compose -f docker-compose.deploy.yml ps

# Entra in un container (per debug)
docker compose -f docker-compose.deploy.yml exec api sh
docker compose -f docker-compose.deploy.yml exec postgres psql -U seed -d seeddb

# Backup del database
docker compose -f docker-compose.deploy.yml exec postgres pg_dump -U seed seeddb > backup_$(date +%Y%m%d).sql

# Restore del database
cat backup.sql | docker compose -f docker-compose.deploy.yml exec -T postgres psql -U seed seeddb
```
