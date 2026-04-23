# FEAT-4 — Mobile App Essentials

## Obiettivo

Rendere la parte mobile della seed app **production-ready** implementando le 8 funzionalità infrastrutturali che praticamente ogni app mobile richiede. L'obiettivo è eliminare il lavoro ripetitivo tra progetti: queste feature devono essere presenti, strutturate e riusabili senza modifiche sostanziali.

**Stack:** .NET MAUI (`frontend/mobile/`)

---

## Funzionalità

### FEAT-4.1 — Autenticazione

Login, logout e gestione del ciclo di vita del token JWT.

**Comportamento atteso:**
- Login con email e password tramite API backend
- Salvataggio del token di accesso e del refresh token in `SecureStorage`
- Refresh automatico del token scaduto prima di ogni chiamata HTTP
- Logout con pulizia di token e stato locale
- Reindirizzamento automatico al login se il token non è rinnovabile

**Note:** Deve integrarsi con il backend esistente (`/api/auth/login`, `/api/auth/refresh`). Il token non va mai salvato in SQLite o `Preferences`.

---

### FEAT-4.2 — Secure Storage

Astrazione centralizzata per salvare dati sensibili in modo sicuro (keychain su iOS, keystore su Android).

**Comportamento atteso:**
- Servizio `ISecureStorageService` che wrappa `SecureStorage` di MAUI Essentials
- Chiavi tipizzate (costanti) per evitare magic string sparse nel codice
- Pulizia completa allo logout (tutti i dati sensibili rimossi)

**Note:** Usato internamente da FEAT-4.1 (token) e da qualsiasi altra feature che tratti dati sensibili. Non è un layer di persistenza general-purpose — per quello c'è FEAT-4.3.

---

### FEAT-4.3 — Database Locale (SQLite)

Persistenza locale strutturata per dati applicativi, basata su SQLite.

**Comportamento atteso:**
- Setup di `sqlite-net-pcl` con database file nella cartella dati dell'app
- Servizio base `ILocalDatabase` con operazioni CRUD generiche
- Migrazioni/schema gestiti all'avvio tramite `CreateTableAsync`
- Accesso asincrono e thread-safe

**Note:** Separato dal secure storage. Contiene dati dell'applicazione (entità, cache, ecc.), non credenziali.

---

### FEAT-4.4 — Sincronizzazione Online/Offline

Meccanismo per mantenere i dati locali allineati con il backend quando la connessione è disponibile.

**Comportamento atteso:**
- Coda locale delle operazioni eseguite offline (create, update, delete)
- Sync automatica all'uscita dalla modalità offline
- Strategia di conflict resolution configurabile (default: server-wins)
- Indicatore visivo dello stato di sync (pending, in-progress, synced, error)

**Note:** Si appoggia su FEAT-4.3 per la coda e su FEAT-4.5 per rilevare il ritorno della connettività. Non deve bloccare l'UI durante la sync.

---

### FEAT-4.5 — Gestione Connettività (Online/Offline)

Rilevamento in tempo reale dello stato della rete con reazione dell'app.

**Comportamento atteso:**
- Servizio `IConnectivityService` che wrappa `Connectivity` di MAUI Essentials
- Evento/observable per i cambi di stato (online → offline e viceversa)
- Banner o snackbar non invasivo che informa l'utente quando è offline
- Le operazioni non disponibili offline vengono disabilitate o accodate (non mostrano errori HTTP generici)

**Note:** Questo servizio è usato da FEAT-4.4 per triggerare la sync e dai repository per decidere se fare chiamate HTTP o leggere solo da locale.

---

### FEAT-4.6 — Force Update

Meccanismo per forzare l'aggiornamento dell'app quando la versione installata è troppo vecchia.

**Comportamento atteso:**
- All'avvio, l'app interroga il backend per la versione minima supportata
- Se la versione installata è inferiore alla minima, viene mostrata una modale bloccante non chiudibile
- La modale ha un pulsante "Aggiorna ora" che apre lo store (App Store / Google Play)
- Versione minima configurabile lato backend senza rilasci

**Endpoint backend da aggiungere:** `GET /api/app/version` → `{ "minimumVersion": "1.2.0", "currentVersion": "1.5.0" }`

**Note:** Il check avviene solo all'avvio (cold start), non in background. La modale deve essere veramente bloccante — nessun modo di chiuderla o aggirarla.

---

### FEAT-4.7 — Push Notifications

Ricezione e gestione di notifiche push via FCM (Android) e APNS (iOS).

**Comportamento atteso:**
- Richiesta permesso notifiche al primo avvio (con spiegazione contestuale prima del prompt di sistema)
- Registrazione del device token sul backend all'autenticazione
- Deregistrazione del token al logout
- Gestione della notifica in foreground (snackbar/banner in-app)
- Navigazione alla schermata corretta al tap sulla notifica (con app in background o chiusa)
- Payload notifica standardizzato: `{ "title", "body", "type", "targetId" }`

**Note:** Il backend deve esporre endpoint per salvare/rimuovere device token. La logica di invio notifiche (FCM server SDK) è responsabilità del backend, non della mobile app.

---

### FEAT-4.8 — Crash Reporting & Analytics

Visibilità su crash e comportamento utente in produzione, senza strumentazione manuale pervasiva.

**Comportamento atteso:**
- Integrazione Sentry (SDK MAUI) per cattura automatica di eccezioni non gestite
- Breadcrumbs automatici per navigazione tra pagine
- Contesto utente allegato ai crash dopo il login (userId, versione app, piattaforma)
- Contesto rimosso al logout
- Un metodo `IAnalyticsService.Track(eventName, properties)` per eventi custom, con implementazione Sentry e implementazione no-op per i test

**Note:** Non usare AppCenter — è in dismissione. Sentry ha un piano free generoso e SDK MAUI maturo. Non loggare mai dati personali sensibili (token, password, contenuti utente).

---

## Dipendenze tra feature

```
FEAT-4.2 (SecureStorage)
    └── FEAT-4.1 (Auth)

FEAT-4.3 (SQLite)
    └── FEAT-4.4 (Sync)

FEAT-4.5 (Connectivity)
    └── FEAT-4.4 (Sync)

FEAT-4.1 (Auth)
    └── FEAT-4.7 (Push Notifications) — registra token dopo login
    └── FEAT-4.8 (Crash Reporting)    — allega contesto utente
```

**Ordine di implementazione consigliato:**
1. FEAT-4.2 Secure Storage
2. FEAT-4.1 Auth
3. FEAT-4.3 SQLite
4. FEAT-4.5 Connectivity
5. FEAT-4.4 Sync
6. FEAT-4.6 Force Update
7. FEAT-4.7 Push Notifications
8. FEAT-4.8 Crash Reporting

---

## Out of scope

- Biometria (FaceID/TouchID) — utile ma non universale
- Deep linking — aggiunge complessità di setup, valutare per progetto
- Localizzazione/i18n — dipende dal target
- Onboarding screens — UX specifica del prodotto
- In-app purchases, geolocation, camera — feature di dominio, non infrastruttura
