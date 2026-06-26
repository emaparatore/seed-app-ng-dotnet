# Primo Deploy di un Nuovo Progetto dalla Seed App

Questa guida spiega come trasformare la seed app in un nuovo progetto deployato tramite GitHub Actions, GHCR, Docker Compose, Cloudflare e VPS.

**Quando usarla:** hai gia un VPS preparato e vuoi arrivare al primo deploy funzionante di una nuova app.

**Prerequisito:** il server deve essere gia configurato seguendo [VPS Setup Guide](vps-setup-guide.md): utente `deploy`, SSH, Docker, firewall e directory `/opt/<project-slug>`.

**Cosa contiene questa guida:** repository, variabili di progetto, `.env`, Cloudflare, certificati, GitHub secrets, primo deploy, smoke test e pulizia post-bootstrap.

---

## Checklist Rapida

```text
[ ] 1. Crea il nuovo repository dal seed
[ ] 2. Scegli `PROJECT_SLUG` e dominio
[ ] 3. Crea la root directory sul VPS
[ ] 4. Configura `.env` per production e, se serve, staging
[ ] 5. Configura Cloudflare DNS, SSL/TLS e staging protetto
[ ] 6. Salva il Cloudflare Origin Certificate nei volumi Docker
[ ] 7. Configura GitHub secrets e variables
[ ] 8. Esegui build immagini e deploy via CI/CD
[ ] 9. Esegui smoke test e pulizia post-bootstrap
```

---

## 1. Crea Repository, Clone e Rinomina

Crea prima un nuovo repository vuoto su GitHub, per esempio `TUO_USERNAME/nuovo-progetto`.

Importante: crealo vuoto, senza README, `.gitignore` o license. Il codice verra' caricato dal clone locale con il primo push.

Poi clona il seed e cambia remote:

```bash
git clone https://github.com/TUO_USERNAME/seed-app-ng-dotnet.git nuovo-progetto
cd nuovo-progetto
git remote set-url origin https://github.com/TUO_USERNAME/nuovo-progetto.git
git remote -v
```

La rinomina completa dei namespace `Seed.*`, solution e progetti C# e opzionale per il primo deploy. Per partire velocemente, scegli prima uno slug stabile e aggiorna branding/configurazione visibile.

Minimo consigliato prima del primo deploy:

- nome repository GitHub
- `PROJECT_SLUG`
- dominio applicazione e `CLIENT_BASE_URL`
- `General__AppName`
- branding base frontend, logo e favicon
- mittente email (`Smtp__FromName`, `Smtp__FromEmail`) se usi SMTP reale

Puoi rimandare a dopo:

- rename completo dei namespace `Seed.*`
- rename solution/progetti C#
- refactor cosmetici non necessari al primo rilascio

Esempio:

```text
PROJECT_SLUG=nuovo-progetto
DOMAIN_NAME=nuovodominio.com
```

Il workflow usa `PROJECT_SLUG` per derivare:

- nome immagini GHCR
- path di deploy default `/opt/<project-slug>`
- `GHCR_IMAGE_NAME` scritto automaticamente nel `.env` remoto

Se non imposti nulla, il default del seed e `seed-app`.

---

## 2. Configura GitHub Variables

Nel repository GitHub vai in **Settings > Secrets and variables > Actions > Variables**.

Variabile consigliata:

| Variable | Valore di esempio | Note |
|----------|-------------------|------|
| `PROJECT_SLUG` | `nuovo-progetto` | Slug stabile usato da immagini e deploy |

Variabile opzionale:

| Variable | Valore di esempio | Note |
|----------|-------------------|------|
| `DEPLOY_ROOT` | `/opt/nuovo-progetto` | Usala solo se vuoi un path diverso da `/opt/<PROJECT_SLUG>` |

---

## 3. Push e Build Immagini

Esegui il primo push:

```bash
git push origin master
```

Il workflow `docker-publish.yml` builda e pubblica le immagini su GHCR. Verifica in GitHub > Actions che il build sia andato a buon fine.

Se vuoi forzare un build manuale:

1. Vai su GitHub > **Actions** > **Docker Publish**
2. Clicca **Run workflow**
3. Seleziona il branch (`master` per production, `dev` per staging)
4. Spunta **Force API image rebuild** e **Force Web image rebuild**
5. Avvia il workflow

