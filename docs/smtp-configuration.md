# Configurazione SMTP

Guida alla configurazione del servizio email per l'invio di email transazionali (reset password, notifiche, ecc.).

---

## Come funziona

Il sistema ha un **auto-switch** basato sulla configurazione:

- Se `Smtp:Host` e' configurato (non vuoto) → usa `SmtpEmailService` (invio reale via MailKit)
- Se `Smtp:Host` e' vuoto o assente → usa `ConsoleEmailService` (logga il token nella console del backend)

Non serve modificare codice: basta compilare la sezione `Smtp` in `appsettings.json` o tramite variabili d'ambiente.

---

## Development locale con Docker: Mailpit

[Mailpit](https://github.com/axllent/mailpit) e' un server SMTP locale che cattura tutte le email in uscita e le mostra in una web UI. E' incluso nel `docker-compose.yml` e funziona automaticamente con `docker compose up`.

**Vantaggi:**
- Nessuna configurazione necessaria — gia' pre-configurato nel compose
- Web UI su `http://localhost:8025` per visualizzare tutte le email (HTML, testo, allegati)
- Le email non vengono mai inviate esternamente
- Non servono account Gmail, app password o credenziali

**Come usarlo:**

1. Avvia lo stack dev: `docker compose up` da `docker/`
2. Apri `http://localhost:8025` nel browser — vedrai la inbox di Mailpit (inizialmente vuota)
3. Quando l'applicazione invia un'email (es. reset password), questa appare automaticamente nella inbox

> **Nota:** Mailpit e' solo per development. In produzione si usa un provider SMTP reale (es. Brevo). Vedi la [sezione Production](#configurazione-per-production-brevo-ex-sendinblue).

---

## Struttura della configurazione

```json
{
  "Smtp": {
    "Host": "",
    "Port": 587,
    "Username": "",
    "Password": "",
    "FromEmail": "noreply@example.com",
    "FromName": "Seed App",
    "UseSsl": true
  }
}
```

| Campo       | Descrizione                                      | Default        |
|-------------|--------------------------------------------------|----------------|
| `Host`      | Hostname del server SMTP                         | `""` (vuoto = console fallback) |
| `Port`      | Porta SMTP                                       | `587`          |
| `Username`  | Username per autenticazione SMTP                 | `""`           |
| `Password`  | Password o App Password per autenticazione SMTP  | `""`           |
| `FromEmail` | Indirizzo mittente                               | `""`           |
| `FromName`  | Nome visualizzato del mittente                   | `"Seed App"`   |
| `UseSsl`    | Usa STARTTLS per la connessione                  | `true`         |

---

## Alternativa: Gmail SMTP (senza Docker)

Per la maggior parte dei casi d'uso in development, [Mailpit](#development-locale-con-docker-mailpit) e' la soluzione consigliata. Gmail SMTP e' un'alternativa utile solo se hai bisogno di inviare email reali o se stai lavorando senza Docker (`dotnet run`).

Gmail richiede una **App Password** (non la password dell'account). La password normale non funziona se hai il 2FA attivo (consigliato).

### 1. Generare una App Password Gmail

1. Vai su https://myaccount.google.com/security
2. Assicurati che la **Verifica in due passaggi** sia attiva
3. Vai su https://myaccount.google.com/apppasswords
4. Seleziona "Altro (nome personalizzato)" → inserisci "Seed App"
5. Copia la password di 16 caratteri generata (es. `abcd efgh ijkl mnop`)

> **Gestione e revoca:** puoi visualizzare e revocare le App Password in qualsiasi momento dalla stessa pagina https://myaccount.google.com/apppasswords. La revoca e' immediata e non impatta la password del tuo account Google.

### 2. Configurare con User Secrets (consigliato per dev)

```bash
cd backend/src/Seed.Api

dotnet user-secrets init   # solo la prima volta
dotnet user-secrets set "Smtp:Host" "smtp.gmail.com"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:Username" "tuaemail@gmail.com"
dotnet user-secrets set "Smtp:Password" "abcd efgh ijkl mnop"
dotnet user-secrets set "Smtp:FromEmail" "tuaemail@gmail.com"
dotnet user-secrets set "Smtp:FromName" "Seed App Dev"
dotnet user-secrets set "Smtp:UseSsl" "true"
```

Per gestire i secrets configurati:

```bash
cd backend/src/Seed.Api

# Visualizzare tutti i secrets configurati
dotnet user-secrets list

# Rimuovere un singolo secret
dotnet user-secrets remove "Smtp:Host"

# Rimuovere tutti i secrets SMTP
dotnet user-secrets remove "Smtp:Host"
dotnet user-secrets remove "Smtp:Port"
dotnet user-secrets remove "Smtp:Username"
dotnet user-secrets remove "Smtp:Password"
dotnet user-secrets remove "Smtp:FromEmail"
dotnet user-secrets remove "Smtp:FromName"
dotnet user-secrets remove "Smtp:UseSsl"

# Oppure rimuovere TUTTI i secrets (attenzione: cancella anche quelli non SMTP)
dotnet user-secrets clear
```

> **Nota:** i User Secrets sono salvati fuori dal progetto (in `%APPDATA%\Microsoft\UserSecrets\` su Windows, `~/.microsoft/usersecrets/` su Linux/macOS), quindi non c'e' rischio di committarli accidentalmente.

### 3. Oppure tramite appsettings.Development.json

Crea o modifica `backend/src/Seed.Api/appsettings.Development.json`:

```json
{
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "tuaemail@gmail.com",
    "Password": "abcd efgh ijkl mnop",
    "FromEmail": "tuaemail@gmail.com",
    "FromName": "Seed App Dev",
    "UseSsl": true
  }
}
```

> **Attenzione:** Non committare mai credenziali reali. Aggiungi `appsettings.Development.json` al `.gitignore` se contiene secret.

### Limiti Gmail SMTP

- **500 email/giorno** per account personali
- **2000 email/giorno** per Google Workspace
- Adatto solo per development e testing, non per produzione

---

## Configurazione per Production: Brevo (ex Sendinblue)

Brevo offre un piano gratuito con 300 email/giorno, sufficiente per molte applicazioni.

### 1. Creare un account Brevo

1. Registrati su https://www.brevo.com
2. Vai su **Settings** → **SMTP & API**
3. Copia le credenziali SMTP:
   - **Server:** `smtp-relay.brevo.com`
   - **Port:** `587`
   - **Login:** la tua email Brevo
   - **Password:** la SMTP key generata (non la password dell'account)

### 2. Configurare il dominio mittente (importante!)

1. In Brevo vai su **Settings** → **Senders, Domains & Dedicated IPs**
2. Aggiungi il tuo dominio (es. `tuodominio.com`)
3. Configura i record DNS richiesti:
   - **SPF** — autorizza Brevo a inviare per il tuo dominio
   - **DKIM** — firma crittografica delle email
   - **DMARC** — policy di autenticazione (consigliato: `v=DMARC1; p=quarantine`)
4. Verifica il dominio nel pannello Brevo
5. Aggiungi il sender email in **Settings** → **Senders** (es. `noreply@tuodominio.com`)

Senza questa configurazione le email finiranno in spam o verranno rifiutate.

### 3. Configurare tramite file `.env` (consigliato per prod)

Le variabili SMTP vanno aggiunte allo stesso file `docker/.env` gia' usato per le altre configurazioni (database, JWT, ecc.). Il file `docker/.env.prod.example` contiene il template completo con tutte le variabili, incluse quelle SMTP.

```env
# Nel file docker/.env
Smtp__Host=smtp-relay.brevo.com
Smtp__Port=587
Smtp__Username=tuaemail@brevo.com
Smtp__Password=xsmtpsib-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Smtp__FromEmail=noreply@tuodominio.com
Smtp__FromName=Seed App
Smtp__UseSsl=true
```

Il `docker-compose.deploy.yml` passa automaticamente queste variabili al container API.

> **Nota:** ASP.NET Core usa `__` (doppio underscore) come separatore per le sezioni nested nelle variabili d'ambiente. Se `Smtp__Host` e' vuoto, il sistema usa automaticamente il fallback console (vedi [Come funziona](#come-funziona)).

### Limiti Brevo

| Piano     | Email/giorno | Email/mese | Costo     |
|-----------|-------------|------------|-----------|
| Free      | 300         | 9.000      | Gratuito  |
| Starter   | Illimitato  | 20.000     | ~25$/mese |

---

## Configurazione Docker

Le variabili SMTP sono gia' configurate in `docker-compose.deploy.yml` per essere lette dal file `.env`. Basta compilare le variabili `Smtp__*` nel file `docker/.env` come descritto nella [sezione Brevo](#3-configurare-tramite-file-env-consigliato-per-prod).

Per il template completo di tutte le variabili d'ambiente (incluse SMTP), vedi `docker/.env.prod.example`.

---

## Verifica della configurazione

### 1. Con Mailpit (Docker dev)

1. Avvia lo stack: `docker compose up` da `docker/`
2. Apri `http://localhost:8025` — la inbox di Mailpit deve essere visibile
3. Vai su `http://localhost:4200/forgot-password` e inserisci l'email di un utente registrato
4. L'email di reset password apparira' nella inbox di Mailpit entro pochi secondi

### 2. Senza SMTP (console fallback)

Se lavori senza Docker (`dotnet run`), `Smtp:Host` e' vuoto e il sistema usa `ConsoleEmailService`. Il token apparira' nei log:

```
[HH:mm:ss WRN] SMTP not configured — logging email to console
[HH:mm:ss INF] Password Reset Email → To: user@example.com, Token: CfDJ8N...
```

> **Nota:** Il token viene generato solo se l'email corrisponde a un utente esistente e attivo. Per motivi di sicurezza (prevenzione email enumeration), l'API restituisce sempre lo stesso messaggio di successo anche se l'utente non esiste.

### 3. Con SMTP reale (Gmail o Brevo)

1. Avvia il backend con la configurazione SMTP
2. Vai su `/forgot-password` nel frontend
3. Inserisci l'email di un utente registrato
4. Controlla la casella email — dovresti ricevere l'email di reset

### Troubleshooting

| Problema                          | Causa probabile                                  | Soluzione                                           |
|-----------------------------------|--------------------------------------------------|-----------------------------------------------------|
| Email non arriva                  | Credenziali SMTP errate                          | Verifica username/password nei log di errore        |
| Email in spam                     | Dominio non verificato (SPF/DKIM)                | Configura i record DNS come descritto sopra         |
| `AuthenticationException`         | Gmail: password account usata invece di App Password | Genera una App Password dedicata                 |
| `Connection refused`              | Host/porta errati o firewall                     | Verifica host, porta, e regole firewall             |
| Nessun errore ma nessuna email    | `Smtp:Host` vuoto → usa ConsoleEmailService      | Controlla i log della console per il token          |

---

## Riepilogo rapido

| Ambiente         | Provider | Host                    | Porta | Metodo configurazione              |
|------------------|----------|-------------------------|-------|------------------------------------|
| Docker dev       | Mailpit  | `mailpit` (interno)     | 1025  | Automatico (`docker-compose.yml`)  |
| Locale no Docker | Nessuno  | *(vuoto)*               | —     | Default (console fallback)         |
| Locale no Docker | Gmail    | `smtp.gmail.com`        | 587   | `dotnet user-secrets` (opzionale)  |
| Production       | Brevo    | `smtp-relay.brevo.com`  | 587   | File `.env` (`.env.prod.example`)  |
