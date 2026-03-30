# Guida: Deploy su VPS con Docker Compose

Questa guida spiega come deployare l'applicazione Seed App su un VPS (Virtual Private Server) usando Docker Compose, Nginx come reverse proxy, Cloudflare come CDN/protezione e SSL con Cloudflare Origin Certificate.

## Architettura

```
Utente --> Cloudflare (CDN, DDoS protection, SSL edge)
              --> VPS (HTTPS/443) --> Nginx --> Angular SSR (frontend)
                                           --> .NET API (backend)
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
sudo ufw allow 8443/tcp   # staging (Cloudflare Origin Rule → porta 8443)
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
8443/tcp                   ALLOW       Anywhere
```

---

## 5. Preparazione della Directory di Deploy

Il deploy usa una struttura a due ambienti separati, entrambi sullo stesso VPS. Il CI/CD crea automaticamente le subdirectory e copia tutti i file (compose, nginx, scripts) ad ogni deploy; qui devi solo creare la directory root e configurare il file `.env`.

```bash
sudo mkdir -p /opt/seed-app
sudo chown deploy:deploy /opt/seed-app
```

La struttura creata dal CI/CD ad ogni deploy sarà:

```
/opt/seed-app/
├── production/
│   ├── docker-compose.deploy.yml   ← copiato dal CI ad ogni deploy
│   ├── .env                         ← creato manualmente (una volta sola)
│   ├── nginx/                       ← copiato dal CI ad ogni deploy
│   └── scripts/                     ← copiato dal CI ad ogni deploy
├── staging/
│   ├── docker-compose.deploy.yml
│   ├── .env
│   ├── nginx/
│   └── scripts/
└── backups/
    ├── production/                  ← backup pre-migrazione production
    └── staging/                     ← backup pre-migrazione staging
```

> **Nota**: non serve creare manualmente le subdirectory ne copiare file nginx o scripts — il CI/CD li crea e sincronizza automaticamente al primo deploy e a ogni deploy successivo.

---

## 6. Configurazione delle Variabili d'Ambiente

Ogni ambiente ha il proprio file `.env`. Crea i file partendo dall'esempio nel repo.

### 6.1 Production

```bash
nano /opt/seed-app/production/.env
```

```env
# --- Stack Configuration ---
COMPOSE_PROJECT_NAME=seed-production
NGINX_HTTP_PORT=80
NGINX_HTTPS_PORT=443
SEQ_PORT=8081

# --- PostgreSQL ---
POSTGRES_DB=seeddb
POSTGRES_USER=seed
POSTGRES_PASSWORD=UnaPasswordMoltoForte123!

# --- ASP.NET Core API ---
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=postgres;Database=seeddb;Username=seed;Password=UnaPasswordMoltoForte123!
JwtSettings__Secret=UnaSuperSecretKeyRandomDiAlmeno32Caratteri!abc123
AllowedHosts=*

# --- VPS Deployment ---
DOMAIN_NAME=tuodominio.com
GHCR_OWNER=tuo-github-username
# Il CI aggiorna automaticamente questi valori con il tag SHA del commit deployato
API_IMAGE_TAG=latest
WEB_IMAGE_TAG=latest
CLIENT_BASE_URL=https://tuodominio.com
```

### 6.2 Staging

```bash
nano /opt/seed-app/staging/.env
```

```env
# --- Stack Configuration ---
COMPOSE_PROJECT_NAME=seed-staging
NGINX_HTTP_PORT=8080
NGINX_HTTPS_PORT=8443
SEQ_PORT=8082

# --- PostgreSQL ---
POSTGRES_DB=seeddb_staging
POSTGRES_USER=seed_staging
POSTGRES_PASSWORD=AltroPasswordForte456!

# --- ASP.NET Core API ---
ASPNETCORE_ENVIRONMENT=Staging
ConnectionStrings__DefaultConnection=Host=postgres;Database=seeddb_staging;Username=seed_staging;Password=AltroPasswordForte456!
JwtSettings__Secret=AltraSecretKeyRandomDiAlmeno32CaratteriAbc456
AllowedHosts=*

# --- VPS Deployment ---
DOMAIN_NAME=staging.tuodominio.com
GHCR_OWNER=tuo-github-username
# Il CI aggiorna automaticamente questi valori con il tag SHA del commit deployato
API_IMAGE_TAG=dev
WEB_IMAGE_TAG=dev
CLIENT_BASE_URL=https://staging.tuodominio.com
```

