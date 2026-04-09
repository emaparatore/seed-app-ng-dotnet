# Implementation Plan: FEAT-2 — GDPR & Privacy Compliance

**Requirements:** `docs/requirements/FEAT-2.md`
**Status:** Not Started
**Created:** 2026-04-09
**Last Updated:** 2026-04-09

## Decisioni Prese

| Decisione | Scelta | Data |
|-----------|--------|------|
| Hard delete vs anonimizzazione | Hard delete del record utente + anonimizzazione audit log | 2026-04-09 |
| Periodo grace soft-delete | 30 giorni | 2026-04-09 |
| Background jobs | `BackgroundService` nello stesso processo API | 2026-04-09 |
| Re-accettazione privacy policy | Sì, tramite `ConsentVersion` + banner al login | 2026-04-09 |

## Story Coverage

| Story | Descrizione | Tasks | Status |
|-------|-------------|-------|--------|
| US-001 | Pagina Privacy Policy | T-01, T-02 | ✅ Done |
| US-002 | Pagina Terms of Service | T-01, T-02 | ✅ Done |
| US-003 | Consenso alla registrazione | T-03, T-04, T-05 | ✅ Done |
| US-004 | Export dati personali | T-08, T-09 | ⏳ In Progress (backend done) |
| US-005 | Hard delete account | T-06, T-07 | ⏳ Not Started |
| US-006 | Purge automatico utenti soft-deleted | T-10, T-11 | ⏳ Not Started |
| US-007 | Cleanup refresh token scaduti | T-10, T-11 | ⏳ Not Started |
| US-008 | Retention e cleanup audit log | T-10, T-11 | ⏳ Not Started |
| — | Checklist GDPR post-implementazione | T-13 | ⏳ Not Started |

---

## T-01: Pagine Privacy Policy e Terms of Service — Frontend

**Stories:** US-001, US-002
**Size:** Small
**Status:** [x] Done

**What to do:**
Creare due componenti Angular standalone per le pagine Privacy Policy e Terms of Service. Aggiungere le route `/privacy-policy` e `/terms-of-service` in `app.routes.ts` (accessibili senza guard). Il contenuto sarà placeholder testuale che il titolare del trattamento personalizzerà.

**Definition of Done:**
- [x] Componente `PrivacyPolicy` in `projects/app/src/app/pages/privacy-policy/`
- [x] Componente `TermsOfService` in `projects/app/src/app/pages/terms-of-service/`
- [x] Route `/privacy-policy` e `/terms-of-service` registrate in `app.routes.ts` senza guard (lazy-loaded)
- [x] Contenuto placeholder leggibile e ben formattato con Angular Material (`mat-card`)
- [x] Test unitari per entrambi i componenti (3 test ciascuno: creazione, titolo, sezioni)
- [x] All tests pass (`ng test app` e `ng build` OK)

**Implementation Notes:**
- Componenti standalone con `templateUrl` e `styleUrl`, lazy-loaded via `loadComponent` nelle route
- Stile `.legal-container` / `.legal-card` separato dallo stile `.auth-container` / `.auth-card` delle pagine auth (max-width 800px vs 420px, nessun centramento verticale)
- Contenuto placeholder in italiano con segnaposto `[bracketed]` per i dati da personalizzare dal titolare del trattamento
- SCSS identico per entrambe le pagine legali per coerenza visiva
- Nessuna deviazione dal piano originale

---

## T-02: Footer con link Privacy Policy e Terms of Service

**Stories:** US-001, US-002
**Size:** Small
**Status:** [x] Done

**What to do:**
Creare un componente footer (`AppFooter`) da inserire nel template principale (`app.html`, dopo `<router-outlet />`). Il footer contiene link a Privacy Policy e Terms of Service. Stile minimale, coerente con il design Material.

**Definition of Done:**
- [x] Componente `AppFooter` creato in `projects/app/src/app/footer/`
- [x] Footer inserito in `app.html` dopo `<router-outlet />` e prima di `<app-pwa-install-prompt />`
- [x] Link a `/privacy-policy` e `/terms-of-service` presenti con `routerLink`
- [x] Footer visibile su tutte le pagine (in flusso del documento, non sticky/fixed)
- [x] Test unitari per il componente (3 test: creazione, link privacy-policy, link terms-of-service)
- [x] All tests pass (`ng test app` e `ng build` OK)

