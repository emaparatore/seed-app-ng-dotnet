# GDPR Compliance Checklist

Questa checklist guida il **titolare del trattamento** nel completare gli aspetti non tecnici della conformità GDPR, dopo l'implementazione delle funzionalità tecniche previste da [FEAT-2](requirements/FEAT-2.md).

**Prerequisiti tecnici già implementati:**
- Pagine Privacy Policy e Terms of Service con testo placeholder (T-01)
- Consenso obbligatorio alla registrazione con timestamp e versioning (T-03, T-04, T-05)
- Hard delete account con anonimizzazione audit log (T-06, T-07)
- Export dati personali in formato JSON (T-08, T-09)
- Data retention automatica con cleanup periodico (T-10, T-11)
- Re-accettazione consenso dopo aggiornamento privacy policy (T-12)

---

## 1. Testo legale Privacy Policy e Terms of Service

La Privacy Policy deve contenere le informazioni richieste dagli **Art. 13-14 GDPR**:

- Identità e dati di contatto del titolare del trattamento
- Dati di contatto del DPO (se nominato)
- Finalità del trattamento e base giuridica (Art. 6)
- Categorie di dati personali trattati
- Destinatari o categorie di destinatari dei dati
- Trasferimenti verso paesi terzi (se applicabile) e garanzie adeguate
- Periodo di conservazione dei dati (o criteri per determinarlo)
- Diritti dell'interessato: accesso, rettifica, cancellazione, limitazione, portabilità, opposizione
- Diritto di reclamo all'autorità di controllo (Garante Privacy)
- Se il conferimento dei dati è obbligatorio o facoltativo e le conseguenze del mancato conferimento

**Dove modificare i file:**
- Privacy Policy: `frontend/web/projects/app/src/app/pages/privacy-policy/privacy-policy.html`
- Terms of Service: `frontend/web/projects/app/src/app/pages/terms-of-service/terms-of-service.html`

I file contengono testo placeholder in italiano con segnaposto `[bracketed]` da sostituire con i dati reali.