> **`COMPOSE_PROJECT_NAME`**: differenzia i container tra i due stack. Con `seed-production` i container si chiamano `seed-production-api-1`, con `seed-staging` diventano `seed-staging-api-1`. Senza questa variabile i nomi colliderebbero.

> **Porte staging**: nginx staging ascolta su 8080 (HTTP) e 8443 (HTTPS). Una Cloudflare Origin Rule reindirizza il traffico `staging.tuodominio.com` alla porta 8443 del VPS.

> **Database separato**: ogni stack ha il proprio volume postgres (`seed-production_postgres_data` vs `seed-staging_postgres_data`), quindi i dati sono completamente isolati.

> **Nota su `AllowedHosts`**: il valore `*` e sicuro perche l'API non e esposta direttamente — solo Nginx riceve traffico esterno. L'healthcheck interno usa `localhost`, quindi un valore restrittivo causerebbe il fallimento dell'healthcheck.

> **Email SMTP (opzionale):** le variabili `Smtp__*` configurano l'invio email. Se non le configuri, il sistema logga le email in console. Vedi [Configurazione SMTP](smtp-configuration.md).

> **SuperAdmin (bootstrap iniziale):** aggiungi le variabili `SuperAdmin__*` al `.env` production per creare l'admin iniziale. Dopo il primo deploy, rimuovi `SuperAdmin__Password` per sicurezza. Vedi [Admin Dashboard](admin-dashboard.md#configurazione-iniziale).

> **IMPORTANTE**: usa password forti e uniche per i due ambienti. Non committare mai i file `.env` su git.

Per generare una password sicura:

```bash
# OpenSSL (Linux/macOS/WSL)
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
| A | staging | TUO_IP_VPS | Proxied (nuvola arancione) | Auto |

> **Proxied (nuvola arancione)** significa che il traffico passa attraverso Cloudflare (CDN, DDoS protection, caching). Se la nuvola e grigia, il traffico va diretto al server.

### 7.3b Configurare la Cloudflare Origin Rule per staging

Il container nginx staging ascolta sulla porta 8443, ma Cloudflare riceve il traffico sulla porta 443. Devi configurare una **Origin Rule** per reindirizzare il traffico di `staging.tuodominio.com` alla porta corretta sul VPS.

Nel pannello Cloudflare > **Rules** > **Overview** (le Origin Rules non hanno una voce separata nel menu — si creano dall'Overview):

1. Clicca **Create rule** e seleziona **Origin Rule**
2. **Rule name**: `Staging port redirect`
3. **When incoming requests match**: usa "Custom filter expression":
   ```
   (http.host eq "staging.tuodominio.com")
   ```
4. **Then**: seleziona **Destination Port** → imposta `8443`
5. Clicca **Deploy**

Questo fa sì che Cloudflare si connetta al tuo VPS su porta 8443 quando il dominio e `staging.tuodominio.com`, mentre production continua su 443.

### 7.3 Cambiare i nameserver

> **Se hai registrato il dominio direttamente con Cloudflare, puoi saltare questo passaggio.** I nameserver sono già configurati automaticamente.

Se invece hai registrato il dominio con un registrar esterno, Cloudflare ti dara 2 nameserver (es. `anna.ns.cloudflare.com`, `bob.ns.cloudflare.com`). Vai nel pannello del tuo registrar di dominio e sostituisci i nameserver con quelli di Cloudflare.

> La propagazione dei nameserver puo richiedere fino a 24 ore, ma di solito e molto piu veloce.

### 7.4 Configurare SSL/TLS su Cloudflare

Nel pannello Cloudflare > **SSL/TLS** > **Overview**:

- Imposta la modalita su **Full (Strict)**

Questo significa:
- Utente <-> Cloudflare: HTTPS (certificato Cloudflare, automatico)
- Cloudflare <-> Tuo VPS: HTTPS (Cloudflare Origin Certificate, configurato al punto 8)

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

## 7b. Cloudflare Access — Protezione Staging

Lo staging non deve essere pubblicamente accessibile. Cloudflare Access (piano Free, fino a 50 utenti) permette di proteggere `staging.tuodominio.com` con autenticazione via email/OTP, senza alcuna configurazione lato VPS.

### Configurazione

1. Nel pannello Cloudflare > **Zero Trust** (in alto a sinistra nel menu)
2. Se e la prima volta, crea il tuo account Zero Trust (gratuito)
3. Vai su **Access** > **Applications** > **Add an application**
4. Scegli **Self-hosted**
5. Configura:
   - **Application name**: `Staging`
   - **Session Duration**: lascia il default (24 hours)
   - Clicca **+ Add public hostname** e inserisci `staging.tuodominio.com` (Path lascia vuoto — protegge tutto)
6. Nella sezione **Policies**, aggiungi una policy:
   - **Policy name**: `Team`
   - **Action**: Allow
   - **Include**: Emails → inserisci le email autorizzate (es. la tua email di lavoro)
7. Salva

Da questo momento, chi tenta di accedere a `staging.tuodominio.com` vede una pagina di login Cloudflare Access e deve inserire la propria email. Cloudflare invia un codice OTP, e solo le email nella whitelist vengono autorizzate.

---

## 8. Certificato SSL (Cloudflare Origin Certificate)

Prima di avviare lo stack, dobbiamo configurare il certificato SSL per il tratto Cloudflare <-> VPS (modalita Full Strict). Usiamo un **Cloudflare Origin Certificate**, gratuito e valido 15 anni, senza bisogno di rinnovo.

Il certificato wildcard `*.tuodominio.com` copre sia `tuodominio.com` che `staging.tuodominio.com`, quindi basta un solo certificato per entrambi gli ambienti.

### 8.1 Genera il certificato su Cloudflare

1. Nel pannello Cloudflare > **SSL/TLS** > **Origin Server**
2. Clicca **Create Certificate**
3. Lascia le impostazioni di default (RSA 2048, validita 15 anni, hostname `tuodominio.com` e `*.tuodominio.com`)
4. Clicca **Create**
5. **Copia subito** il certificato (Origin Certificate) e la chiave privata (Private Key) — la chiave privata non sara piu visibile dopo aver chiuso la pagina

### 8.2 Salva i certificati sul server — production

Il nome del volume Docker e derivato da `COMPOSE_PROJECT_NAME` nel `.env`. Con `COMPOSE_PROJECT_NAME=seed-production`, il volume si chiama `seed-production_certbot_conf`.

```bash
# Crea il volume Docker e la directory per i certificati
docker volume create seed-production_certbot_conf
sudo mkdir -p /var/lib/docker/volumes/seed-production_certbot_conf/_data/live/tuodominio.com/

