# FEAT-2: GDPR & Privacy Compliance

## Overview

L'applicazione deve conformarsi al Regolamento Generale sulla Protezione dei Dati (GDPR - Reg. UE 2016/679) e alle best practice in materia di privacy. Questo comprende: informativa sulla privacy e termini di servizio accessibili pubblicamente, raccolta e tracciamento del consenso dell'utente al momento della registrazione, diritto di accesso ai propri dati (Art. 15), diritto all'oblio con cancellazione effettiva dei dati (Art. 17), e politiche di data retention con pulizia automatica dei dati scaduti.

L'app attualmente ha già un sistema di audit logging completo, raccolta dati minimale (email, nome, cognome), nessuna condivisione con terze parti, HTTPS configurato e password hashing via ASP.NET Identity. Queste basi solide riducono il perimetro di intervento.

## Stato Attuale

| Area | Stato | Note |
|------|-------|------|
| Audit logging | Esistente | Sistema completo con IP, user agent, azioni |
| Dati raccolti | OK | Solo email, nome, cognome — PII minimale |
| Condivisione dati terzi | OK | Nessuna integrazione esterna |
| HTTPS | OK | `UseHttpsRedirection` + HSTS |
| Password hashing | OK | ASP.NET Identity (bcrypt) |
| Cancellazione account | Parziale | Solo soft delete (IsDeleted), nessun hard delete/purge |
| Privacy Policy / ToS | Mancante | Nessuna pagina né route |
| Consenso registrazione | Mancante | Nessun tracciamento del consenso |
| Export dati utente | Mancante | Solo `GET /auth/me`, non include tutti i dati |
| Data retention | Mancante | Nessuna pulizia automatica |

## Requisiti Funzionali

### RF-1: Pagine Privacy Policy e Terms of Service

Il sistema deve esporre pagine pubbliche con l'informativa sulla privacy (Art. 13-14 GDPR) e i termini di servizio. Le pagine devono essere accessibili senza autenticazione, raggiungibili dal footer dell'applicazione e dal form di registrazione.

### RF-2: Consenso alla Registrazione

Al momento della registrazione, l'utente deve esprimere consenso esplicito all'informativa sulla privacy e ai termini di servizio tramite checkbox obbligatoria. Il sistema deve registrare data/ora del consenso e la versione del documento accettato. Il consenso deve essere dimostrabile (Art. 7 GDPR).

### RF-3: Diritto di Accesso — Export Dati Personali

L'utente autenticato deve poter esportare tutti i propri dati personali in formato portabile (JSON). I dati esportati includono: profilo (nome, cognome, email, date creazione/aggiornamento), storico accessi (audit log relativo all'utente), consensi registrati.

### RF-4: Diritto all'Oblio — Cancellazione Effettiva

Il sistema deve supportare la cancellazione effettiva (hard delete) dei dati dell'utente, non solo il soft delete attuale. Dopo la cancellazione: i dati personali vengono rimossi, le entry nell'audit log vengono anonimizzate (userId nullificato, dettagli con PII rimossi), i refresh token vengono eliminati.

### RF-5: Data Retention e Pulizia Automatica

Il sistema deve implementare politiche di conservazione dei dati con pulizia automatica:
- Utenti soft-deleted: purge dopo un periodo configurabile (default 30 giorni)
- Refresh token scaduti/revocati: cleanup periodico
- Audit log: retention configurabile (default 365 giorni)

## Requisiti Non-Funzionali

### RNF-1: Sicurezza dei dati esportati

L'endpoint di export dati deve essere protetto da autenticazione e deve restituire esclusivamente i dati dell'utente autenticato. Non deve essere possibile accedere ai dati di altri utenti.

### RNF-2: Idempotenza della cancellazione

Richiedere la cancellazione di un account già cancellato non deve produrre errori né effetti collaterali.

### RNF-3: Configurabilità retention

I periodi di retention devono essere configurabili via `appsettings.json` senza richiedere modifiche al codice.

### RNF-4: Contenuti privacy policy

