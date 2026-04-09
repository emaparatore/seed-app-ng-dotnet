# Task 05: Consenso alla registrazione — Frontend

## Contesto

- **Stato attuale:** Il form di registrazione (`frontend/web/projects/app/src/app/pages/register/`) ha 5 campi (firstName, lastName, email, password, confirmPassword) e chiama `AuthService.register()` con un `RegisterRequest` che non include campi di consenso.
- **Backend pronto:** T-04 ha aggiunto `AcceptPrivacyPolicy` e `AcceptTermsOfService` al `RegisterCommand` backend con validazione FluentValidation. Il backend rifiuta registrazioni senza consenso.
- **Model attuale:** `RegisterRequest` in `shared-auth/src/lib/models/auth.models.ts` (riga 15-20) non include i campi di consenso.
- **AuthService:** `register()` in `shared-auth/src/lib/services/auth.service.ts` (riga 42-44) fa POST a `/auth/register` con il `RegisterRequest` attuale.
- **Test esistenti:** `register.spec.ts` ha 9 test con mock di `AuthService`. I test `setValue` del form dovranno includere il nuovo campo.

## Piano di esecuzione

### 1. Aggiornare `RegisterRequest` nel model
**File:** `frontend/web/projects/shared-auth/src/lib/models/auth.models.ts`
- Aggiungere `acceptPrivacyPolicy: boolean` e `acceptTermsOfService: boolean` a `RegisterRequest`

### 2. Aggiungere form control e checkbox al form di registrazione
**File:** `frontend/web/projects/app/src/app/pages/register/register.ts`
- Aggiungere import `MatCheckboxModule` agli imports del componente
- Aggiungere form control `acceptPrivacy: [false, [Validators.requiredTrue]]` al FormGroup
- Nel `onSubmit()`, mappare il valore di `acceptPrivacy` su entrambi i campi `acceptPrivacyPolicy` e `acceptTermsOfService` nel payload (una sola checkbox copre entrambi i consensi)

**File:** `frontend/web/projects/app/src/app/pages/register/register.html`
- Aggiungere dopo il campo confirmPassword e prima del button submit:
  ```html
  <mat-checkbox formControlName="acceptPrivacy" class="consent-checkbox">
    Ho letto e accetto la <a routerLink="/privacy-policy" target="_blank">Privacy Policy</a> e i <a routerLink="/terms-of-service" target="_blank">Terms of Service</a>
  </mat-checkbox>
  @if (form.controls.acceptPrivacy.touched && form.controls.acceptPrivacy.hasError('required')) {
    <mat-error class="consent-error">You must accept the Privacy Policy and Terms of Service</mat-error>
  }
  ```

**File:** `frontend/web/projects/app/src/app/pages/register/register.scss`
- Aggiungere stile `.consent-checkbox` con margin-bottom e font-size appropriati
- Aggiungere stile `.consent-error` per il messaggio di errore fuori dal mat-form-field

### 3. Aggiornare il payload nella chiamata API
**File:** `frontend/web/projects/app/src/app/pages/register/register.ts`
- In `onSubmit()`, dopo il destructuring di `confirmPassword`, mappare:
  ```typescript
  const { confirmPassword, acceptPrivacy, ...rest } = this.form.getRawValue();
  const request = { ...rest, acceptPrivacyPolicy: acceptPrivacy, acceptTermsOfService: acceptPrivacy };
  ```

### 4. Aggiornare i test
**File:** `frontend/web/projects/app/src/app/pages/register/register.spec.ts`
- Aggiungere test: "should have invalid form when acceptPrivacy is false" — verificare che il form sia invalido quando `acceptPrivacy` è `false`
- Aggiornare tutti i `form.setValue()` esistenti per includere `acceptPrivacy: true` (nei test di submit valido) o `acceptPrivacy: false` dove appropriato
- Aggiornare il test "should call AuthService.register on valid submit" per verificare che il payload includa `acceptPrivacyPolicy: true` e `acceptTermsOfService: true`

### 5. Verificare
- `ng test app` — tutti i test passano
- `ng build` — build OK

## Criteri di completamento
- Checkbox con link a Privacy Policy e Terms of Service visibile nel form di registrazione
- Form control `acceptPrivacy` con `Validators.requiredTrue` impedisce submit senza consenso
- Messaggio di errore visibile se checkbox non selezionata al submit
- Payload API include `acceptPrivacyPolicy: true` e `acceptTermsOfService: true`
- Test unitario verifica form invalido senza checkbox
- Test esistenti aggiornati e tutti passanti
- `ng test app` e `ng build` OK

## Risultato
- File modificati/creati
  - `frontend/web/projects/shared-auth/src/lib/models/auth.models.ts` — aggiunto `acceptPrivacyPolicy` e `acceptTermsOfService` a `RegisterRequest`
  - `frontend/web/projects/app/src/app/pages/register/register.ts` — aggiunto import `MatCheckboxModule`, form control `acceptPrivacy` con `Validators.requiredTrue`, mappatura payload con destructuring
  - `frontend/web/projects/app/src/app/pages/register/register.html` — aggiunta checkbox con link a Privacy Policy e Terms of Service, messaggio di errore condizionale
  - `frontend/web/projects/app/src/app/pages/register/register.scss` — aggiunto stile `.consent-checkbox` e `.consent-error`
  - `frontend/web/projects/app/src/app/pages/register/register.spec.ts` — aggiunto test "should have invalid form when acceptPrivacy is false", aggiornati tutti i `setValue` con `acceptPrivacy`, verificato payload con `acceptPrivacyPolicy` e `acceptTermsOfService`
- Scelte implementative e motivazioni
  - Una sola checkbox copre entrambi i consensi (privacy + terms) come da piano — semplifica UX, il testo include link a entrambi i documenti
  - Il payload mappa `acceptPrivacy` → `acceptPrivacyPolicy` + `acceptTermsOfService` nel destructuring di `onSubmit()` per allinearsi all'API backend
  - Test usa `expect.objectContaining` per verificare solo i campi di consenso senza duplicare l'intero payload
- Eventuali deviazioni dal piano e perche'
  - Nessuna deviazione dal piano