# Salva il certificato (incolla il contenuto di "Origin Certificate")
sudo nano /var/lib/docker/volumes/seed-production_certbot_conf/_data/live/tuodominio.com/fullchain.pem

# Salva la chiave privata (incolla il contenuto di "Private Key")
sudo nano /var/lib/docker/volumes/seed-production_certbot_conf/_data/live/tuodominio.com/privkey.pem
```

### 8.3 Salva i certificati sul server — staging

Lo staging usa lo stesso wildcard certificate. Copia i file nel volume staging:

```bash
docker volume create seed-staging_certbot_conf
sudo mkdir -p /var/lib/docker/volumes/seed-staging_certbot_conf/_data/live/staging.tuodominio.com/

# Copia gli stessi file (o incollali di nuovo)
sudo cp /var/lib/docker/volumes/seed-production_certbot_conf/_data/live/tuodominio.com/fullchain.pem \
  /var/lib/docker/volumes/seed-staging_certbot_conf/_data/live/staging.tuodominio.com/fullchain.pem
sudo cp /var/lib/docker/volumes/seed-production_certbot_conf/_data/live/tuodominio.com/privkey.pem \
  /var/lib/docker/volumes/seed-staging_certbot_conf/_data/live/staging.tuodominio.com/privkey.pem
```

> **Nota**: il percorso `live/<DOMAIN_NAME>/` deve corrispondere alla variabile `DOMAIN_NAME` nel file `.env` di ciascun ambiente.

### 8.4 Verifica

```bash
# Production
sudo ls -la /var/lib/docker/volumes/seed-production_certbot_conf/_data/live/tuodominio.com/

