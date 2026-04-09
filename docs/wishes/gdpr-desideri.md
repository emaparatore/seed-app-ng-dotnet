# GDPR

## Stato attuale

| Area | Stato | Note |
| --- | --- | --- |
| Audit logging | **OK** | Sistema completo con IP, user agent, azioni |
| Dati raccolti | **OK** | Solo email, nome, cognome — PII minimale |
| Condivisione dati terzi | **OK** | Nessuna integrazione esterna |
| HTTPS | **OK** | `UseHttpsRedirection` + HSTS |
| Password hashing | **OK** | ASP.NET Identity (bcrypt) |

* * *

## Cosa manca per la conformità GDPR

### 1\. Privacy Policy e Terms of Service (obbligatori)

- Pagina pubblica con informativa sulla privacy (Art. 13-14)
- Chi è il titolare del trattamento, quali dati raccogli, perché, base giuridica, durata conservazione, diritti dell'utente
- Link nel footer e durante la registrazione

### 2\. Consenso tracciato (Art. 7)

- Checkbox in registrazione: "Ho letto e accetto la Privacy Policy"
- Campi DB: `PrivacyPolicyAcceptedAt`, `TermsAcceptedAt`
- Il consenso deve essere dimostrabile — salvare timestamp + versione accettata

### 3\. Diritto di accesso / Data Export (Art. 15)

- Endpoint per l'utente per scaricare tutti i propri dati in formato portabile (JSON/CSV)
- Attualmente esiste solo `GET /auth/me` ma non include audit log, refresh tokens, ecc.

### 4\. Diritto all'oblio / Cancellazione reale (Art. 17)

- Attualmente l'app fa **soft delete** (`IsDeleted = true`)
- Serve un meccanismo di **hard delete** o **purge automatico** dopo un periodo definito (es. 30 giorni)
- Anonimizzare i dati nell'audit log relativi all'utente cancellato

### 5\. Data Retention Policy (Art. 5.1.e)

- Definire per quanto tempo conservi ogni tipo di dato:
    - Audit log: es. 2 anni
    - Account soft-deleted: es. 30 giorni poi purge
    - Refresh token scaduti: cleanup periodico
- Implementare un background job che faccia pulizia automatica

### 6\. Cookie Banner / Consent

- Se usi cookie (analytics, tracking), serve un banner con opt-in
- Attualmente l'app usa localStorage per i JWT → meno critico, ma se aggiungi analytics (Google Analytics, ecc.) diventa obbligatorio

### 7\. Registro dei trattamenti (Art. 30)

- Documento interno (non necessariamente nel codice) che elenca:
    - Quali dati tratti, perché, base giuridica, chi vi accede, per quanto li conservi
    - Se hai meno di 250 dipendenti e il trattamento non è "a rischio", è semplificato

### 8\. DPO e contatto

- Se sei un'azienda, serve un punto di contatto per richieste privacy
- Email dedicata (es. `privacy@tuodominio.com`) indicata nella privacy policy

* * *

## Priorità suggerite

| Priorità | Azione | Effort |
| --- | --- | --- |
| **Alta** | Privacy Policy + ToS (pagine + link in registrazione) | Medio |
| **Alta** | Consenso tracciato in DB alla registrazione | Basso |
| **Alta** | Hard delete / purge automatico utenti cancellati | Medio |
| **Media** | Endpoint "Esporta i miei dati" | Medio |
| **Media** | Data retention policy + background job cleanup | Medio |
| **Bassa** | Cookie banner (solo se aggiungi analytics/tracking) | Basso |
| **Bassa** | Crittografia at-rest per colonne PII in PostgreSQL | Alto |

* * *

## Aspetti non tecnici (ma necessari)

- **Testo della Privacy Policy**: serve un testo legale conforme. Puoi usare generatori online come iubenda.com o consultare un avvocato
- **DPIA** (Data Protection Impact Assessment): necessario se fai trattamenti "ad alto rischio"
- **DPA** (Data Processing Agreement): se usi SMTP di terze parti (Brevo, Gmail) per inviare email, serve un accordo con il fornitore
- **Registro dei trattamenti**: documento interno (Excel va bene)