**Implementation Notes:**
- Componente standalone con `templateUrl`/`styleUrl` separati, coerente con il pattern degli altri componenti dell'app
- Separatore `·` tra i link Privacy Policy e Terms of Service
- Footer centrato con flexbox, font 0.85rem, colore `rgba(0, 0, 0, 0.54)` (Material Design secondary text)
- Importato `RouterLink` per la navigazione interna, componente aggiunto agli imports di `App`
- Nessuna deviazione dal piano originale

---

## T-03: Campi consenso su entità utente — Backend

**Stories:** US-003
**Size:** Small
**Status:** [x] Done

**What to do:**
Aggiungere i campi di consenso all'entità `ApplicationUser`: `PrivacyPolicyAcceptedAt` (DateTime?), `TermsAcceptedAt` (DateTime?), `ConsentVersion` (string?). Aggiornare la configurazione EF (`ApplicationUserConfiguration`). Creare la migration EF Core.

**Definition of Done:**
- [x] Campi `PrivacyPolicyAcceptedAt`, `TermsAcceptedAt`, `ConsentVersion` aggiunti a `ApplicationUser`
- [x] Configurazione EF aggiornata con tipi e vincoli appropriati (tutti nullable, MaxLength 20 per ConsentVersion)
- [x] Migration EF Core creata (`20260409000413_AddConsentFieldsToUsers`)
- [x] Build OK (`dotnet build Seed.slnx` — 0 errori, 0 warning)
- [x] All tests pass (180 unit + 97 integration)

**Implementation Notes:**
- Campi posizionati prima di `RefreshTokens` in `ApplicationUser`, raggruppando i campi di consenso insieme
- Tutti i campi nullable per compatibilità con utenti esistenti (nessun dato default necessario)
- `ConsentVersion` con `MaxLength(20)` — sufficiente per versioning semantico (es. "1.0", "2.1.0")
- Migration genera 3 colonne nullable su `AspNetUsers`: `timestamp with time zone` per le date, `character varying(20)` per la versione
- Nessuna deviazione dal piano originale

---

## T-04: Consenso obbligatorio alla registrazione — Backend

**Stories:** US-003
**Size:** Medium
**Status:** [x] Done
**Depends on:** T-03

**What to do:**
Modificare `RegisterCommand` per includere `AcceptPrivacyPolicy` (bool) e `AcceptTermsOfService` (bool). Aggiungere validazione in `RegisterCommandValidator` (entrambi devono essere `true`). Aggiornare `RegisterCommandHandler` per salvare i timestamp di consenso e la `ConsentVersion` corrente sull'utente creato. Definire la versione corrente del consenso come configurazione in `appsettings.json` (es. `Privacy:ConsentVersion`). Aggiungere audit log per l'evento di consenso.

**Definition of Done:**
- [x] `RegisterCommand` include `AcceptPrivacyPolicy` e `AcceptTermsOfService`
- [x] Validazione FluentValidation rifiuta registrazione senza consenso
- [x] `RegisterCommandHandler` salva `PrivacyPolicyAcceptedAt`, `TermsAcceptedAt`, `ConsentVersion`
- [x] Sezione `Privacy` aggiunta in `appsettings.json` con `ConsentVersion` (default "1.0")
- [x] `PrivacySettings` in `Seed.Shared/Configuration/` registrata in DI
- [x] Audit log registra evento di consenso (`AuditActions.ConsentGiven`)
- [x] Unit test: registrazione rifiutata senza consenso (Theory con 3 combinazioni InlineData)
- [x] Unit test: registrazione con consenso salva timestamp e versione
- [x] All tests pass (184 unit + 97 integration = 281 totali)

**Implementation Notes:**
- I timestamp di consenso vengono impostati sull'oggetto `ApplicationUser` prima di `CreateAsync`, salvati atomicamente con la creazione utente
- L'audit log `ConsentGiven` è un evento separato da `UserCreated` per distinguere chiaramente le due azioni ai fini GDPR
- Creata classe `PrivacySettings` in `Seed.Shared/Configuration/` con pattern identico a `ClientSettings`, registrata in DI via `services.Configure<PrivacySettings>()`
- Il test di validazione usa `[Theory]` con `[InlineData]` per coprire tutte e 3 le combinazioni di rifiuto (solo privacy, solo terms, entrambi)
- Aggiornati anche tutti i test di integrazione (9 file) che usavano il payload di registrazione senza i campi consenso

