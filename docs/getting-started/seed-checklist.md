# Checklist Rapida: Riusa la Seed per un Nuovo Progetto

Usa questa checklist quando trasformi il seed in una nuova applicazione reale.

**Quando usarla:** vuoi il percorso piu rapido per creare una nuova app dal seed.

**Quando non basta:** se hai bisogno di tutti i dettagli di VPS, Cloudflare, SSL o deploy, passa a [New Project Deploy Guide](new-project-deploy-guide.md).

## 1. Crea il nuovo repository

```bash
git clone https://github.com/TUO_USERNAME/seed-app-ng-dotnet.git nuovo-progetto
cd nuovo-progetto
git remote set-url origin https://github.com/TUO_USERNAME/nuovo-progetto.git
```

Verifica:
- il repository GitHub nuovo esiste
- `origin` punta al nuovo repo

## 2. Decidi cosa rinominare subito

Minimo consigliato per partire veloce:
- nome repository GitHub
- dominio applicazione
- `General.AppName`
- branding base frontend
- mittente email (`Smtp__FromName`, `Smtp__FromEmail`)

Puoi rimandare a dopo:
- rename completo dei namespace `Seed.*`
- rename solution/progetti C#
- refactor cosmetici non necessari al primo rilascio

## 3. Configura il deploy

Nel `.env` del VPS imposta almeno:

```env
GHCR_OWNER=tuo-github-username
GHCR_IMAGE_NAME=nuovo-progetto
DOMAIN_NAME=nuovodominio.com
CLIENT_BASE_URL=https://nuovodominio.com
```

Opzionale:
- GitHub Actions variable `DEPLOY_ROOT=/opt/nuovo-progetto` se non vuoi usare il default `/opt/<repository-name>`

Verifica:
- la directory root del deploy esiste sul VPS
- `.env` e stato creato per l'ambiente giusto

## 4. Configura i segreti GitHub

Repository secrets richiesti:
- `DEPLOY_HOST`
- `DEPLOY_USER`
- `DEPLOY_SSH_KEY`
- `GHCR_TOKEN`

Se usi ambienti GitHub:
- `staging`
- `production`

## 5. Esegui il primo push

```bash
git push origin master
```

Verifica:
- `docker-publish.yml` completa il build immagini
- `deploy.yml` parte correttamente

## 6. Smoke test dopo il deploy

Controlla almeno:
- `https://tuodominio.com/health/ready`
- login pagina web
- registrazione o flusso auth base
- accesso admin iniziale
- log applicativi / errori evidenti

Se i pagamenti sono disattivi:
- `GET /api/v1.0/config` restituisce `paymentsEnabled: false`

## 7. Pulizia post-primo deploy

Dopo il bootstrap iniziale:
- rimuovi la password `SuperAdmin__Password` dal `.env`
- aggiorna `General.AppName` se non l'hai gia fatto
- aggiorna logo, favicon e testi demo residui
- verifica SMTP reale o fallback console

## 8. Primo criterio di successo del seed

Considera il seed validato se riesci a fare senza attrito anomalo:
- clone/template del repo
- configurazione `.env` e secrets
- primo deploy riuscito
- smoke test base passato
- avvio della prima feature della tua applicazione

Se durante il processo perdi piu di 5-10 minuti su un passaggio ripetibile, trasformalo in un miglioramento del seed.