---

## 4. Prepara la Directory sul VPS

Il CI/CD crea automaticamente le subdirectory `production`, `staging` e sincronizza compose, nginx, monitoring e script ad ogni deploy. Manualmente devi preparare solo la root e i file `.env`.

Struttura attesa dopo il primo deploy:

```text
/opt/nuovo-progetto/
|-- production/
|   |-- docker-compose.deploy.yml
|   |-- .env
|   |-- nginx/
|   `-- scripts/
|-- staging/
|   |-- docker-compose.deploy.yml
|   |-- .env
|   |-- nginx/
|   `-- scripts/
`-- backups/
    |-- production/
    `-- staging/
```

---

## 5. Configura `.env`

Ogni ambiente ha il proprio file `.env`. I valori sotto sono esempi: genera password e secret reali con `openssl rand -base64 32`.

### 5.1 Production

```bash
mkdir -p /opt/nuovo-progetto/production
nano /opt/nuovo-progetto/production/.env
```

```env
# --- Stack Configuration ---
COMPOSE_PROJECT_NAME=nuovo-progetto-production
NGINX_HTTP_PORT=80
NGINX_HTTPS_PORT=443
SEQ_PORT=8081

# --- PostgreSQL ---
POSTGRES_DB=nuovoprogettodb
POSTGRES_USER=nuovoprogetto
POSTGRES_PASSWORD=<password-forte>

# --- ASP.NET Core API ---
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=postgres;Database=nuovoprogettodb;Username=nuovoprogetto;Password=<password-forte>
JwtSettings__Secret=<secret-random-almeno-32-caratteri>
AllowedHosts=*

# --- Application ---
DOMAIN_NAME=nuovodominio.com
CLIENT_BASE_URL=https://nuovodominio.com
General__AppName=Nuovo Progetto

# --- Container Registry ---
GHCR_OWNER=tuo-github-username
# GHCR_IMAGE_NAME viene scritto automaticamente dal workflow in base a PROJECT_SLUG
API_IMAGE_TAG=latest
WEB_IMAGE_TAG=latest

# --- SMTP opzionale ---
# Smtp__Host=smtp-relay.brevo.com
# Smtp__Port=587
# Smtp__Username=tua-email@brevo.com
# Smtp__Password=<smtp-key>
# Smtp__FromEmail=noreply@nuovodominio.com
# Smtp__FromName=Nuovo Progetto
# Smtp__UseSsl=true

# --- SuperAdmin iniziale, solo per primo bootstrap ---
# SuperAdmin__Email=admin@nuovodominio.com
# SuperAdmin__Password=<password-temporanea-forte>
# SuperAdmin__FirstName=Admin
# SuperAdmin__LastName=User
```

### 5.2 Staging

Se usi staging sullo stesso VPS:

```bash
mkdir -p /opt/nuovo-progetto/staging
nano /opt/nuovo-progetto/staging/.env
```

```env
# --- Stack Configuration ---
COMPOSE_PROJECT_NAME=nuovo-progetto-staging
NGINX_HTTP_PORT=8080
NGINX_HTTPS_PORT=8443
SEQ_PORT=8082

# --- PostgreSQL ---
POSTGRES_DB=nuovoprogettodb_staging
POSTGRES_USER=nuovoprogetto_staging
POSTGRES_PASSWORD=<password-forte-diversa>

# --- ASP.NET Core API ---
ASPNETCORE_ENVIRONMENT=Staging
ConnectionStrings__DefaultConnection=Host=postgres;Database=nuovoprogettodb_staging;Username=nuovoprogetto_staging;Password=<password-forte-diversa>
JwtSettings__Secret=<secret-random-diverso>
AllowedHosts=*

# --- Application ---
DOMAIN_NAME=staging.nuovodominio.com
CLIENT_BASE_URL=https://staging.nuovodominio.com
General__AppName=Nuovo Progetto Staging

# --- Container Registry ---
GHCR_OWNER=tuo-github-username
API_IMAGE_TAG=dev
WEB_IMAGE_TAG=dev
```

Note importanti:

