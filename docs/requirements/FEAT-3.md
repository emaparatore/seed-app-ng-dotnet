# FEAT-3: Subscription Plans & Payments (Stripe)

## Overview

L'applicazione deve supportare piani di abbonamento con pagamenti ricorrenti tramite Stripe. Il modulo è progettato come attivabile/disattivabile via configurazione, in modo che i progetti derivati dal seed possano decidere se includere la gestione abbonamenti o meno.

L'architettura segue il pattern già consolidato nel seed: interfaccia astratta (`IPaymentGateway`) con implementazione concreta Stripe e fallback mock per sviluppo locale. Stripe gestisce i pagamenti, le ricevute automatiche e la fiscalità (Stripe Tax per IVA EU). La fatturazione formale è su richiesta manuale dell'utente (modello B2C Italia/EU).

## Stato Attuale

| Area | Stato | Note |
|------|-------|------|
| Modello dati piani | Mancante | Nessuna entità Plan/Subscription |
| Payment gateway | Mancante | Nessuna integrazione pagamenti |
| Stripe integration | Mancante | Nessun SDK o webhook |
| Subscription guards | Mancante | Nessun middleware per plan-based access |
| Pricing page | Mancante | Nessuna UI pubblica per i piani |
| User subscription UI | Mancante | Nessuna sezione abbonamento nel profilo |
| Admin plans management | Mancante | Nessun CRUD piani nell'admin |
| Feature module toggle | Mancante | Nessun sistema di moduli attivabili |

## Decisioni Architetturali

### DA-1: Modulo attivabile via configurazione

Il modulo Payments è disattivato di default. Si attiva tramite `appsettings.json`:

```json
"Modules": {
  "Payments": {
    "Enabled": false,
    "Provider": "Stripe"
  }
}
```

