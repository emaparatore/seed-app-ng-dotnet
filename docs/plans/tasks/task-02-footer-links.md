# Task 02: Footer con link Privacy Policy e Terms of Service

## Contesto

- **Stato attuale:** Non esiste un componente footer nell'app. Il template principale (`app.html`) contiene solo la toolbar top-nav, il `<router-outlet />` e il componente `<app-pwa-install-prompt />`.
- **Dipendenze:** T-01 (done) — le route `/privacy-policy` e `/terms-of-service` sono già registrate in `app.routes.ts` e i componenti esistono.
- **Pattern:** I componenti standalone dell'app usano `templateUrl`/`styleUrl`, selector con prefisso `app-`, e sono importati direttamente nel componente `App` (come `PwaInstallPrompt`).

## Piano di esecuzione

### File da creare

1. **`frontend/web/projects/app/src/app/footer/footer.ts`** — Componente standalone `AppFooter`
   - Selector: `app-footer`
   - Imports: `RouterLink` (da `@angular/router`)
   - Template inline o via `templateUrl` — un `<footer>` con due `<a routerLink>` a `/privacy-policy` e `/terms-of-service`
   - Stile minimale: testo centrato, font piccolo, padding, colore secondario, separatore visivo dal contenuto

2. **`frontend/web/projects/app/src/app/footer/footer.html`** — Template del footer
   - Struttura: `<footer class="app-footer">` con link separati da un separatore (es. `|` o `·`)
   - Testo link: "Privacy Policy" e "Terms of Service"

3. **`frontend/web/projects/app/src/app/footer/footer.scss`** — Stili del footer
   - Footer fissato in fondo (non sticky/fixed, ma in fondo al flusso del documento)
   - Testo centrato, font-size piccolo (0.85rem), padding verticale, colore attenuato
   - Link con colore inherit e underline on hover

4. **`frontend/web/projects/app/src/app/footer/footer.spec.ts`** — Test unitario
   - Test: componente si crea correttamente
   - Test: verifica presenza link a `/privacy-policy`
   - Test: verifica presenza link a `/terms-of-service`

### File da modificare

5. **`frontend/web/projects/app/src/app/app.ts`** — Aggiungere `AppFooter` agli imports del componente `App`

6. **`frontend/web/projects/app/src/app/app.html`** — Aggiungere `<app-footer />` dopo `<router-outlet />` (prima di `<app-pwa-install-prompt />`)

### Approccio tecnico

1. Creare il componente `AppFooter` con template e stili separati
2. Importare `RouterLink` per i link interni
3. Aggiungere il componente al template di `App` dopo il router-outlet
4. Scrivere test che verifichino la presenza dei link con i corretti `routerLink`
5. Eseguire `ng test app` e `ng build` per verificare

### Test da scrivere/verificare

- `footer.spec.ts`: 3 test (creazione, link privacy-policy, link terms-of-service)
- Verificare che i test esistenti dell'app continuino a passare

## Criteri di completamento

- Componente `AppFooter` creato e funzionante
- Footer visibile in tutte le pagine (inserito in `app.html` dopo `<router-outlet />`)
- Link a `/privacy-policy` e `/terms-of-service` presenti e con `routerLink` corretto
- Test unitari passano (`ng test app`)
- Build OK (`ng build`)

## Risultato

- File creati:
  - `frontend/web/projects/app/src/app/footer/footer.ts` — Componente standalone `AppFooter`
  - `frontend/web/projects/app/src/app/footer/footer.html` — Template con link a Privacy Policy e Terms of Service separati da `·`
  - `frontend/web/projects/app/src/app/footer/footer.scss` — Stili minimali: centrato, font 0.85rem, colore attenuato, underline on hover
  - `frontend/web/projects/app/src/app/footer/footer.spec.ts` — 3 test: creazione componente, link `/privacy-policy`, link `/terms-of-service`
- File modificati:
  - `frontend/web/projects/app/src/app/app.ts` — Aggiunto import `AppFooter` negli imports del componente
  - `frontend/web/projects/app/src/app/app.html` — Aggiunto `<app-footer />` dopo `<router-outlet />` e prima di `<app-pwa-install-prompt />`
- Scelte implementative:
  - Template e stili separati (`templateUrl`/`styleUrl`) per coerenza con il pattern degli altri componenti dell'app
  - Separatore `·` tra i link come indicato nel mini-plan
  - Footer in flusso del documento (non sticky/fixed), centrato con flexbox
  - Colore `rgba(0, 0, 0, 0.54)` per il testo attenuato, coerente con Material Design secondary text
- Nessuna deviazione dal piano