# Staging
sudo ls -la /var/lib/docker/volumes/seed-staging_certbot_conf/_data/live/staging.tuodominio.com/
```

Dovresti vedere `fullchain.pem` e `privkey.pem`, entrambi con dimensione > 0.

---

## 9. Configurazione GitHub Actions (Secrets + Environments)

Prima di effettuare il primo deploy, configura i secrets e gli ambienti su GitHub. Servono una chiave SSH dedicata e un PAT per il pull delle immagini.

### 9.1 Crea un Personal Access Token (PAT)

1. Vai su GitHub > **Settings** > **Developer settings** > **Personal access tokens** > **Tokens (classic)**
2. Crea un nuovo token con lo scope `read:packages`
3. Copia il token — servira sia per il login manuale sul VPS che come GitHub Secret

### 9.2 Login a GHCR sul VPS

```bash
echo "IL_TUO_GITHUB_PAT" | docker login ghcr.io -u TUO_GITHUB_USERNAME --password-stdin
```

### 9.3 Genera una chiave SSH dedicata al deploy

Usa una chiave separata da quella personale, senza passphrase (le CI/CD non possono inserirla interattivamente — la chiave è già protetta dalla cifratura dei GitHub Secrets).

**Dal tuo PC locale** (PowerShell o Git Bash su Windows):

```bash
ssh-keygen -t ed25519 -f ~/.ssh/deploy_key -C "github-actions-deploy"
```

Quando chiede la passphrase, premi **Invio** due volte senza scrivere nulla.

> Su Windows il file viene creato in `C:\Users\TUO_UTENTE\.ssh\deploy_key`.

### 9.4 Aggiungi la chiave pubblica sul VPS

Copia il contenuto della chiave **pubblica** e aggiungilo alle authorized_keys dell'utente `deploy` sul server:

```bash
# Mostra la chiave pubblica (Windows)
type %USERPROFILE%\.ssh\deploy_key.pub

# Oppure su Git Bash / Linux / macOS
cat ~/.ssh/deploy_key.pub
```

Connettiti al VPS e aggiungi la chiave:

```bash
ssh deploy@TUO_IP_VPS
echo "INCOLLA_QUI_LA_CHIAVE_PUBBLICA" >> ~/.ssh/authorized_keys
exit
```

### 9.5 Verifica la connessione

```bash
ssh -i ~/.ssh/deploy_key deploy@TUO_IP_VPS
```

Deve entrare senza chiedere password ne passphrase.

### 9.6 Configura i secrets su GitHub

1. Vai nel tuo repository GitHub > **Settings** > **Secrets and variables** > **Actions**
2. Aggiungi questi **Repository secrets**:

| Secret | Valore | Descrizione |
|--------|--------|-------------|
| `DEPLOY_HOST` | IP del tuo VPS | Indirizzo del server |
| `DEPLOY_USER` | `deploy` | Utente SSH |
| `DEPLOY_SSH_KEY` | Contenuto di `~/.ssh/deploy_key` | Chiave privata SSH (dedicata, senza passphrase) |
| `GHCR_TOKEN` | Il PAT creato al punto 9.1 | Token per pull immagini |

Per ottenere la chiave privata SSH (dal tuo PC locale):

```bash
# Windows
type %USERPROFILE%\.ssh\deploy_key

# Git Bash / Linux / macOS
cat ~/.ssh/deploy_key
```

> Copia **tutto** il contenuto, incluse le righe `-----BEGIN` e `-----END`.

### 9.7 Configura gli ambienti (opzionale ma consigliato)

In **Settings > Environments**, configura:
- **staging**: nessuna approvazione richiesta (auto-deploy su push a `dev`)
- **production**: aggiungi reviewer richiesti (deploy su push a `master`)

Se usi VPS separati per staging e production, configura secrets diversi per ogni environment.

---

## 10. Primo Deploy

Con i secrets configurati al punto 9, il primo deploy avviene tramite CI/CD — non serve avviare lo stack manualmente. Il CI crea le directory, copia i file (compose, nginx, scripts), esegue le migrazioni, il seeding e avvia lo stack.

### 10.1 Pubblica le immagini Docker e triggera il deploy

1. Vai su GitHub → **Actions** → **Docker Publish**
2. Clicca **Run workflow** → seleziona il branch (`master` per production, `dev` per staging)
3. Spunta **Force API image rebuild** e **Force Web image rebuild**
4. Clicca **Run workflow** e attendi che completi con successo

Al termine del Docker Publish, il workflow **Deploy** parte automaticamente: copia i file sul VPS, esegue il pull delle immagini, le migrazioni, il seeding e avvia lo stack.

Puoi verificare che le immagini esistano su: `https://github.com/TUO_USERNAME/TUO_REPO/pkgs/container`

### 10.2 Verifica

