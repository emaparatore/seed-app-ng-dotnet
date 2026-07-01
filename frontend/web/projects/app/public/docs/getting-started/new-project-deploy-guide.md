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
[ ] 2.5 Abilita Workflow Permissions per Actions
[ ] 3. Configura accesso VPS, GitHub Environments e secrets per il deploy
[ ] 4. Crea le directory minime sul VPS
[ ] 5. Configura `.env` per production e, se serve, staging
[ ] 6. Configura Cloudflare DNS, SSL/TLS e staging protetto
[ ] 7. Salva il Cloudflare Origin Certificate nei volumi Docker
[ ] 8. Esegui build immagini e deploy via CI/CD
[ ] 9. Verifica struttura deployata, smoke test e pulizia post-bootstrap
[ ] 9.5 Configura Branch Protection su master e dev
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

## 2.5 Workflow Permissions

Le pipeline usano GitHub Actions con permessi specifici. Configurali prima di qualsiasi deploy.

In **Settings > Actions > General > Workflow permissions**:

1. Imposta **Workflow permissions** a **Read and write permissions** (necessario per `docker-publish.yml` che pubblica immagini su GHCR)
2. Abilita **Allow GitHub Actions to create and approve pull requests** — necessario per il workflow `hotfix-backmerge.yml` che apre automaticamente la PR di back-merge `master` → `dev`

Senza quest'ultima opzione, il back-merge automatico dopo un hotfix fallisce con errore di permessi.

