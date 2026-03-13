# Configurazione SMTP

Guida alla configurazione del servizio email per l'invio di email transazionali (reset password, notifiche, ecc.).

---

## Come funziona

Il sistema ha un **auto-switch** basato sulla configurazione:

- Se `Smtp:Host` e' configurato (non vuoto) → usa `SmtpEmailService` (invio reale via MailKit)
- Se `Smtp:Host` e' vuoto o assente → usa `ConsoleEmailService` (logga il token nella console del backend)

Non serve modificare codice: basta compilare la sezione `Smtp` in `appsettings.json` o tramite variabili d'ambiente.

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

## Configurazione per Development: Gmail SMTP

Gmail richiede una **App Password** (non la password dell'account). La password normale non funziona se hai il 2FA attivo (consigliato).

### 1. Generare una App Password Gmail

1. Vai su https://myaccount.google.com/security
2. Assicurati che la **Verifica in due passaggi** sia attiva
3. Vai su https://myaccount.google.com/apppasswords
4. Seleziona "Altro (nome personalizzato)" → inserisci "Seed App"
5. Copia la password di 16 caratteri generata (es. `abcd efgh ijkl mnop`)

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

### 3. Configurare tramite variabili d'ambiente (consigliato per prod)

```bash
# Variabili d'ambiente sul server/container
Smtp__Host=smtp-relay.brevo.com
Smtp__Port=587
Smtp__Username=tuaemail@brevo.com
Smtp__Password=xsmtpsib-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Smtp__FromEmail=noreply@tuodominio.com
Smtp__FromName=Seed App
Smtp__UseSsl=true
```

> **Nota:** ASP.NET Core usa `__` (doppio underscore) come separatore per le sezioni nested nelle variabili d'ambiente.

### 4. Oppure tramite appsettings.Production.json

```json
{
  "Smtp": {
    "Host": "smtp-relay.brevo.com",
    "Port": 587,
    "Username": "tuaemail@brevo.com",
    "Password": "USARE_VARIABILE_AMBIENTE_NON_COMMITTARE",
    "FromEmail": "noreply@tuodominio.com",
    "FromName": "Seed App",
    "UseSsl": true
  }
}
```

> **Best practice:** Non mettere la password nel file. Usa variabili d'ambiente o un secret manager per `Smtp__Password`.

### Limiti Brevo

| Piano     | Email/giorno | Email/mese | Costo     |
|-----------|-------------|------------|-----------|
| Free      | 300         | 9.000      | Gratuito  |
| Starter   | Illimitato  | 20.000     | ~25$/mese |

---

## Configurazione Docker

Nel `docker-compose.yml`, passa le variabili SMTP al container API:

```yaml
services:
  api:
    environment:
      - Smtp__Host=smtp-relay.brevo.com
      - Smtp__Port=587
      - Smtp__Username=${SMTP_USERNAME}
      - Smtp__Password=${SMTP_PASSWORD}
      - Smtp__FromEmail=noreply@tuodominio.com
      - Smtp__FromName=Seed App
      - Smtp__UseSsl=true
```

E definisci `SMTP_USERNAME` e `SMTP_PASSWORD` in un file `.env` (non committato):

```env
SMTP_USERNAME=tuaemail@brevo.com
SMTP_PASSWORD=xsmtpsib-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

---

## Verifica della configurazione

### 1. Senza SMTP (console fallback)

Avvia il backend senza configurare `Smtp:Host`. Quando richiedi un reset password, il token apparira' nei log:

```
[HH:mm:ss WRN] SMTP not configured — logging email to console
[HH:mm:ss INF] Password Reset Email → To: user@example.com, Token: CfDJ8N...
```

Per trovare il token nei log Docker:

```bash
# Da docker/
docker compose logs --tail 100 api | grep "Password Reset Email"
```

> **Nota:** Il token viene generato solo se l'email corrisponde a un utente esistente e attivo. Per motivi di sicurezza (prevenzione email enumeration), l'API restituisce sempre lo stesso messaggio di successo anche se l'utente non esiste.

### 2. Con SMTP configurato

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

| Ambiente    | Provider | Host                     | Porta | Metodo configurazione        |
|-------------|----------|--------------------------|-------|------------------------------|
| Development | Gmail    | `smtp.gmail.com`         | 587   | `dotnet user-secrets`        |
| Production  | Brevo    | `smtp-relay.brevo.com`   | 587   | Variabili d'ambiente         |
| Locale      | Nessuno  | *(vuoto)*                | —     | Default (console fallback)   |