```bash
cd /opt/seed-app/production

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

### 10.3 Migrazioni Database

Le migrazioni e il seeding vengono eseguiti automaticamente dal CI/CD ad ogni deploy — non serve copiare script ne eseguirli manualmente. Il CI copia gli script, esegue `migrate.sh` (backup + migrazioni EF Core) e `seed.sh` (bootstrap: ruoli, permessi, impostazioni, SuperAdmin). Se una di queste fasi fallisce, l'API vecchia resta attiva.

I backup pre-migrazione vengono salvati in:
- Production: `/opt/seed-app/backups/production/`
- Staging: `/opt/seed-app/backups/staging/`

I backup sono conservati per 7 giorni. Per dettagli completi, vedi [Migration Strategy](migration-strategy.md).

---

## 11. Certificato SSL — Note

Il Cloudflare Origin Certificate configurato al punto 8 ha una validita di **15 anni** e non richiede rinnovo automatico. Se in futuro dovesse scadere o avessi bisogno di rigenerarlo:

1. Nel pannello Cloudflare > **SSL/TLS** > **Origin Server** > **Create Certificate**
2. Sostituisci i file `fullchain.pem` e `privkey.pem` sul server (vedi punto 8.2)
3. Riavvia Nginx:

```bash
docker compose -f docker-compose.deploy.yml restart nginx
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

Seq e accessibile solo da localhost per sicurezza (production su 8081, staging su 8082). Accedi tramite SSH tunnel:

```bash
# Production (dal tuo PC locale)
ssh -L 8081:localhost:8081 deploy@TUO_IP_VPS

# Staging (dal tuo PC locale)
ssh -L 8082:localhost:8082 deploy@TUO_IP_VPS
```

Poi apri nel browser: http://localhost:8081 (o 8082 per staging)

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

### API esce con codice 139 (Serilog)

**Causa**: la variabile d'ambiente `Serilog__WriteTo__1__Args__serverUrl` crea un sink Serilog senza il campo `Name`, causando un crash all'avvio.

**Soluzione**: assicurati che nel docker-compose.deploy.yml ci sia anche la variabile `Serilog__WriteTo__1__Name=Seq`:

```yaml
environment:
  - Serilog__WriteTo__1__Name=Seq
  - Serilog__WriteTo__1__Args__serverUrl=http://seq:5341
```

### API risulta "unhealthy" (Bad Request - Invalid Hostname)

**Causa**: `AllowedHosts` nel `.env` e impostato solo sul dominio (es. `tuodominio.com`) ma l'healthcheck chiama `localhost:8080`. Kestrel rifiuta la richiesta con HTTP 400.

**Soluzione**: imposta `AllowedHosts=*` nel `.env`. E sicuro perche l'API non e esposta direttamente — Nginx fa da reverse proxy.

```bash
# Verifica dall'interno del container (il nome e generato da COMPOSE_PROJECT_NAME)
# production: seed-production-api-1, staging: seed-staging-api-1
docker exec seed-production-api-1 curl -f http://localhost:8080/health/ready
```

### Angular SSR: "URL with hostname is not allowed"

**Causa**: Angular SSR (Express/Node.js) controlla l'header `Host` della richiesta HTTP contro la lista `allowedHosts` in `angular.json`. Se il dominio non e nella lista, la richiesta viene rifiutata. Il wildcard `"*"` non funziona perche viene interpretato come stringa letterale, non come glob.

**Soluzione**: un middleware Express in `server.ts` normalizza l'header `Host` a `localhost` prima che Angular lo controlli. In questo modo `allowedHosts` in `angular.json` contiene solo `["localhost"]` e il check passa sempre. La validazione reale del dominio e delegata a Nginx tramite `server_name` — le richieste con Host non valido non arrivano mai a Express. Se servisse leggere il dominio originale nel SSR, usare l'header `X-Forwarded-Host` inoltrato da Nginx.

### Nginx non si avvia

**Causa**: i file del certificato SSL non esistono o sono vuoti.

```bash
# Production
sudo ls -la /var/lib/docker/volumes/seed-production_certbot_conf/_data/live/tuodominio.com/

# Staging
sudo ls -la /var/lib/docker/volumes/seed-staging_certbot_conf/_data/live/staging.tuodominio.com/
```

Se mancano o sono vuoti, ripeti il punto 8 (Cloudflare Origin Certificate).

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
# Production
cd /opt/seed-app/production
docker compose -f docker-compose.deploy.yml down
docker compose -f docker-compose.deploy.yml up -d