Per il dettaglio dei permessi richiesti da ciascun workflow, vedi [CI/CD Pipeline](../operations/ci-cd.md#github-settings-required).

---

## 3. Prepara Accesso VPS e GitHub Environments/Secrets

Prima di creare `.env` e lanciare il deploy, GitHub Actions deve poter entrare nel VPS via SSH e leggere le immagini private da GHCR.

Questi valori sono specifici del nuovo repository: non vengono ereditati dal seed e non vengono creati automaticamente.

### 3.1 Crea un PAT per GHCR

In GitHub > **Settings > Developer settings > Personal access tokens > Tokens (classic)** crea un token con scope:

```text
read:packages
```

### 3.2 Genera una chiave SSH dedicata al deploy

Dal computer locale:

```bash
ssh-keygen -t ed25519 -f ~/.ssh/gh_deploy_key -C "github-actions-deploy"
```

Non impostare passphrase: la chiave privata sara protetta dai GitHub Secrets e deve essere usabile dalla pipeline non interattiva.

### 3.3 Aggiungi la chiave pubblica al VPS

Aggiungi la chiave pubblica sul server alle authorized keys:

```bash
ssh-copy-id -i ~/.ssh/gh_deploy_key deploy@TUO_IP_VPS
```

Verifica dal computer locale:

```bash
ssh -i ~/.ssh/gh_deploy_key deploy@TUO_IP_VPS
```

### 3.4 Crea gli Environments su GitHub

Il workflow `deploy.yml` usa GitHub Environments per discriminare production e staging dal branch di partenza:

- branch `master` → environment `production`
- branch `dev` → environment `staging`

In **Settings > Environments** crea due environments:

| Environment | Deploy da | Reviewer | Note |
|-------------|-----------|----------|------|
| `production` | `master` | Consigliato (required reviewers) | Blocca il deploy finche un umano non approva |
| `staging` | `dev` | Nessuno | Auto-deploy al merge su `dev` |

Per `production`, in **Required reviewers** aggiungi almeno il tuo utente o il team. Puoi anche impostare un **Wait timer** (es. 2 minuti) per dare tempo di annullare un deploy errato.

### 3.5 Configura i Secrets negli Environments

I secret di deploy devono esistere **in ogni environment** che vuoi deployare, non basta lasciarli a livello di repository. Crea gli stessi 4 secret sia in `production` che in `staging`:

| Secret | Valore |
|--------|--------|
| `DEPLOY_HOST` | IP o hostname del VPS |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_SSH_KEY` | contenuto della chiave privata `~/.ssh/deploy_key` |
| `GHCR_TOKEN` | PAT con scope `read:packages` |

I valori sono identici tra i due environments: entrambi deployano sullo stesso VPS, e la directory di destinazione (`production/` o `staging/`) viene derivata automaticamente dal branch. Se in futuro deployerai staging e production su VPS diversi, potrai diversificare `DEPLOY_HOST` e `DEPLOY_SSH_KEY` per environment.

`DEPLOY_SSH_KEY` deve contenere la chiave privata completa, non il file `.pub`:

```text
-----BEGIN OPENSSH PRIVATE KEY-----
...
-----END OPENSSH PRIVATE KEY-----
```

Prima di proseguire, verifica che `DEPLOY_HOST`, `DEPLOY_USER` e `DEPLOY_SSH_KEY` siano presenti in entrambi gli environments (o almeno in quello che stai per deployare per primo).

---

## 4. Prepara le Directory Minime sul VPS

Prima del deploy devi creare solo la directory dell'ambiente e il file `.env`. Il workflow sincronizzera automaticamente compose, nginx, monitoring e script.

Per production:

```bash
mkdir -p /opt/nuovo-progetto/production
```

Se usi staging sullo stesso VPS:

```bash
mkdir -p /opt/nuovo-progetto/staging
```

Non aspettarti ancora `docker-compose.deploy.yml`, `nginx/`, `scripts/` o `monitoring/`: vengono copiati dal workflow durante un deploy riuscito.

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

## 8. Primo Deploy tramite CI/CD

Con `.env`, certificati e secrets pronti, il primo deploy avviene tramite pipeline.

Esegui il primo push solo a questo punto:

```bash
git push origin master
```

Il workflow `docker-publish.yml` builda e pubblica le immagini su GHCR. A build completata, il workflow `deploy.yml` parte automaticamente e deploya sul VPS.

Se vuoi forzare un build manuale:

1. Vai su GitHub > **Actions** > **Docker Publish**
2. Clicca **Run workflow**
3. Seleziona il branch (`master` per production, `dev` per staging)
4. Spunta **Force API image rebuild** e **Force Web image rebuild**
5. Avvia il workflow

Dopo aver avviato la pipeline:

1. Verifica che `docker-publish.yml` pubblichi le immagini
2. Verifica che `deploy.yml` copi i file sul VPS
3. Verifica che migrazioni, seeding e bootstrap completino senza errori
4. Verifica la struttura deployata
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

## 9. Verifica la Struttura Creata dal Deploy

Dopo un deploy riuscito, la struttura attesa e:

```text
/opt/nuovo-progetto/
|-- production/
|   |-- docker-compose.deploy.yml
|   |-- .env
|   |-- nginx/
|   |-- monitoring/
|   `-- scripts/
|-- staging/
|   |-- docker-compose.deploy.yml
|   |-- .env
|   |-- nginx/
|   |-- monitoring/
|   `-- scripts/
`-- backups/
    |-- production/
    `-- staging/
```

Se questa struttura non esiste, il deploy non e arrivato alla fase di sync dei file. Controlla il log del workflow `Deploy` prima di proseguire con gli smoke test.

---

## 9.5 Configura Branch Protection

Una volta che il primo deploy e andato a buon fine, proteggi i branch `master` e `dev` per evitare push diretti e garantire che la CI passi prima di ogni merge.

In **Settings > Branches > Add branch protection rule** crea una regola per entrambi i branch (`master` e `dev`) con:

- **Require a pull request before merging** — 0 approvazioni per solo dev, aumentare per team
- **Require status checks to pass before merging** — aggiungi `ci-success` come required check
- **Require branches to be up to date before merging** 
- **Require conversation resolution before merging**
- **Do not allow bypassing the above settings** — applica le regole anche agli admin
- **Restrict who can push to matching branches** (opzionale, per team)
- **No force pushes** e **No deletions**

Il check `ci-success` e l'unico required status check necessario: e il job gate del workflow `ci.yml` che aggrega il risultato di build, test, docs-sync e migration check. Non aggiungere singoli job (`backend-build-test`, `frontend-build-test`, ecc.) come required, perche vengono skippati se il path filter non rileva cambiamenti e bloccherebbero la PR.

Per il dettaglio completo delle regole e della strategia di branching, vedi [CI/CD Pipeline](../operations/ci-cd.md#branch-protection-rules).

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

### Deploy fallisce con `usage: ssh` e exit code 255

Causa probabile: uno tra `DEPLOY_HOST`, `DEPLOY_USER` o `DEPLOY_SSH_KEY` non e configurato, e vuoto oppure si trova nell'environment GitHub sbagliato.

Il sintomo tipico nel workflow `Deploy` e:

```text
Run install -m 600 /dev/null /tmp/deploy_key
usage: ssh [-46AaCfGgKkMNnqsTtVvXxYy] ...
Error: Process completed with exit code 255.
```

Verifica:

- nel nuovo repository: **Settings > Secrets and variables > Actions > Secrets**
- se usi Environments: **Settings > Environments > production/staging**
- `DEPLOY_HOST` contiene IP o hostname del VPS
- `DEPLOY_USER` contiene `deploy` o l'utente SSH scelto
- `DEPLOY_SSH_KEY` contiene la chiave privata completa, non la `.pub`

Dopo aver corretto i secret, rilancia il workflow. Se il deploy parte da `workflow_run`, puoi rilanciare `Docker Publish` per attivare di nuovo `Deploy`.

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
