# Task T-01: Pagine Privacy Policy e Terms of Service — Frontend

## Contesto

- **Stato attuale:** Non esistono componenti né route per Privacy Policy e Terms of Service.
- **Routing:** `app.routes.ts` definisce le route dell'app. La wildcard `**` è l'ultima entry e fa redirect a `''`. Le nuove route vanno inserite prima del wildcard.
- **Pattern componenti:** I componenti pagina sono in `projects/app/src/app/pages/<nome>/` come standalone components con `templateUrl` e `styleUrl`. Non usano `mat-card` attualmente, ma il piano lo richiede.
- **Testing:** Vitest con `vi.fn()`, `TestBed`, `provideRouter([])`, `provideNoopAnimations()`. Vedi `login.spec.ts` come riferimento.
- **App shell:** `app.html` ha toolbar + `<router-outlet />` + `<app-pwa-install-prompt />`. Nessun footer attuale (T-02 lo aggiungerà dopo).

## Piano di esecuzione

### File da creare

1. **`projects/app/src/app/pages/privacy-policy/privacy-policy.ts`**
   - Componente standalone `PrivacyPolicy`
   - Import `MatCardModule` per il layout
   - Template inline o templateUrl con contenuto placeholder Privacy Policy
   - Contenuto: titoli sezione tipici GDPR (Titolare, Dati raccolti, Finalità, Base giuridica, Conservazione, Diritti, Contatto) con testo placeholder

2. **`projects/app/src/app/pages/privacy-policy/privacy-policy.html`**
   - Layout con `mat-card` contenente il testo placeholder strutturato
   - Titolo "Privacy Policy", sottosezioni con `<h2>`/`<h3>`

3. **`projects/app/src/app/pages/privacy-policy/privacy-policy.scss`**
   - Stile minimale: margini, max-width per leggibilità

4. **`projects/app/src/app/pages/privacy-policy/privacy-policy.spec.ts`**
   - Test: componente si crea
   - Test: il titolo "Privacy Policy" è presente nel DOM
   - Test: il contenuto placeholder è renderizzato

5. **`projects/app/src/app/pages/terms-of-service/terms-of-service.ts`**
   - Componente standalone `TermsOfService`, stessa struttura di PrivacyPolicy
   - Contenuto placeholder ToS (Accettazione, Uso del servizio, Account, Proprietà intellettuale, Limitazione responsabilità, Modifiche, Legge applicabile)

6. **`projects/app/src/app/pages/terms-of-service/terms-of-service.html`**
   - Layout con `mat-card`, testo placeholder ToS

7. **`projects/app/src/app/pages/terms-of-service/terms-of-service.scss`**
   - Stile coerente con privacy-policy

8. **`projects/app/src/app/pages/terms-of-service/terms-of-service.spec.ts`**
   - Test: componente si crea
   - Test: il titolo "Terms of Service" è presente
   - Test: contenuto placeholder renderizzato

### File da modificare

9. **`projects/app/src/app/app.routes.ts`**
   - Aggiungere route `privacy-policy` e `terms-of-service` prima del wildcard `**`
   - Lazy load con `loadComponent`, nessun guard

### Approccio step-by-step

1. Creare il componente `PrivacyPolicy` con template, stile e test
2. Creare il componente `TermsOfService` con template, stile e test
3. Aggiungere le due route in `app.routes.ts`
4. Eseguire `npm test` (o `ng test app`) per verificare che tutti i test passino
5. Verificare build con `ng build`

## Criteri di completamento

- [ ] Componente `PrivacyPolicy` esiste in `projects/app/src/app/pages/privacy-policy/`
- [ ] Componente `TermsOfService` esiste in `projects/app/src/app/pages/terms-of-service/`
- [ ] Route `/privacy-policy` e `/terms-of-service` registrate in `app.routes.ts` senza guard
- [ ] Contenuto placeholder leggibile e ben formattato con `mat-card`
- [ ] Test unitari per entrambi i componenti passano
- [ ] `ng test app` — all tests pass
- [ ] `ng build` — build OK

## Risultato

- File creati:
  - `projects/app/src/app/pages/privacy-policy/privacy-policy.ts` — componente standalone
  - `projects/app/src/app/pages/privacy-policy/privacy-policy.html` — template con sezioni GDPR placeholder
  - `projects/app/src/app/pages/privacy-policy/privacy-policy.scss` — stile layout legale
  - `projects/app/src/app/pages/privacy-policy/privacy-policy.spec.ts` — 3 test (create, titolo, sezioni)
  - `projects/app/src/app/pages/terms-of-service/terms-of-service.ts` — componente standalone
  - `projects/app/src/app/pages/terms-of-service/terms-of-service.html` — template con sezioni ToS placeholder
  - `projects/app/src/app/pages/terms-of-service/terms-of-service.scss` — stile layout legale
  - `projects/app/src/app/pages/terms-of-service/terms-of-service.spec.ts` — 3 test (create, titolo, sezioni)
- File modificati:
  - `projects/app/src/app/app.routes.ts` — aggiunte route lazy-loaded `/privacy-policy` e `/terms-of-service` prima del wildcard
- Scelte implementative:
  - Stile `.legal-container` / `.legal-card` separato dallo stile `.auth-container` / `.auth-card` usato nelle pagine auth, perche le pagine legali hanno layout diverso (max-width 800px vs 420px, nessun centramento verticale)
  - Contenuto placeholder in italiano coerente con il contesto GDPR del progetto, con segnaposto `[bracketed]` per i dati da personalizzare
  - SCSS identico per entrambe le pagine legali per coerenza visiva
- Nessuna deviazione dal piano