# Staging
cd /opt/seed-app/staging
docker compose -f docker-compose.deploy.yml down
docker compose -f docker-compose.deploy.yml up -d
```

> **ATTENZIONE**: `down` ferma i container ma i dati sono preservati nei volumi Docker. Non usare `down -v` altrimenti perderai i dati del database!

---

## Comandi Utili — Cheat Sheet

I comandi seguenti vanno eseguiti dalla directory dell'ambiente (`cd /opt/seed-app/production` oppure `cd /opt/seed-app/staging`).

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

# Backup del database (manuale)
docker compose -f docker-compose.deploy.yml exec postgres pg_dump -U seed seeddb > backup_$(date +%Y%m%d).sql

# Restore del database (manuale)
cat backup.sql | docker compose -f docker-compose.deploy.yml exec -T postgres psql -U seed seeddb

# Esegui migrazioni manualmente (backup + migrazione)
BACKUP_DIR=/opt/seed-app/backups/production bash scripts/migrate.sh
# oppure per staging:
BACKUP_DIR=/opt/seed-app/backups/staging bash scripts/migrate.sh

# Esegui bootstrap applicativo manualmente
bash scripts/seed.sh

# Restore da backup pre-migrazione (interattivo)
bash scripts/restore.sh /opt/seed-app/backups/production/seeddb_YYYYMMDD_HHMMSS.sql.gz

# Lista backup disponibili
ls -lh /opt/seed-app/backups/production/
ls -lh /opt/seed-app/backups/staging/
```

---

## Migrazione dalla struttura precedente

Se hai già un VPS con la struttura precedente (repo clonato in `/opt/seed-app/`), esegui questi passi per migrare alla nuova struttura. Il CI/CD si occupa di copiare compose, nginx e scripts — qui devi solo spostare il `.env`, il volume SSL e i backup.

> ⚠️ Production avrà circa 1 minuto di downtime durante il riavvio dello stack.

```bash
# 1. Ferma lo stack corrente
cd /opt/seed-app/docker
docker compose -f docker-compose.deploy.yml down

# 2. Crea la directory production e sposta il .env
mkdir -p /opt/seed-app/production
cp /opt/seed-app/docker/.env /opt/seed-app/production/.env

# 3. Aggiungi le nuove variabili al .env production
echo "COMPOSE_PROJECT_NAME=seed-production" >> /opt/seed-app/production/.env
echo "NGINX_HTTP_PORT=80" >> /opt/seed-app/production/.env
echo "NGINX_HTTPS_PORT=443" >> /opt/seed-app/production/.env
echo "SEQ_PORT=8081" >> /opt/seed-app/production/.env
echo "CLIENT_BASE_URL=https://tuodominio.com" >> /opt/seed-app/production/.env

# 4. Sposta i backup esistenti
mkdir -p /opt/seed-app/backups/{production,staging}
mv /opt/seed-app/backups/*.sql.gz /opt/seed-app/backups/production/ 2>/dev/null || true

# 5. Crea il volume SSL per il nuovo nome (seed-production_certbot_conf)
docker volume create seed-production_certbot_conf
sudo cp -r /var/lib/docker/volumes/seed-app-deploy_certbot_conf/_data/* \
  /var/lib/docker/volumes/seed-production_certbot_conf/_data/

# 6. Apri porta firewall per staging
sudo ufw allow 8443/tcp

# 7. Rimuovi il vecchio repo clonato
rm -rf /opt/seed-app/backend /opt/seed-app/frontend /opt/seed-app/.git /opt/seed-app/.github
rm -rf /opt/seed-app/docs /opt/seed-app/.claude /opt/seed-app/CLAUDE.md /opt/seed-app/README.md
rm -rf /opt/seed-app/.gitignore /opt/seed-app/Seed.slnx /opt/seed-app/docker /opt/seed-app/scripts
```

Dopo i passi manuali:

1. **Triggera il deploy da GitHub Actions**: vai su **Docker Publish** → **Run workflow** su `master` → spunta **Force API** e **Force Web** → il deploy parte automaticamente e copia tutti i file, avvia lo stack e esegue le migrazioni
2. **Verifica**: `curl https://tuodominio.com/health/ready`
3. **Configura staging**: Cloudflare (sezione 7.2, 7.3b e 7b), crea il file `.env` per staging (sezione 6.2), volume SSL staging (sezione 8.3), poi triggera Docker Publish su `dev`
