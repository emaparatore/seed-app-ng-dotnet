# Guida: Deploy di un Nuovo Progetto dalla Seed App

Questa guida spiega come deployare un nuovo progetto creato a partire dalla seed app su un VPS gia configurato.

> **Prerequisito**: il VPS deve essere gia configurato seguendo la [guida VPS](vps-setup-guide.md) (punti 1-4: utente, Docker, firewall). Se e il tuo primo deploy in assoluto, parti da quella guida.

---

## Checklist Rapida

```
[ ] 1. Fork/clone della seed app e rinomina il progetto
[ ] 2. Aggiorna i nomi delle immagini Docker nel CI/CD
[ ] 3. Push su GitHub — le immagini vengono buildate automaticamente
[ ] 4. Crea la directory sul VPS e configura .env
[ ] 5. Aggiungi dominio su Cloudflare
[ ] 6. Crea il certificato SSL (Origin Certificate)
[ ] 7. Primo deploy manuale sul VPS
[ ] 8. Configura i GitHub Secrets per il deploy automatico
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

Le immagini Docker vengono nominate automaticamente dal nome del repository GitHub (`ghcr.io/username/nuovo-progetto/api` e `.../web`), quindi non serve rinominarle manualmente.

---

## 2. Aggiorna il CI/CD (se hai rinominato il repo)

I workflow usano `${{ github.repository }}` per i nomi delle immagini, quindi **si adattano automaticamente** al nuovo repo. Non devi cambiare nulla nei workflow.

L'unica cosa da aggiornare e nel `docker-compose.deploy.yml` la variabile `GHCR_OWNER`:

```yaml
# Questo si adatta automaticamente tramite .env
image: ghcr.io/${GHCR_OWNER}/nuovo-progetto/api:${IMAGE_TAG:-latest}
image: ghcr.io/${GHCR_OWNER}/nuovo-progetto/web:${IMAGE_TAG:-latest}
```

> **Nota**: devi copiare `docker-compose.deploy.yml` nel nuovo progetto e aggiornare i nomi delle immagini da `seed-app-ng-dotnet` al nome del nuovo repo.

---

## 3. Push e Build Automatica

```bash
git push origin master
```

Il workflow `docker-publish.yml` builda e pubblica automaticamente le immagini su GHCR.
Verifica su GitHub > Actions che il build sia andato a buon fine.

---

## 4. Configura il VPS

### Sullo stesso VPS (se ospiti piu app)

```bash
# Crea la directory per la nuova app
sudo mkdir -p /opt/nuovo-progetto
sudo chown deploy:deploy /opt/nuovo-progetto
cd /opt/nuovo-progetto
```

### Copia i file necessari

```bash
# Dal tuo PC locale
scp docker/docker-compose.deploy.yml deploy@TUO_IP_VPS:/opt/nuovo-progetto/
scp -r docker/nginx deploy@TUO_IP_VPS:/opt/nuovo-progetto/
scp docker/.env.prod.example deploy@TUO_IP_VPS:/opt/nuovo-progetto/.env
```

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
IMAGE_TAG=latest
CERTBOT_EMAIL=tua-email@example.com
```

> **SMTP**: Se non configuri `Smtp__Host`, le email vengono loggate in console (utile per debug). Per la guida completa alla configurazione SMTP (Gmail per dev, Brevo per prod, DNS setup) vedi [Configurazione SMTP](smtp-configuration.md).

> **SuperAdmin**: Il file `.env.prod.example` include anche le variabili `SuperAdmin__Email`, `SuperAdmin__Password`, `SuperAdmin__FirstName`, `SuperAdmin__LastName` per creare l'utente admin iniziale durante il bootstrap del deploy. Dopo il primo deploy, rimuovi la password dal file `.env`. Vedi [Admin Dashboard — Configurazione iniziale](admin-dashboard.md#configurazione-iniziale).

### Aggiorna i nomi delle immagini nel compose

```bash
nano docker-compose.deploy.yml
```

Sostituisci `seed-app-ng-dotnet` con `nuovo-progetto` nelle righe delle immagini api e web.

Cambia anche il `name:` del progetto compose (es. `nuovo-progetto-deploy`) per evitare conflitti con altre app sullo stesso VPS.

### Configura i backup per le migrazioni

```bash
sudo mkdir -p /opt/nuovo-progetto/backups
sudo chown deploy:deploy /opt/nuovo-progetto/backups

# Copia gli script di migrazione dal tuo PC locale
scp -r docker/scripts deploy@TUO_IP_VPS:/opt/nuovo-progetto/
```

> Il deploy automatico aggiorna gli script ad ogni rilascio, esegue backup + migrazioni e poi il bootstrap applicativo prima di riavviare l'API. Vedi [Migration Strategy](migration-strategy.md) per dettagli.

---

## 5. Cloudflare — Aggiungi il Nuovo Dominio

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

## 7. Primo Deploy Manuale

```bash
ssh deploy@TUO_IP_VPS
cd /opt/nuovo-progetto

# Login a GHCR
echo "TUO_GITHUB_PAT" | docker login ghcr.io -u TUO_USERNAME --password-stdin

# Pull e avvio
docker compose -f docker-compose.deploy.yml pull
docker compose -f docker-compose.deploy.yml up -d

# Verifica
docker compose -f docker-compose.deploy.yml ps
curl https://nuovodominio.com/health/ready
```

---

## 8. GitHub Secrets per Deploy Automatico

Nel nuovo repository GitHub > **Settings** > **Secrets and variables** > **Actions**:

| Secret | Valore |
|--------|--------|
| `DEPLOY_HOST` | IP del VPS |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_SSH_KEY` | Chiave privata SSH (stessa del VPS) |
| `GHCR_TOKEN` | GitHub PAT con scope `read:packages` |

> Se usi lo stesso VPS per piu app, i secrets `DEPLOY_HOST`, `DEPLOY_USER` e `DEPLOY_SSH_KEY` sono gli stessi. Solo `GHCR_TOKEN` potrebbe variare se usi repo privati diversi.

### Aggiorna il path nel workflow deploy.yml

Nel file `.github/workflows/deploy.yml`, aggiorna il path nel comando SSH:

```yaml
script: |
  cd /opt/nuovo-progetto    # <-- cambia qui
  echo "${{ secrets.GHCR_TOKEN }}" | docker login ghcr.io ...
```

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
| Primo deploy manuale | 5 min |
| GitHub Secrets | 5 min |
| **Totale** | **~35 min** |