Il contenuto testuale della privacy policy e dei termini di servizio sarà fornito dall'utente (titolare del trattamento). L'implementazione prevede pagine con contenuto statico facilmente modificabile, non un CMS.

## User Stories

#### US-001: Visualizzare la Privacy Policy

**As a** visitatore del sito,
**I want** accedere alla pagina della Privacy Policy,
**So that** possa leggere come vengono trattati i miei dati prima di registrarmi.

**Acceptance Criteria:**
- [ ] Esiste una route `/privacy-policy` accessibile senza autenticazione
- [ ] La pagina mostra il contenuto dell'informativa privacy in formato leggibile
- [ ] Il footer dell'applicazione contiene un link alla Privacy Policy
- [ ] Il form di registrazione contiene un link alla Privacy Policy

#### US-002: Visualizzare i Terms of Service

**As a** visitatore del sito,
**I want** accedere alla pagina dei Terms of Service,
**So that** possa leggere le condizioni d'uso prima di registrarmi.

**Acceptance Criteria:**
- [ ] Esiste una route `/terms-of-service` accessibile senza autenticazione
- [ ] La pagina mostra il contenuto dei termini di servizio in formato leggibile
- [ ] Il footer dell'applicazione contiene un link ai Terms of Service
- [ ] Il form di registrazione contiene un link ai Terms of Service

#### US-003: Accettare Privacy Policy e ToS alla registrazione

**As a** nuovo utente,
**I want** dare il mio consenso esplicito alla Privacy Policy e ai Terms of Service durante la registrazione,
**So that** il mio consenso sia tracciato e dimostrabile come richiesto dal GDPR.

**Acceptance Criteria:**
- [ ] Il form di registrazione include una checkbox "Ho letto e accetto la Privacy Policy e i Terms of Service" (con link ai documenti)
- [ ] La checkbox è obbligatoria — il form non può essere inviato senza consenso
- [ ] Il backend salva `PrivacyPolicyAcceptedAt` e `TermsAcceptedAt` (timestamp UTC) sull'entità utente
- [ ] Il backend salva `ConsentVersion` (stringa, es. "1.0") per tracciare la versione accettata
- [ ] Il `RegisterCommand` include i nuovi campi di consenso
- [ ] La validazione backend rifiuta la registrazione se il consenso non è fornito
- [ ] L'audit log registra l'evento di consenso

#### US-004: Esportare i propri dati personali

**As a** utente autenticato,
**I want** scaricare tutti i miei dati personali in formato JSON,
**So that** possa esercitare il mio diritto di accesso (Art. 15 GDPR) e portabilità (Art. 20).

**Acceptance Criteria:**
- [ ] Esiste un endpoint `GET /api/v1/auth/export-my-data` protetto da autenticazione
- [ ] La risposta JSON include: profilo (nome, cognome, email, date), consensi, ruoli
- [ ] La risposta JSON include lo storico audit log relativo all'utente
- [ ] L'endpoint restituisce solo i dati dell'utente autenticato (non è possibile accedere ai dati altrui)
- [ ] L'audit log registra la richiesta di export dati
- [ ] Nel profilo utente (frontend) è presente un pulsante "Esporta i miei dati"

#### US-005: Cancellare definitivamente il proprio account

**As a** utente autenticato,
**I want** cancellare definitivamente il mio account e tutti i miei dati personali,
**So that** possa esercitare il mio diritto all'oblio (Art. 17 GDPR).

**Acceptance Criteria:**
- [ ] L'endpoint `DELETE /api/v1/auth/account` esegue hard delete dei dati personali (non solo soft delete)
- [ ] I dati eliminati includono: dati profilo, refresh token, consensi
- [ ] Le entry nell'audit log vengono anonimizzate: `UserId` impostato a null, campi `Details` con PII ripuliti
- [ ] L'utente deve confermare con la propria password prima della cancellazione
- [ ] Dopo la cancellazione, tutti i token dell'utente vengono invalidati
- [ ] L'audit log registra l'evento di cancellazione (con dati anonimizzati)
- [ ] L'operazione è idempotente: richiedere la cancellazione di un account già cancellato restituisce un messaggio appropriato senza errori

#### US-006: Purge automatico utenti soft-deleted