---

## T-05: Consenso alla registrazione — Frontend

**Stories:** US-003
**Size:** Small
**Status:** [x] Done
**Depends on:** T-04

**What to do:**
Aggiungere checkbox di consenso al form di registrazione (`register.ts` / `register.html`). La checkbox include link alla Privacy Policy e ai Terms of Service. Il form non è inviabile senza la checkbox selezionata. Aggiornare la chiamata API per includere i campi di consenso.

**Definition of Done:**
- [x] Checkbox con link a Privacy Policy e Terms of Service aggiunta al form di registrazione
- [x] Form control `acceptPrivacy` aggiunto con `Validators.requiredTrue`
- [x] Messaggio di errore mostrato se checkbox non selezionata al submit
- [x] Payload API di registrazione include `acceptPrivacyPolicy` e `acceptTermsOfService`
- [x] Test unitario: form invalido senza checkbox (`acceptPrivacy: false`)
- [x] All tests pass (`ng test app` e `ng build` OK)

**Implementation Notes:**
- Una sola checkbox copre entrambi i consensi (privacy + terms) — il testo include link a entrambi i documenti per semplificare la UX
- Il payload mappa `acceptPrivacy` → `acceptPrivacyPolicy` + `acceptTermsOfService` via destructuring in `onSubmit()`
- Aggiunto `MatCheckboxModule` agli imports del componente, stili `.consent-checkbox` e `.consent-error` in SCSS
- `RegisterRequest` in `shared-auth` aggiornato con i due campi boolean `acceptPrivacyPolicy` e `acceptTermsOfService`
- Test usa `expect.objectContaining` per verificare i campi di consenso nel payload senza duplicare l'intero oggetto

---

## T-06: Hard delete account — Backend

**Stories:** US-005
**Size:** Medium
**Status:** [ ] Not Started

**What to do:**
Modificare `DeleteAccountCommandHandler` per eseguire hard delete anziché soft delete. Il flusso diventa: verificare password → anonimizzare audit log dell'utente (set `UserId = null`, rimuovere PII dal campo `Details`) → eliminare refresh token → eliminare il record utente dal DB (via `UserManager.DeleteAsync`). Creare un servizio `IUserPurgeService` in Application con interfaccia, implementazione in Infrastructure, per riutilizzare la logica in US-006.

**Definition of Done:**
- [ ] Interfaccia `IUserPurgeService` in `Application/Common/Interfaces/` con metodo `PurgeUserAsync(Guid userId)`
- [ ] Implementazione `UserPurgeService` in `Infrastructure/Services/` che: anonimizza audit log, elimina refresh token, elimina utente
- [ ] `DeleteAccountCommandHandler` usa `IUserPurgeService` per il hard delete
- [ ] Registrazione del servizio in DI
- [ ] Audit log di cancellazione scritto prima del purge (con dati anonimizzati)
- [ ] Unit test: purge anonimizza audit log
- [ ] Unit test: purge elimina refresh token
- [ ] Integration test: hard delete rimuove utente dal DB
- [ ] All tests pass

---

## T-07: Hard delete account — Frontend

**Stories:** US-005
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-06

**What to do:**
Aggiornare il dialog di conferma cancellazione nella pagina profilo per informare l'utente che la cancellazione è definitiva e irreversibile. Aggiungere testo esplicativo che i dati verranno eliminati permanentemente.

**Definition of Done:**
- [ ] Testo dialog aggiornato: menzione esplicita che la cancellazione è permanente e i dati saranno eliminati
- [ ] Nessuna modifica funzionale necessaria (il dialog già raccoglie la password)
- [ ] All tests pass

---

## T-08: Export dati personali — Backend

**Stories:** US-004
**Size:** Medium
**Status:** [x] Done

**What to do:**
Creare query MediatR `ExportMyDataQuery` che raccoglie tutti i dati dell'utente autenticato: profilo (nome, cognome, email, date, consensi, ruoli), audit log relativo all'utente. Endpoint `GET /api/v1/auth/export-my-data` protetto da `[Authorize]`. La risposta è JSON. L'audit log registra la richiesta di export.