**Risorse utili:**
- [iubenda.com](https://www.iubenda.com/) — Generatore automatico di privacy policy conformi GDPR
- [Termly](https://termly.io/) — Generatore di privacy policy e terms of service
- Consultare un avvocato specializzato in diritto della privacy per validare il testo finale

**Responsabile suggerito:** Titolare del trattamento (con supporto legale)

**Nota importante:** Dopo aver aggiornato il testo, se la struttura o il contenuto della privacy policy cambia in modo sostanziale, incrementare il valore `Privacy:ConsentVersion` in `appsettings.json` per attivare la ri-accettazione automatica da parte degli utenti al prossimo login.

- [ ] Testo Privacy Policy personalizzato e completo
- [ ] Testo Terms of Service personalizzato e completo
- [ ] Segnaposti `[bracketed]` rimossi da entrambi i documenti
- [ ] Testo validato da un legale

---

## 2. Contatto privacy

Il GDPR richiede che gli interessati possano contattare il titolare del trattamento per esercitare i propri diritti.

**Azioni richieste:**
- Decidere un indirizzo email dedicato (es. `privacy@dominio.com`)
- Indicare l'email nella Privacy Policy e nei Terms of Service
- Assicurarsi che la casella sia monitorata regolarmente
- Valutare se è necessario nominare un **DPO (Data Protection Officer)** ai sensi dell'Art. 37 GDPR:
  - Obbligatorio se: ente pubblico, monitoraggio regolare e sistematico su larga scala, trattamento su larga scala di dati sensibili
  - Consigliato in tutti gli altri casi come buona pratica

**Responsabile suggerito:** Titolare del trattamento

- [ ] Email dedicata per la privacy creata e attiva
- [ ] Email indicata nella Privacy Policy
- [ ] Necessità di DPO valutata
- [ ] DPO nominato (se necessario)

---

## 3. DPA con fornitori terzi

Se dati personali vengono trattati da fornitori esterni (sub-responsabili), è necessario un **Data Processing Agreement (DPA)** ai sensi dell'Art. 28 GDPR.

**Fornitori comuni da verificare:**

| Fornitore | Tipo di servizio | DPA necessario |
|-----------|-----------------|----------------|
| **Brevo** (ex Sendinblue) | Invio email transazionali (SMTP) | Sì |
| **Gmail / Google Workspace** | Email (se usato come SMTP in dev/prod) | Sì |
| Provider hosting (VPS) | Hosting server e database | Sì |
| Cloudflare | CDN e DNS | Sì |
| Eventuali servizi analytics | Tracciamento utenti | Sì |

**Link utili per i DPA:**
- [Brevo DPA](https://www.brevo.com/legal/termsofuse/#data-processing-agreement) — Disponibile nell'area account Brevo
- [Google Workspace DPA](https://workspace.google.com/terms/dpa_terms.html) — Data Processing Amendment
- [Cloudflare DPA](https://www.cloudflare.com/cloudflare-customer-dpa/) — Customer DPA

**Responsabile suggerito:** Titolare del trattamento (con supporto legale)

- [ ] Elenco completo dei fornitori che trattano dati personali
- [ ] DPA firmato con il provider SMTP (Brevo/Gmail)
- [ ] DPA firmato con il provider hosting
- [ ] DPA firmato con Cloudflare (se utilizzato)
- [ ] DPA firmato con eventuali altri fornitori

---

## 4. Registro dei trattamenti (Art. 30)

Il registro dei trattamenti è un documento obbligatorio che descrive tutte le attività di trattamento dei dati personali.

**Quando è obbligatorio:**
- Organizzazioni con più di 250 dipendenti
- **Oppure** se il trattamento non è occasionale, riguarda dati sensibili, o può presentare un rischio per i diritti e le libertà degli interessati
- In pratica: **quasi sempre obbligatorio** per qualsiasi applicazione che gestisce dati utente

**Contenuto del registro (Art. 30, par. 1):**

| Campo | Esempio per questa applicazione |
|-------|---------------------------------|
| Nome e contatti del titolare | [Nome azienda], [email], [indirizzo] |
| Finalità del trattamento | Gestione account utente, autenticazione, comunicazioni di servizio |
| Categorie di interessati | Utenti registrati |
| Categorie di dati | Nome, cognome, email, password (hash), log di audit, token di sessione |
| Categorie di destinatari | Provider SMTP (Brevo/Gmail), provider hosting |
| Trasferimenti extra-UE | Indicare se Brevo/hosting/CDN hanno server fuori dall'UE |
| Termini di cancellazione | Account: su richiesta utente + 30 giorni grace period; Refresh token: 7 giorni; Audit log: 365 giorni |
| Misure di sicurezza | HTTPS/TLS, password hashing (ASP.NET Identity), JWT con refresh token rotation, audit logging |

**Risorse utili:**
- [Template registro trattamenti del Garante Privacy](https://www.garanteprivacy.it/registro-delle-attivita-di-trattamento) — Modello ufficiale italiano
- Un semplice foglio Excel con le colonne sopra elencate è sufficiente per la maggior parte delle organizzazioni

**Responsabile suggerito:** Titolare del trattamento (con supporto DPO se nominato)

- [ ] Registro dei trattamenti creato
- [ ] Tutte le attività di trattamento documentate
- [ ] Registro aggiornato con i periodi di retention configurati nell'applicazione
- [ ] Registro conservato in luogo sicuro e aggiornabile

---

## 5. Informativa cookie (se applicabile)

La normativa ePrivacy (Direttiva 2002/58/CE e provvedimenti del Garante) richiede il consenso per i cookie non tecnici.

**Azioni richieste:**
- Verificare se l'applicazione usa cookie non strettamente necessari (analytics, marketing, profilazione)
- Se **no** (solo cookie tecnici di sessione/autenticazione): nessun cookie banner necessario, ma menzionare i cookie tecnici nella Privacy Policy
- Se **sì**: implementare un cookie banner con consenso preventivo (opt-in) prima di installare i cookie non tecnici

**Stato attuale:** L'applicazione usa solo cookie tecnici per l'autenticazione (JWT/refresh token). Se si aggiungono servizi di analytics o marketing in futuro, sarà necessario un cookie banner.

**Responsabile suggerito:** Titolare del trattamento + sviluppatore (se serve implementare il banner)

- [ ] Audit dei cookie effettuato
- [ ] Cookie tecnici documentati nella Privacy Policy
- [ ] Cookie banner implementato (se necessario)

---

## 6. Riepilogo funzionalità tecniche implementate

| Funzionalità | Riferimento tecnico | Articolo GDPR |
|-------------|---------------------|---------------|
| Privacy Policy e Terms of Service | `pages/privacy-policy/`, `pages/terms-of-service/` | Art. 13-14 (Informativa) |
| Consenso alla registrazione | `RegisterCommand` con `AcceptPrivacyPolicy`/`AcceptTermsOfService`, `PrivacySettings.ConsentVersion` | Art. 6-7 (Base giuridica, Consenso) |
| Re-accettazione consenso | `LoginCommandHandler` verifica `ConsentVersion`, endpoint `accept-updated-consent` | Art. 7 (Gestione consenso) |
| Export dati personali | `GET /api/v1/auth/export-my-data` — JSON con profilo, consensi, ruoli, audit log | Art. 15, 20 (Accesso, Portabilità) |
| Cancellazione account | `DELETE /api/v1/auth/delete-account` — hard delete + anonimizzazione audit log | Art. 17 (Diritto alla cancellazione) |
| Data retention automatica | `DataRetentionBackgroundService` — cleanup utenti soft-deleted (30gg), token (7gg), audit log (365gg) | Art. 5(1)(e) (Limitazione conservazione) |
| Audit logging | Tutti gli eventi significativi (login, consenso, export, cancellazione) registrati | Art. 5(2) (Accountability) |