- `COMPOSE_PROJECT_NAME` deve essere diverso tra production e staging per evitare collisioni di container, network e volumi.
- `AllowedHosts=*` e intenzionale: l'API non e esposta direttamente, riceve traffico interno da Nginx e healthcheck locali.
- Se `Smtp__Host` non e configurato, le email vengono loggate in console. Vedi [SMTP Configuration](../modules/smtp-configuration.md).
- Dopo il primo bootstrap, rimuovi `SuperAdmin__Password` dal `.env`. Vedi [Admin Dashboard](../modules/admin-dashboard.md#configurazione-iniziale).

---

## 6. Configura Cloudflare

Cloudflare e legato al dominio dell'app, quindi fa parte del deploy del progetto, non del setup generico del VPS.

### 6.1 Aggiungi il dominio

1. Vai su https://dash.cloudflare.com
2. Clicca **Add a site** e inserisci `nuovodominio.com`
3. Seleziona il piano **Free**
4. Se il dominio non e registrato su Cloudflare, cambia i nameserver nel pannello del registrar

### 6.2 Record DNS

In **DNS > Records**, crea:

| Tipo | Nome | Valore | Proxy |
|------|------|--------|-------|
| A | @ | TUO_IP_VPS | Proxied |
| A | www | TUO_IP_VPS | Proxied |
| A | staging | TUO_IP_VPS | Proxied |

Se non usi staging, puoi saltare il record `staging`.

### 6.3 SSL/TLS

In **SSL/TLS > Overview**:

- imposta **Full (Strict)**

In **SSL/TLS > Edge Certificates**:

- abilita **Always Use HTTPS**
- imposta **Minimum TLS Version** almeno a TLS 1.2

In **Speed > Optimization > Content Optimization**:

- abilita **Brotli**
- abilita Auto Minify solo se non crea problemi con asset o debug frontend

### 6.4 Origin Rule per staging

Lo staging ascolta su `8443`, mentre Cloudflare riceve traffico su `443`. Crea una Origin Rule:

1. Vai in **Rules > Overview**
2. Clicca **Create rule** e seleziona **Origin Rule**
3. **Rule name**: `Staging port redirect`
4. Custom filter expression:

```text
(http.host eq "staging.nuovodominio.com")
```

5. **Then**: Destination Port `8443`
6. Clicca **Deploy**

### 6.5 Proteggi staging con Cloudflare Access

Lo staging non dovrebbe essere pubblico.

1. Vai in **Zero Trust > Access > Applications**
2. Clicca **Add an application**
3. Scegli **Self-hosted**
4. Public hostname: `staging.nuovodominio.com`
5. Aggiungi una policy `Allow` con le email autorizzate
6. Salva

Da questo momento Cloudflare mostra login email/OTP prima di permettere l'accesso allo staging.

---

## 7. Certificato SSL Cloudflare Origin

Cloudflare Full Strict richiede un certificato valido tra Cloudflare e VPS. Usa un **Cloudflare Origin Certificate**, gratuito e valido fino a 15 anni.

### 7.1 Genera il certificato

1. Cloudflare > **SSL/TLS > Origin Server**
2. Clicca **Create Certificate**
3. Usa hostname `nuovodominio.com` e `*.nuovodominio.com`
4. Clicca **Create**
5. Copia subito Origin Certificate e Private Key

### 7.2 Salva certificato production

Il nome del volume deriva da `COMPOSE_PROJECT_NAME`. Con `COMPOSE_PROJECT_NAME=nuovo-progetto-production`:

```bash
docker volume create nuovo-progetto-production_certbot_conf
sudo mkdir -p /var/lib/docker/volumes/nuovo-progetto-production_certbot_conf/_data/live/nuovodominio.com/

sudo nano /var/lib/docker/volumes/nuovo-progetto-production_certbot_conf/_data/live/nuovodominio.com/fullchain.pem
sudo nano /var/lib/docker/volumes/nuovo-progetto-production_certbot_conf/_data/live/nuovodominio.com/privkey.pem
```

### 7.3 Salva certificato staging

Lo staging puo usare lo stesso wildcard certificate:

```bash
docker volume create nuovo-progetto-staging_certbot_conf
sudo mkdir -p /var/lib/docker/volumes/nuovo-progetto-staging_certbot_conf/_data/live/staging.nuovodominio.com/

sudo cp /var/lib/docker/volumes/nuovo-progetto-production_certbot_conf/_data/live/nuovodominio.com/fullchain.pem \
  /var/lib/docker/volumes/nuovo-progetto-staging_certbot_conf/_data/live/staging.nuovodominio.com/fullchain.pem
sudo cp /var/lib/docker/volumes/nuovo-progetto-production_certbot_conf/_data/live/nuovodominio.com/privkey.pem \
  /var/lib/docker/volumes/nuovo-progetto-staging_certbot_conf/_data/live/staging.nuovodominio.com/privkey.pem
```

Il percorso `live/<DOMAIN_NAME>/` deve corrispondere alla variabile `DOMAIN_NAME` dell'ambiente.

Verifica:

```bash
sudo ls -la /var/lib/docker/volumes/nuovo-progetto-production_certbot_conf/_data/live/nuovodominio.com/
sudo ls -la /var/lib/docker/volumes/nuovo-progetto-staging_certbot_conf/_data/live/staging.nuovodominio.com/
```

---

## 8. GitHub Secrets per Deploy Automatico

### 8.1 Crea un PAT per GHCR

In GitHub > **Settings > Developer settings > Personal access tokens > Tokens (classic)** crea un token con scope:

```text
read:packages
```

### 8.2 Genera una chiave SSH dedicata al deploy

Dal computer locale:

```bash
ssh-keygen -t ed25519 -f ~/.ssh/deploy_key -C "github-actions-deploy"
```

Non impostare passphrase: la chiave privata sara protetta dai GitHub Secrets e deve essere usabile dalla pipeline non interattiva.

### 8.3 Aggiungi la chiave pubblica al VPS

Mostra la chiave pubblica:

```bash
cat ~/.ssh/deploy_key.pub
```

Sul VPS:

```bash
ssh deploy@TUO_IP_VPS
echo "INCOLLA_QUI_LA_CHIAVE_PUBBLICA" >> ~/.ssh/authorized_keys
exit
```

Verifica:

```bash
ssh -i ~/.ssh/deploy_key deploy@TUO_IP_VPS
```

### 8.4 Configura repository secrets

Nel repository GitHub > **Settings > Secrets and variables > Actions > Secrets**:

| Secret | Valore |
|--------|--------|
| `DEPLOY_HOST` | IP del VPS |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_SSH_KEY` | contenuto della chiave privata `~/.ssh/deploy_key` |
| `GHCR_TOKEN` | PAT con scope `read:packages` |

Se usi GitHub Environments, configura:

- `staging`: deploy automatico da `dev`
- `production`: deploy da `master`, preferibilmente con reviewer richiesto

---

## 9. Primo Deploy tramite CI/CD

Con `.env`, certificati e secrets pronti, il primo deploy avviene tramite pipeline.

Flusso consigliato:

1. Esegui `git push origin master`
2. Verifica che `docker-publish.yml` pubblichi le immagini
3. Verifica che `deploy.yml` copi i file sul VPS
4. Verifica che migrazioni, seeding e bootstrap completino senza errori
5. Esegui gli smoke test

Il deploy automatico esegue:

- sync di compose, nginx, monitoring e scripts
- pull delle immagini GHCR
- backup pre-migrazione
- migrazioni EF Core
- bootstrap applicativo con ruoli, permessi, impostazioni e SuperAdmin
- riavvio stack
- health check

I backup pre-migrazione vengono salvati in:

- production: `/opt/nuovo-progetto/backups/production/`
- staging: `/opt/nuovo-progetto/backups/staging/`

Per dettagli, vedi [Migration Strategy](../architecture/migration-strategy.md).

---

## 10. Smoke Test

Da locale:

```bash
curl https://nuovodominio.com/health/ready
curl https://nuovodominio.com
```

Sul VPS:

```bash
cd /opt/nuovo-progetto/production
docker compose -f docker-compose.deploy.yml ps
docker compose -f docker-compose.deploy.yml logs --tail 100 api
docker compose -f docker-compose.deploy.yml logs --tail 100 web
docker compose -f docker-compose.deploy.yml logs --tail 100 nginx
```

Verifica anche:

- pagina web caricata via HTTPS
- login o registrazione base
- accesso SuperAdmin iniziale, se configurato
- assenza di errori evidenti nei log API e web

---

## 11. Pulizia Post-Primo Deploy

Dopo il bootstrap iniziale:

- rimuovi `SuperAdmin__Password` dal `.env`
- conserva in modo sicuro le credenziali iniziali
- verifica SMTP reale oppure accetta consapevolmente il fallback console
- aggiorna `General__AppName`, branding, logo, favicon e testi demo
- verifica che Cloudflare Access protegga staging
- documenta eventuali valori custom del progetto

Considera il seed validato quando riesci ad arrivare senza attrito anomalo a:

- clone/template del repository
- configurazione `.env` e GitHub secrets/variables
- primo deploy riuscito
- smoke test base passato
- avvio della prima feature della tua applicazione

---

## 12. Comandi Operativi Utili

Esegui questi comandi dalla directory dell'ambiente, per esempio `/opt/nuovo-progetto/production`.

```bash
# Stato servizi
docker compose -f docker-compose.deploy.yml ps

# Log in tempo reale
docker compose -f docker-compose.deploy.yml logs -f

# Log di un servizio
docker compose -f docker-compose.deploy.yml logs -f api

# Riavvio stack
docker compose -f docker-compose.deploy.yml up -d --remove-orphans

# Pull immagini manuale
docker compose -f docker-compose.deploy.yml pull api web

# Migrazioni manuali, se necessario
BACKUP_DIR=/opt/nuovo-progetto/backups/production bash scripts/migrate.sh

# Bootstrap manuale, se necessario
bash scripts/seed.sh

# Lista backup
ls -lh /opt/nuovo-progetto/backups/production/
```

Non usare `docker compose down -v` in ambienti reali: elimina i volumi e quindi puo cancellare database e certificati.

---

## 13. Troubleshooting Primo Deploy

### Nginx non si avvia

Causa probabile: certificati mancanti o vuoti.

Verifica che `fullchain.pem` e `privkey.pem` esistano nel volume Docker e nel path `live/<DOMAIN_NAME>/`.

### API unhealthy con Invalid Hostname

Causa probabile: `AllowedHosts` troppo restrittivo. Imposta:

```env
AllowedHosts=*
```

L'API non e esposta direttamente: Nginx e Docker network limitano il traffico effettivo.

### Pull immagini fallisce

Verifica:

- `GHCR_TOKEN` ha scope `read:packages`
- `GHCR_OWNER` nel `.env` e corretto
- `PROJECT_SLUG` corrisponde al nome immagine atteso
- le immagini sono state pubblicate dal workflow `docker-publish.yml`

### Errore 502 Bad Gateway

Controlla in ordine:

```bash
docker compose -f docker-compose.deploy.yml ps
docker compose -f docker-compose.deploy.yml logs api
docker compose -f docker-compose.deploy.yml logs web
docker compose -f docker-compose.deploy.yml logs nginx
```

### Il dominio non risponde

Verifica:

- record DNS Cloudflare puntano all'IP corretto
- proxy Cloudflare e attivo se vuoi usare Full Strict
- `sudo ufw status` mostra 80, 443 e, per staging, 8443
- il workflow `deploy.yml` ha completato senza errori
- i container sono `Up` sul VPS

Per problemi operativi ricorrenti, vedi [Troubleshooting](../operations/troubleshooting.md).

---

## 14. Piu App sullo Stesso VPS

Ogni app ha il suo Docker Compose e la sua rete interna. Se piu app provano ad ascoltare direttamente su 80/443, avrai conflitti.

Soluzioni:

- usa un VPS per app, semplice e isolato
- usa porte diverse e Cloudflare Origin Rules, accettabile per pochi ambienti
- usa un reverse proxy globale condiviso, consigliato quando hai piu app stabili sullo stesso VPS

Esempio reverse proxy globale:

```text
/opt/nginx-proxy/
  docker-compose.yml
  nginx/
    conf.d/
      app1.conf
      app2.conf
```

In questo caso le singole app non espongono direttamente 80/443: espongono solo servizi interni su una rete Docker condivisa. Questa evoluzione richiede modifiche al compose e va trattata come scelta infrastrutturale separata.