**Definition of Done:**
- [x] `ExportMyDataQuery` e `ExportMyDataQueryHandler` in `Application/Auth/Queries/ExportMyData/`
- [x] DTO `UserDataExportDto` con tutte le sezioni (profilo, consensi, ruoli, audit log) — 4 record: `UserDataExportDto`, `UserProfileExportDto`, `UserConsentExportDto`, `AuditLogExportDto`
- [x] Endpoint `GET /api/v1/auth/export-my-data` in `AuthController`
- [x] Endpoint protetto da `[Authorize]`, restituisce solo dati dell'utente autenticato
- [x] Audit log registra evento di export (`AuditActions.DataExported`)
- [x] Unit test: 4 test cases (dati corretti, utente non trovato, audit log scritto, audit log vuoto)
- [x] Integration test: endpoint restituisce 401 senza auth, 200 con dati corretti con auth
- [x] All tests pass

**Implementation Notes:**
- Usato `.ToList()` sincronamente per la query audit log perché il layer Application non referenzia `Microsoft.EntityFrameworkCore` (Clean Architecture), coerente con gli altri handler (`GetAuditLogEntriesQueryHandler`, `ExportAuditLogQueryHandler`)
- L'audit log `DataExported` viene scritto dopo la raccolta dati, così l'evento non appare nella stessa risposta di export
- DTO strutturato in 4 record separati (`UserDataExportDto`, `UserProfileExportDto`, `UserConsentExportDto`, `AuditLogExportDto`) per chiarezza e separazione delle sezioni
- Fix collaterale in `UserPurgeServiceTests.cs` (T-06): corretto metodo FluentAssertions `HaveCountGreaterOrEqualTo` → `HaveCountGreaterThanOrEqualTo` per far passare la build
- Aggiunto `AuditActions.DataExported` costante nel Domain layer

---

## T-09: Export dati personali — Frontend

**Stories:** US-004
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-08

**What to do:**
Aggiungere pulsante "Esporta i miei dati" nella pagina profilo. Al click, chiama l'endpoint e scarica il JSON come file. Usare `Blob` + `URL.createObjectURL` per il download.

**Definition of Done:**
- [ ] Pulsante "Esporta i miei dati" aggiunto in `profile.html`
- [ ] Metodo `exportMyData()` in `profile.ts` che chiama l'endpoint e scarica il file JSON
- [ ] Metodo `exportMyData()` aggiunto in `AuthService` (shared-auth)
- [ ] Feedback visivo durante il download (loading state)
- [ ] Test unitario: verifica che il metodo venga chiamato al click
- [ ] All tests pass

---

## T-10: Configurazione Data Retention e servizio di cleanup — Backend

**Stories:** US-006, US-007, US-008
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-06 (riutilizza `IUserPurgeService`)

**What to do:**
Creare la sezione di configurazione `DataRetention` in `appsettings.json` con i periodi di retention e gli intervalli di esecuzione. Creare un servizio `IDataCleanupService` con metodi per: purge utenti soft-deleted, cleanup refresh token, cleanup audit log. Implementazione in Infrastructure che usa `ApplicationDbContext` direttamente per le query bulk delete.

**Definition of Done:**
- [ ] Sezione `DataRetention` in `appsettings.json`:
  - `SoftDeletedUserRetentionDays` (default: 30)
  - `RefreshTokenRetentionDays` (default: 7)
  - `AuditLogRetentionDays` (default: 365)
  - `CleanupIntervalHours` (default: 24)
- [ ] Classe `DataRetentionSettings` in `Shared/Configuration/`
- [ ] Interfaccia `IDataCleanupService` in `Application/Common/Interfaces/`
- [ ] Implementazione `DataCleanupService` in `Infrastructure/Services/`
- [ ] Metodo purge utenti soft-deleted riutilizza logica di `IUserPurgeService`
- [ ] Metodo cleanup refresh token elimina token scaduti/revocati oltre il periodo
- [ ] Metodo cleanup audit log elimina entry oltre il periodo di retention
- [ ] Unit test per ogni metodo di cleanup
- [ ] All tests pass