Quando disabilitato:
- Le rotte API dei pagamenti non vengono registrate
- Le navigation guards frontend nascondono le pagine subscription
- I subscription guards backend passano sempre (nessun limite — l'app si comporta come se tutti avessero il piano massimo)

### DA-2: Interfaccia astratta IPaymentGateway

Come per `IEmailService`, il payment gateway è astratto:
- `IPaymentGateway` — interfaccia nel layer Application
- `StripePaymentGateway` — implementazione concreta in Infrastructure (quando `Provider = "Stripe"` e `Enabled = true`)
- `MockPaymentGateway` — fallback per sviluppo locale (quando `Enabled = true` ma nessun provider configurato, o per i test)

### DA-3: Stripe come source of truth per i pagamenti

Il database locale traccia lo stato della subscription (piano, date, stato), ma Stripe è la source of truth per tutto ciò che riguarda pagamenti, carte, e billing. La sincronizzazione avviene tramite webhook Stripe → API.

### DA-4: Stripe Checkout per il flusso di pagamento

Il checkout usa Stripe Checkout (redirect) anziché un form di pagamento embedded. Vantaggi:
- Stripe gestisce PCI compliance
- Supporto automatico per 3D Secure, Apple Pay, Google Pay
- Meno codice da mantenere
- Stripe genera automaticamente le ricevute

### DA-5: Stripe Customer Portal per self-service

La gestione dell'abbonamento (cambio carta, cancellazione, storico pagamenti) usa Stripe Customer Portal. L'utente viene reindirizzato al portale Stripe, evitando di reimplementare queste funzionalità.

### DA-6: Modello di billing B2C senza invoicing automatico

- Stripe genera ricevute automatiche per ogni pagamento
- L'utente può richiedere fattura formale manualmente (fornendo dati fiscali)
- Stripe Tax gestisce il calcolo IVA per vendite EU
- Nessun sistema di invoicing automatico nel seed

## Requisiti Funzionali

### RF-1: Modello dati dei piani

Il sistema deve supportare la definizione di piani di abbonamento con le seguenti informazioni:
- Nome, descrizione, prezzo mensile e annuale
- Lista di feature/limiti inclusi nel piano (es. "max 10 progetti", "supporto email", "API access")
- Stato (attivo/inattivo/archiviato)
- Ordinamento per la visualizzazione
- Riferimento al Product/Price ID di Stripe
- Flag per piano gratuito (free tier) e piano di default per nuovi utenti
- Flag per piano "più popolare" (evidenziato nella pricing page)
- Intervalli di billing supportati (mensile, annuale)

### RF-2: Gestione subscription utente

Il sistema deve tracciare la subscription attiva di ogni utente:
- Piano attuale e stato (active, trialing, past_due, canceled, expired)
- Date di inizio, rinnovo, scadenza
- Periodo di trial (configurabile per piano)
- Riferimento alla Stripe Subscription ID
- Storico delle subscription passate

### RF-3: Checkout e pagamento

Il sistema deve permettere all'utente di:
- Selezionare un piano dalla pricing page
- Essere reindirizzato a Stripe Checkout per completare il pagamento
- Tornare all'app dopo il pagamento (success/cancel URL)
- Ricevere conferma dell'attivazione del piano

### RF-4: Gestione self-service dell'abbonamento

L'utente autenticato deve poter:
- Visualizzare il proprio piano attuale e i dettagli della subscription
- Accedere a Stripe Customer Portal per: aggiornare metodo di pagamento, visualizzare storico pagamenti e ricevute, cancellare l'abbonamento
- Effettuare upgrade/downgrade del piano (con proration gestita da Stripe)

### RF-5: Webhook Stripe

Il sistema deve ricevere e gestire i seguenti eventi Stripe via webhook:
- `checkout.session.completed` — attivazione subscription dopo checkout
- `invoice.payment_succeeded` — conferma rinnovo riuscito
- `invoice.payment_failed` — pagamento fallito, subscription a rischio
- `customer.subscription.updated` — cambio piano, stato, o periodo
- `customer.subscription.deleted` — cancellazione subscription
- `customer.subscription.trial_will_end` — trial in scadenza (per notifica)

L'endpoint webhook deve:
- Validare la firma Stripe (webhook secret)
- Essere idempotente (gestire eventi duplicati)
- Loggare tutti gli eventi ricevuti
- Aggiornare lo stato della subscription locale

### RF-6: Subscription guards (backend)

Il sistema deve fornire un meccanismo di autorizzazione basato sul piano dell'utente:
- Attributo/policy `[RequiresPlan("Pro")]` per proteggere endpoint specifici
- Attributo/policy `[RequiresFeature("api-access")]` per proteggere in base a feature del piano
- Middleware per verificare che la subscription sia attiva (non scaduta, non canceled)
- Quando il modulo Payments è disabilitato, i guard passano sempre

### RF-7: Admin — gestione piani

L'amministratore deve poter:
- Creare, modificare e archiviare piani di abbonamento
- Definire feature/limiti per ogni piano
- Sincronizzare i piani con Stripe (creare/aggiornare Product e Price)
- Visualizzare la lista degli abbonamenti attivi
- Visualizzare metriche base: MRR (Monthly Recurring Revenue), numero abbonati per piano, churn

### RF-8: Trial period

Il sistema deve supportare periodi di prova:
- Durata trial configurabile per piano (in giorni, default 14)
- L'utente può iniziare un trial senza fornire metodo di pagamento (configurabile)
- Al termine del trial: se il metodo di pagamento è presente, si converte in subscription attiva; altrimenti la subscription scade
- Notifica prima della scadenza del trial (via email, se configurato)

### RF-9: Richiesta fattura manuale

L'utente deve poter richiedere una fattura formale per un pagamento effettuato:
- Form con dati fiscali (ragione sociale / nome, indirizzo, P.IVA / CF)
- La richiesta viene salvata nel database e notificata all'admin
- L'admin processa la richiesta manualmente (fuori dal sistema, per ora)
- Storico delle richieste di fattura visibile all'utente e all'admin

## Requisiti Non-Funzionali

### RNF-1: Sicurezza webhook

L'endpoint webhook Stripe deve validare la firma dell'evento usando il webhook signing secret. Gli eventi con firma non valida devono essere rifiutati con HTTP 400.

### RNF-2: Idempotenza eventi

Il sistema deve gestire eventi Stripe duplicati senza effetti collaterali. Ogni evento deve essere processato al massimo una volta (deduplicazione basata su event ID).

### RNF-3: Configurabilità

Tutti i parametri del modulo devono essere configurabili via `appsettings.json`:
- Abilitazione/disabilitazione modulo
- Chiavi API Stripe (publishable key, secret key, webhook secret)
- Durata trial di default
- URL di success/cancel per il checkout

### RNF-4: Resilienza

Se Stripe non è raggiungibile:
- Le operazioni di lettura (piano attuale, stato subscription) funzionano dal database locale
- Le operazioni di scrittura (checkout, portal) restituiscono errore con messaggio chiaro
- I webhook vengono ritentati automaticamente da Stripe

### RNF-5: Modularità

Il modulo Payments deve avere dipendenze minime con il resto dell'applicazione:
- Si integra con il sistema utenti esistente (relazione User → Subscription)
- Usa il sistema di audit log esistente per tracciare eventi
- Non introduce dipendenze circolari tra i layer

### RNF-6: Testabilità

- `MockPaymentGateway` consente test senza Stripe reale
- I webhook possono essere testati con Stripe CLI (`stripe listen --forward-to`)
- Integration test con Testcontainers non richiedono un account Stripe

## User Stories

#### US-001: Visualizzare i piani disponibili

**As a** visitatore del sito,
**I want** vedere i piani di abbonamento disponibili con prezzi e feature,
**So that** possa confrontarli e scegliere quello più adatto.

**Acceptance Criteria:**
- [x] Esiste una route `/pricing` accessibile senza autenticazione
- [x] La pagina mostra tutti i piani attivi con nome, descrizione, prezzo mensile/annuale
- [x] Ogni piano elenca le feature incluse (con icone check/cross)
- [x] Il piano "più popolare" è evidenziato visivamente
- [x] È presente un toggle mensile/annuale per la visualizzazione dei prezzi
- [x] Ogni piano ha un pulsante CTA (Call To Action) che porta al checkout (o al login se non autenticato)
- [x] Il piano free (se presente) ha un CTA "Inizia gratis" che porta alla registrazione

#### US-002: Sottoscrivere un piano a pagamento

**As a** utente autenticato,
**I want** sottoscrivere un piano a pagamento,
**So that** possa accedere alle feature premium.

**Acceptance Criteria:**
- [x] Cliccando "Scegli piano" dalla pricing page, l'utente viene reindirizzato a Stripe Checkout
- [X] La sessione Stripe Checkout è pre-compilata con l'email dell'utente
- [X] Dopo il pagamento riuscito, l'utente torna all'app su una pagina di conferma
- [X] La subscription è attiva e visibile nel profilo utente
- [X] L'audit log registra l'evento di sottoscrizione
- [X] Se l'utente annulla il checkout, torna all'app sulla pricing page

#### US-003: Visualizzare il proprio abbonamento

**As a** utente autenticato,
**I want** vedere i dettagli del mio abbonamento attuale,
**So that** possa sapere quale piano ho, quando si rinnova e quanto pago.

**Acceptance Criteria:**
- [X] Nel profilo utente esiste una sezione "Il mio abbonamento"
- [X] Mostra: nome del piano, stato, prezzo, prossima data di rinnovo
- [X] Se l'utente non ha un piano a pagamento, mostra il piano free o un invito a sottoscrivere
- [X] Sono presenti pulsanti per: gestire il pagamento, cambiare piano, cancellare

#### US-004: Gestire pagamento e cancellare abbonamento

**As a** utente abbonato,
**I want** poter aggiornare il mio metodo di pagamento o cancellare l'abbonamento,
**So that** possa gestire il mio account in autonomia.

**Acceptance Criteria:**
- [X] Il pulsante "Gestisci pagamento" reindirizza a Stripe Customer Portal
- [X] Da Stripe Customer Portal l'utente può: aggiornare la carta, vedere lo storico pagamenti, scaricare le ricevute
- [X] Il pulsante "Cancella abbonamento" mostra un dialog di conferma
- [X] Dopo la cancellazione, la subscription resta attiva fino alla fine del periodo pagato
- [X] Lo stato della subscription si aggiorna via webhook

#### US-005: Upgrade/downgrade del piano

**As a** utente abbonato,
**I want** poter cambiare il mio piano (upgrade o downgrade),
**So that** possa adattare il servizio alle mie esigenze.

**Acceptance Criteria:**
- [x] Dalla pagina del proprio abbonamento, l'utente può cliccare "Cambia piano"
- [x] Viene mostrata la lista dei piani disponibili con il piano attuale evidenziato
- [x] L'upgrade è immediato, con proration calcolata da Stripe
- [x] Il downgrade diventa effettivo alla fine del periodo di billing corrente (Stripe Subscription Schedules)
- [x] L'audit log registra il cambio piano

#### US-006: Trial period

**As a** nuovo utente,
**I want** provare un piano premium gratuitamente per un periodo limitato,
**So that** possa valutare il servizio prima di pagare.

**Acceptance Criteria:**
- [ ] Quando un piano ha un trial configurato, il CTA mostra "Prova gratis per X giorni"
- [ ] L'utente può iniziare il trial senza inserire un metodo di pagamento (configurabile)
- [ ] Durante il trial, l'utente ha accesso a tutte le feature del piano
- [ ] La sezione abbonamento mostra i giorni rimanenti del trial
- [ ] X giorni prima della scadenza del trial, l'utente riceve una notifica email (se email configurata)
- [ ] Al termine del trial senza pagamento, l'utente torna al piano free

#### US-007: Admin — CRUD piani di abbonamento

**As a** amministratore,
**I want** creare e gestire i piani di abbonamento,
**So that** possa definire l'offerta commerciale dell'applicazione.

**Acceptance Criteria:**
- [ ] Nell'area admin esiste una sezione "Piani" con la lista di tutti i piani
- [ ] L'admin può creare un nuovo piano: nome, descrizione, prezzo mensile/annuale, trial days, feature list
- [ ] L'admin può modificare un piano esistente (nome, descrizione, feature; il prezzo richiede nuovo Price su Stripe)
- [ ] L'admin può archiviare un piano (non è più selezionabile da nuovi utenti, quelli esistenti restano)
- [ ] La creazione/modifica sincronizza automaticamente Product e Price su Stripe
- [ ] L'admin può vedere quanti utenti sono abbonati a ciascun piano

#### US-008: Admin — dashboard abbonamenti

**As a** amministratore,
**I want** avere una panoramica degli abbonamenti e dei ricavi,
**So that** possa monitorare l'andamento del business.

**Acceptance Criteria:**
- [ ] Nell'area admin esiste una sezione "Abbonamenti" con metriche aggregate
- [ ] Metriche visualizzate: MRR (Monthly Recurring Revenue), numero abbonamenti attivi, abbonamenti in trial, tasso di churn
- [ ] Lista abbonamenti con filtri per piano e stato
- [ ] Dettaglio abbonamento: utente, piano, stato, date, storico pagamenti (da Stripe)

#### US-009: Webhook processing

**As a** sistema,
**I want** ricevere e processare gli eventi Stripe in tempo reale,
**So that** lo stato delle subscription nel database sia sempre sincronizzato con Stripe.

**Acceptance Criteria:**
- [ ] Esiste un endpoint `POST /webhooks/stripe` pubblico (no auth, validazione firma)
- [ ] L'endpoint valida la firma Stripe e rifiuta eventi con firma non valida (HTTP 400)
- [ ] Gli eventi supportati vengono processati e aggiornano lo stato della subscription locale
- [ ] Gli eventi duplicati vengono ignorati (deduplicazione su event ID)
- [ ] Tutti gli eventi ricevuti vengono loggati (tipo, ID, timestamp)
- [ ] Gli eventi non supportati vengono loggati e ignorati (HTTP 200, senza errore)

#### US-010: Subscription guard su endpoint

**As a** sviluppatore,
**I want** proteggere gli endpoint API in base al piano dell'utente,
**So that** solo gli utenti con il piano appropriato possano accedere a determinate funzionalità.

**Acceptance Criteria:**
- [ ] Esiste un attributo `[RequiresPlan("Pro", "Enterprise")]` applicabile a controller/action
- [ ] Esiste un attributo `[RequiresFeature("feature-key")]` per controllo granulare
- [ ] Se l'utente non ha il piano richiesto, l'endpoint restituisce HTTP 403 con messaggio chiaro
- [ ] Se il modulo Payments è disabilitato, i guard passano sempre (nessuna restrizione)
- [ ] I guard verificano che la subscription sia in stato attivo (non scaduta/cancellata)

#### US-011: Feature gating frontend

**As a** sviluppatore frontend,
**I want** mostrare/nascondere elementi UI in base al piano dell'utente,
**So that** l'interfaccia rifletta le feature disponibili per il piano attuale.

**Acceptance Criteria:**
- [ ] Esiste una directive/pipe Angular `*requiresPlan="'Pro'"` per conditional rendering
- [ ] Esiste un service `SubscriptionService` che espone il piano attuale come signal
- [ ] Le route protette da piano hanno un guard Angular che reindirizza alla pricing page
- [ ] Quando il modulo Payments è disabilitato, tutte le feature sono visibili (nessun gating)
- [ ] Il piano dell'utente viene incluso nella risposta di `/auth/me` (o endpoint dedicato)

#### US-012: Richiesta fattura manuale

**As a** utente abbonato,
**I want** richiedere una fattura formale per un pagamento effettuato,
**So that** possa avere documentazione fiscale per le mie spese.

**Acceptance Criteria:**
- [ ] Nella sezione abbonamento del profilo, è presente un pulsante "Richiedi fattura"
- [ ] Il form richiede: tipo (persona fisica / azienda), nome/ragione sociale, indirizzo completo, codice fiscale e/o P.IVA, codice SDI/PEC (opzionali)
- [ ] I dati fiscali vengono salvati nel profilo utente per riutilizzo futuro
- [ ] La richiesta viene salvata con riferimento al pagamento Stripe
- [ ] L'admin riceve una notifica (audit log + eventuale email) della richiesta
- [ ] L'utente può vedere lo storico delle proprie richieste di fattura e il loro stato (richiesta, in lavorazione, emessa)

## Dipendenze tra User Stories

- **US-002** dipende da **US-001** (pricing page) e **US-009** (webhook per attivazione)
- **US-003, US-004, US-005** dipendono da **US-002** (subscription attiva)
- **US-006** dipende da **US-002** (checkout flow con trial)
- **US-007** è propedeutico a **US-001** (servono i piani nel DB)
- **US-008** dipende da **US-007** e **US-009** (dati da mostrare)
- **US-009** è trasversale, necessario per tutti i flussi di subscription
- **US-010** e **US-011** sono indipendenti dal checkout, dipendono solo dal modello dati
- **US-012** dipende da **US-002** (deve esistere un pagamento)

## Ordine di implementazione suggerito

1. **Infrastruttura base**: modello dati, `IPaymentGateway`, configurazione modulo, migration
2. **Admin CRUD piani** (US-007): necessario per avere piani nel sistema
3. **Webhook handler** (US-009): necessario per sincronizzare gli stati
4. **Pricing page** (US-001): prima UI pubblica
5. **Checkout flow** (US-002): flusso di pagamento
6. **Subscription UI** (US-003, US-004, US-005): gestione self-service
7. **Trial** (US-006): trial period
8. **Guards** (US-010, US-011): protezione basata sul piano
9. **Admin dashboard** (US-008): metriche e overview
10. **Fattura manuale** (US-012): richiesta fattura

## Fuori Scope

- **Invoicing automatico**: nessun sistema di generazione fatture automatiche. Le ricevute sono gestite da Stripe, le fatture formali su richiesta manuale.
- **Metered billing / pay-per-use**: modello a consumo non supportato in questa implementazione.
- **Multi-currency avanzato**: Stripe gestisce le valute, ma l'app mostra i prezzi in una sola valuta (EUR).
- **Coupon e promozioni**: Stripe li supporta nativamente, ma la UI per crearli/gestirli non è in scope. Possono essere applicati direttamente da Stripe Dashboard.
- **Marketplace / multi-vendor**: nessun supporto per Stripe Connect o pagamenti multi-party.
- **Notifiche in-app**: le notifiche avvengono via email (se configurata) e audit log. Un sistema di notifiche in-app è un feature separato.
- **Dunning management avanzato**: Stripe gestisce i retry automatici per pagamenti falliti. Nessuna logica aggiuntiva nel seed.

## Open Questions

1. ~~**Free tier obbligatorio?**~~ **DECISO:** Sì, il free tier è obbligatorio quando il modulo Payments è attivo. Ogni applicazione deve avere un piano gratuito di default assegnato automaticamente ai nuovi utenti alla registrazione. Motivazione: senza free tier il flusso di registrazione andrebbe ripensato completamente (checkout obbligatorio post-signup o muro di accesso), aggiungendo complessità significativa e rompendo la modularità del modulo Payments. Chi vuole incentivare il pagamento può rendere il free tier molto limitato, ottenendo lo stesso effetto senza complicare il flusso.

2. ~~**Trial senza carta?**~~ **DECISO:** Il trial richiede sempre un metodo di pagamento. È lo standard di settore, garantisce conversione più alta, scoraggia account usa e getta, e semplifica il codice (il trial con carta è nativo in Stripe, nessuna logica custom per gestire scadenza senza pagamento).

3. ~~**Stripe Tax**~~ **DECISO:** Fuori scope per ora. Le applicazioni iniziali saranno gestite con P.IVA regime forfettario (esente IVA), quindi le ricevute automatiche di Stripe sono sufficienti. Stripe Tax è aggiungibile in futuro senza modifiche architetturali (si attiva a livello di account Stripe e nelle sessioni di checkout).

4. ~~**Soft delete subscription**~~ **DECISO:** Alla cancellazione account (GDPR): (a) cancellare la subscription Stripe attiva tramite API, (b) cancellare il Customer su Stripe tramite API (Stripe lo marca "deleted" ma mantiene le transazioni per compliance), (c) anonimizzare la subscription nel DB locale (rimuovere il legame con l'utente, mantenere dati aggregati per contabilità), (d) conservare le richieste di fattura per 10 anni (obbligo fiscale italiano) anonimizzando i dati personali non strettamente necessari. Coerente con il pattern soft delete + anonimizzazione di FEAT-2.

5. ~~**Webhook endpoint path**~~ **DECISO:** Usare `/webhooks/stripe`, fuori dal versioning API. Il formato del webhook è dettato da Stripe e non cambia con le versioni dell'API dell'applicazione. L'endpoint è isolato: non passa per l'auth middleware (valida la firma Stripe internamente) e non ha dipendenze dal versioning dei controller.