**As a** amministratore del sistema,
**I want** che gli utenti soft-deleted vengano eliminati definitivamente dopo un periodo configurabile,
**So that** i dati personali non vengano conservati oltre il necessario (principio di limitazione della conservazione, Art. 5.1.e).

**Acceptance Criteria:**
- [ ] Esiste un background service che esegue periodicamente il purge degli utenti soft-deleted
- [ ] Il periodo di retention è configurabile in `appsettings.json` (default: 30 giorni)
- [ ] L'intervallo di esecuzione del job è configurabile (default: ogni 24 ore)
- [ ] Il purge esegue hard delete come descritto in US-005 (inclusa anonimizzazione audit log)
- [ ] Il job logga l'attività (numero di utenti purgati) tramite il logger dell'applicazione

#### US-007: Cleanup refresh token scaduti

**As a** amministratore del sistema,
**I want** che i refresh token scaduti e revocati vengano eliminati periodicamente,
**So that** il database non accumuli dati inutili e si rispetti il principio di minimizzazione.

**Acceptance Criteria:**
- [ ] Esiste un background service che elimina i refresh token scaduti o revocati
- [ ] Il job viene eseguito periodicamente (configurabile, default: ogni 24 ore)
- [ ] I token eliminati sono quelli dove `ExpiresAt < now` oppure `RevokedAt IS NOT NULL` e il token ha più di X giorni (configurabile, default: 7 giorni)
- [ ] Il job logga il numero di token eliminati

#### US-008: Retention e cleanup audit log

**As a** amministratore del sistema,
**I want** che le entry dell'audit log più vecchie di un periodo configurabile vengano eliminate,
**So that** il database non cresca indefinitamente e si rispettino le policy di conservazione.

**Acceptance Criteria:**
- [ ] Esiste un background service che elimina le entry dell'audit log più vecchie del periodo di retention
- [ ] Il periodo è configurabile in `appsettings.json` (default: 365 giorni)
- [ ] Il job viene eseguito periodicamente (configurabile, default: ogni 24 ore)
- [ ] Il job logga il numero di entry eliminate

## Dipendenze tra User Stories

- **US-003** dipende da **US-001** e **US-002** (i link alla privacy policy e ToS devono esistere)
- **US-005** è indipendente ma condivide la logica di hard delete con **US-006**
- **US-006** riutilizza la logica di hard delete di **US-005**
- **US-007** e **US-008** sono indipendenti tra loro e dalle altre stories

## Fuori Scope

- **Cookie banner**: l'app attualmente non usa cookie di tracciamento/analytics. Se in futuro verranno aggiunti, il cookie banner sarà oggetto di un feature separato.
- **Crittografia at-rest per colonne PII**: richiede valutazione architetturale separata (impatto su query, performance, key management).
- **DPO / Registro dei trattamenti**: sono documenti organizzativi, non implementazioni nel codice.
- **Consenso marketing/newsletter**: l'app invia solo email transazionali. Se in futuro si aggiungessero email marketing, il consenso marketing sarà un feature separato.
- **Testo legale della privacy policy**: il contenuto testuale sarà un placeholder che il titolare del trattamento dovrà personalizzare.

## Open Questions

1. **Hard delete vs anonimizzazione**: Per US-005, confermi che la strategia preferita è hard delete del record utente + anonimizzazione delle entry audit log? L'alternativa sarebbe anonimizzare anche il record utente (sovrascrivere nome/email con valori anonimi) mantenendo il record nel DB.

2. **Periodo di grace per soft-delete**: Per US-006, il default di 30 giorni prima del purge è adeguato? Alcuni sistemi usano 14 giorni, altri 90.

3. **Background service hosting**: Per US-006/007/008, i background job devono girare nello stesso processo dell'API (via `IHostedService` / `BackgroundService`) oppure preferisci un processo separato (es. un console app schedulato)?

4. **Versioning della privacy policy**: Per US-003, quando il testo della privacy policy cambia, gli utenti esistenti devono ri-accettare la nuova versione? Oppure è sufficiente tracciare la versione al momento della registrazione?
