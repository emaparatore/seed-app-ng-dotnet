# Guida: Deploy di un Nuovo Progetto dalla Seed App

Questa guida spiega come deployare un nuovo progetto creato a partire dalla seed app su un VPS gia configurato.

**Quando usarla:** vuoi il flusso completo per trasformare il seed in una nuova applicazione deployata.

**Quando non serve partire da qui:** se vuoi solo una checklist corta, usa [Seed Checklist](seed-checklist.md). Se devi ancora preparare il server da zero, usa prima [VPS Setup Guide](vps-setup-guide.md).

> **Prerequisito**: il VPS deve essere gia configurato seguendo la [guida VPS](vps-setup-guide.md) (punti 1-4: utente, Docker, firewall). Se e il tuo primo deploy in assoluto, parti da quella guida.

---

## Checklist Rapida

```
[ ] 1. Fork/clone della seed app e rinomina il progetto
[ ] 2. Configura `PROJECT_SLUG`, `GHCR_OWNER` e, se serve, `DEPLOY_ROOT`
[ ] 3. Push su GitHub — le immagini vengono buildate automaticamente
[ ] 4. Crea la directory sul VPS e configura .env
[ ] 5. Aggiungi dominio su Cloudflare
[ ] 6. Crea il certificato SSL (Origin Certificate)
[ ] 7. Configura i GitHub Secrets per il deploy automatico
[ ] 8. Primo deploy tramite CI/CD e smoke test
```

---

## 1. Fork e Rinomina

```bash
# Crea un nuovo repo dalla seed app (o usa "Use this template" su GitHub)
git clone https://github.com/TUO_USERNAME/seed-app-ng-dotnet.git nuovo-progetto
cd nuovo-progetto
git remote set-url origin https://github.com/TUO_USERNAME/nuovo-progetto.git
```

### Rinomina i riferimenti

Nel backend, rinomina il namespace `Seed` se vuoi (opzionale, puoi farlo dopo):

- Solution, progetti, namespace C#
- Dockerfile references

La convenzione consigliata e scegliere uno slug stabile del progetto. Il workflow usa la GitHub Actions variable `PROJECT_SLUG` per derivare sia il nome immagini sia il path di deploy. Se non vuoi personalizzare nulla, lascia il default del seed: `PROJECT_SLUG=seed-app`.

---

## 2. Aggiorna il CI/CD (se hai rinominato il repo)

I workflow di build e deploy usano lo stesso `PROJECT_SLUG`, quindi il nome delle immagini e il path VPS restano coerenti e non dipendono dal nome del repository GitHub.

Per il deploy su VPS:

- `PROJECT_SLUG` e opzionale: se non lo imposti, il seed usa il default `seed-app`
- `GHCR_OWNER` conviene impostarlo nel `.env` del server; se manca, il workflow usa il repository owner GitHub come fallback
- `GHCR_IMAGE_NAME` viene scritto automaticamente dal workflow a partire da `PROJECT_SLUG`, quindi non devi ricavarlo dal nome repo ne gestirlo a mano nel file di esempio
- il path di deploy di default diventa automaticamente `/opt/<project-slug>`
- se vuoi un path custom, imposta la variabile GitHub Actions `DEPLOY_ROOT` (es. `/opt/piattaforma`)

---

## 3. Push e Build Automatica

```bash
git push origin master
```

Il workflow `docker-publish.yml` builda e pubblica automaticamente le immagini su GHCR.
Verifica su GitHub > Actions che il build sia andato a buon fine.

---

## 4. Configura il VPS

### Directory di deploy

Esempio con `PROJECT_SLUG=nuovo-progetto` e senza `DEPLOY_ROOT` custom:

```bash
# Crea la directory per la nuova app
sudo mkdir -p /opt/nuovo-progetto
sudo chown deploy:deploy /opt/nuovo-progetto
cd /opt/nuovo-progetto
```

> Il CI/CD copia automaticamente compose, nginx, monitoring e script di deploy. L'unica cosa da creare manualmente e la root directory sul VPS, insieme al file `.env`.

### Configura le variabili d'ambiente

```bash
ssh deploy@TUO_IP_VPS
cd /opt/nuovo-progetto
nano .env
```