---

## T-11: Background service per Data Retention — Backend

**Stories:** US-006, US-007, US-008
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-10

**What to do:**
Creare `DataRetentionBackgroundService` che estende `BackgroundService`. Esegue periodicamente (intervallo da config) i tre metodi di cleanup via `IDataCleanupService`. Logga i risultati (numero di record eliminati per ogni tipo). Registrare il servizio in `Program.cs`.

**Definition of Done:**
- [ ] `DataRetentionBackgroundService` in `Infrastructure/Services/` estende `BackgroundService`
- [ ] Esegue i tre cleanup (utenti, token, audit log) ad ogni ciclo
- [ ] Intervallo configurabile da `DataRetentionSettings.CleanupIntervalHours`
- [ ] Logga risultati via `ILogger` (es. "Purged 3 soft-deleted users, cleaned 150 expired tokens, removed 0 audit log entries")
- [ ] Gestione errori: un fallimento in un cleanup non blocca gli altri
- [ ] Registrazione in DI (`builder.Services.AddHostedService<DataRetentionBackgroundService>()`)
- [ ] Unit test: verifica che il service chiami i metodi di cleanup
- [ ] All tests pass

---

## T-12: Re-accettazione consenso dopo aggiornamento Privacy Policy

**Stories:** US-003
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-04

**What to do:**
Modificare il `LoginCommandHandler` per verificare se la `ConsentVersion` dell'utente corrisponde alla versione corrente (da config). Se non corrisponde, la risposta di login include un flag `consentUpdateRequired: true` e la nuova versione. Creare un endpoint `POST /api/v1/auth/accept-updated-consent` che aggiorna i timestamp di consenso e la versione. Nel frontend, intercettare il flag dopo il login e mostrare un dialog che chiede di ri-accettare. Se l'utente rifiuta, eseguire il logout.

**Definition of Done:**
- [ ] `LoginResponse` include campo `consentUpdateRequired` (bool) e `currentConsentVersion` (string)
- [ ] `LoginCommandHandler` confronta `user.ConsentVersion` con la versione da config
- [ ] Endpoint `POST /api/v1/auth/accept-updated-consent` aggiorna consenso utente
- [ ] Validazione: solo utenti autenticati possono aggiornare il proprio consenso
- [ ] Frontend: dialog dopo login se `consentUpdateRequired === true`
- [ ] Frontend: se utente rifiuta il dialog, logout automatico
- [ ] Unit test backend: login con versione diversa restituisce flag
- [ ] Unit test backend: endpoint aggiorna consenso correttamente
- [ ] Test frontend: dialog appare quando flag è true
- [ ] All tests pass

---

## T-13: Checklist GDPR post-implementazione — Documentazione

**Stories:** —
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-01 (il placeholder della privacy policy deve esistere)

**What to do:**
Creare un documento `docs/gdpr-compliance-checklist.md` che guida il titolare del trattamento nel completare gli aspetti non tecnici della conformità GDPR. Il documento elenca le azioni manuali necessarie dopo l'implementazione tecnica di FEAT-2, con indicazioni pratiche su cosa fare, chi deve farlo e risorse utili.

**Definition of Done:**
- [ ] File `docs/gdpr-compliance-checklist.md` creato
- [ ] Sezione: **Testo legale Privacy Policy e ToS** — cosa deve contenere (Art. 13-14), suggerimento di usare generatori (es. iubenda.com) o consultare un legale, dove sostituire il placeholder nel codice
- [ ] Sezione: **Contatto privacy** — decidere un'email dedicata (es. `privacy@dominio.com`), indicarla nella privacy policy
- [ ] Sezione: **DPA con fornitori terzi** — se si usa SMTP esterno (Brevo, Gmail), firmare un Data Processing Agreement con il fornitore
- [ ] Sezione: **Registro dei trattamenti (Art. 30)** — cosa deve contenere, template semplificato (Excel), quando è obbligatorio
- [ ] Ogni sezione ha: descrizione dell'azione, responsabile suggerito, risorse/link utili, checkbox di stato
- [ ] Riferimento al documento aggiunto in `README.md` (indice docs) e in `CLAUDE.md` (lista docs)
- [ ] All tests pass (nessun test necessario, solo documentazione)