Compila con i valori del nuovo progetto:

```env
# --- PostgreSQL ---
POSTGRES_DB=nuovoprogettoDb
POSTGRES_USER=nuovoprogetto
POSTGRES_PASSWORD=<generata con: openssl rand -base64 32>

# --- ASP.NET Core API ---
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=postgres;Database=nuovoprogettoDb;Username=nuovoprogetto;Password=<stessa-password-sopra>
JwtSettings__Secret=<generata con: openssl rand -base64 32>
AllowedHosts=nuovodominio.com

# --- SMTP (opzionale, per invio email) ---
Smtp__Host=smtp-relay.brevo.com
Smtp__Port=587
Smtp__Username=tua-email@brevo.com
Smtp__Password=<SMTP key da Brevo>
Smtp__FromEmail=noreply@nuovodominio.com
Smtp__FromName=Nuovo Progetto
Smtp__UseSsl=true

# --- VPS Deployment ---
DOMAIN_NAME=nuovodominio.com
GHCR_OWNER=tuo-github-username
# Il CI aggiorna automaticamente questi valori con il tag SHA del commit deployato
API_IMAGE_TAG=latest
WEB_IMAGE_TAG=latest
CERTBOT_EMAIL=tua-email@example.com
```

> **SMTP**: Se non configuri `Smtp__Host`, le email vengono loggate in console (utile per debug). Per la guida completa alla configurazione SMTP (Gmail per dev, Brevo per prod, DNS setup) vedi [Configurazione SMTP](../modules/smtp-configuration.md).

> **SuperAdmin**: Il file `.env.prod.example` include anche le variabili `SuperAdmin__Email`, `SuperAdmin__Password`, `SuperAdmin__FirstName`, `SuperAdmin__LastName` per creare l'utente admin iniziale durante il bootstrap del deploy. Dopo il primo deploy, rimuovi la password dal file `.env`. Vedi [Admin Dashboard — Configurazione iniziale](../modules/admin-dashboard.md#configurazione-iniziale).

### Note sul compose

Non serve piu modificare manualmente i nomi delle immagini nel `docker-compose.deploy.yml`: vengono risolti tramite `GHCR_OWNER` + `GHCR_IMAGE_NAME` dal file `.env`. Il workflow scrive e aggiorna automaticamente `GHCR_IMAGE_NAME` in base a `PROJECT_SLUG`.

Puoi comunque cambiare `COMPOSE_PROJECT_NAME` nel `.env` se vuoi nomi container/volumi diversi da quelli di default.

### Configura i backup per le migrazioni

```bash
sudo mkdir -p /opt/nuovo-progetto/backups
sudo chown deploy:deploy /opt/nuovo-progetto/backups
```

> Il deploy automatico aggiorna gli script ad ogni rilascio, esegue backup + migrazioni e poi il bootstrap applicativo prima di riavviare l'API. Vedi [Migration Strategy](../architecture/migration-strategy.md) per dettagli.

---

## 5. Cloudflare — Aggiungi il Nuovo Dominio

Per il dettaglio completo della configurazione Cloudflare, SSL/TLS e staging protetto, usa sempre [VPS Setup Guide](vps-setup-guide.md). Qui trovi solo il minimo necessario per un progetto derivato dal seed.

### Se usi lo stesso account Cloudflare

1. Pannello Cloudflare > **Add a site** > inserisci `nuovodominio.com`
2. Seleziona piano **Free**
3. Aggiungi record DNS:

| Tipo | Nome | Valore | Proxy |
|------|------|--------|-------|
| A | @ | TUO_IP_VPS | Proxied |
| A | www | TUO_IP_VPS | Proxied |

4. Cambia i nameserver del dominio nel pannello del registrar
5. Configura SSL/TLS > **Full (Strict)**
6. Attiva: Always Use HTTPS, Brotli, Auto Minify

---

## 6. Certificato SSL

### Opzione consigliata: Cloudflare Origin Certificate

1. Pannello Cloudflare > **SSL/TLS** > **Origin Server** > **Create Certificate**
2. Lascia le impostazioni di default > **Create**
3. Copia certificato e chiave privata
4. Sul server:

```bash
# Crea il volume Docker per i certificati
docker volume create nuovo-progetto-deploy_certbot_conf

# Crea la directory
sudo mkdir -p /var/lib/docker/volumes/nuovo-progetto-deploy_certbot_conf/_data/live/nuovodominio.com/

# Salva il certificato
sudo nano /var/lib/docker/volumes/nuovo-progetto-deploy_certbot_conf/_data/live/nuovodominio.com/fullchain.pem
# Incolla il certificato

sudo nano /var/lib/docker/volumes/nuovo-progetto-deploy_certbot_conf/_data/live/nuovodominio.com/privkey.pem
# Incolla la chiave privata
```

> Il nome del volume deve corrispondere a `<nome-progetto-compose>_certbot_conf`.

---

## 7. GitHub Secrets per Deploy Automatico

Nel nuovo repository GitHub > **Settings** > **Secrets and variables** > **Actions**:

| Secret | Valore |
|--------|--------|
| `DEPLOY_HOST` | IP del VPS |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_SSH_KEY` | Chiave privata SSH (stessa del VPS) |
| `GHCR_TOKEN` | GitHub PAT con scope `read:packages` |

> Se usi lo stesso VPS per piu app, i secrets `DEPLOY_HOST`, `DEPLOY_USER` e `DEPLOY_SSH_KEY` sono gli stessi. Solo `GHCR_TOKEN` potrebbe variare se usi repo privati diversi.

### Path di deploy custom

Per default il workflow deploya in `/opt/<project-slug>`. Se non configuri nulla, il seed usa `PROJECT_SLUG=seed-app` e quindi `/opt/seed-app`.

Se vuoi usare un path custom, aggiungi una repository variable GitHub Actions:

| Variable | Valore di esempio |
|----------|-------------------|
| `PROJECT_SLUG` | `nuovo-progetto` |
| `DEPLOY_ROOT` | `/opt/nuovo-progetto` |

---

## 8. Primo Deploy tramite CI/CD

Con secrets e `.env` gia configurati, il primo deploy puo avvenire direttamente tramite pipeline dopo il push su `master`.

### Flusso consigliato

1. Esegui `git push origin master`
2. Verifica che `docker-publish.yml` pubblichi le immagini
3. Verifica che `deploy.yml` completi sync file, migrazioni, seeding e avvio stack
4. Esegui lo smoke test finale

### Smoke test minimo

```bash
curl https://nuovodominio.com/health/ready
```

### Deploy manuale straordinario

Se ti serve fare un avvio manuale di emergenza prima di avere la pipeline pronta, usa il `docker-compose.deploy.yml` e gli stessi valori del file `.env`. Non e il flusso principale raccomandato per i progetti derivati dal seed.

---

## Piu App sullo Stesso VPS — Considerazioni

### Porte e conflitti

Ogni app ha il suo Docker Compose con la propria rete interna. Nginx di ogni app ascolta sulle porte 80/443 — questo crea conflitto se hai piu app.

**Soluzione**: usa un unico Nginx condiviso come reverse proxy globale:

```
/opt/nginx-proxy/
  docker-compose.yml      # Nginx + certbot condiviso
  nginx/
    conf.d/
      app1.conf           # server_name app1.com -> http://app1-web:80
      app2.conf           # server_name app2.com -> http://app2-web:80
```

In questo caso, rimuovi il servizio `nginx` e `certbot` dal compose di ogni singola app e esponi `web` e `api` solo sulla rete Docker condivisa.

Questa evoluzione e consigliata quando hai 2+ app sullo stesso VPS.

### Database

Ogni app ha il suo container PostgreSQL isolato. Questo e semplice e sicuro. Se vuoi risparmiare RAM, puoi usare un singolo PostgreSQL con database separati, ma aggiunge complessita.

---

## Riepilogo Tempi

| Operazione | Tempo stimato |
|------------|--------------|
| Fork + push | 5 min |
| Configurazione VPS (.env, compose) | 10 min |
| Cloudflare (dominio + SSL) | 10 min |
| GitHub Secrets | 5 min |
| Primo deploy via CI/CD | 5 min |
| **Totale** | **~35 min** |
