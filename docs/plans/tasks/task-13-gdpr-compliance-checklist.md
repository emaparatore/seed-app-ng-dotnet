# Task 13: Checklist GDPR post-implementazione — Documentazione

## Contesto

### Stato attuale del codice rilevante

- **Privacy Policy e Terms of Service** placeholder gia creati in `frontend/web/projects/app/src/app/pages/privacy-policy/` e `terms-of-service/` (T-01) — contengono testo italiano con segnaposto `[bracketed]` da personalizzare
- **Consenso alla registrazione** implementato (T-03, T-04, T-05) — campi `PrivacyPolicyAcceptedAt`, `TermsAcceptedAt`, `ConsentVersion` su `ApplicationUser`
- **Export dati personali** implementato (T-08, T-09) — endpoint `GET /api/v1/auth/export-my-data`
- **Hard delete account** implementato (T-06, T-07) — `IUserPurgeService` con anonimizzazione audit log
- **Data retention** implementato (T-10, T-11) — `DataRetentionBackgroundService` con cleanup automatico
- **Re-accettazione consenso** implementato (T-12) — `ConsentVersion` check al login
- **SMTP configuration** documentata in `docs/smtp-configuration.md` — Brevo come provider produzione
- **README.md** ha una tabella indice docs alla riga ~215 con formato `| [Title](path) | Description |`
- **CLAUDE.md** ha una lista docs alla fine con formato `- \`docs/file.md\` — description`
- **Nessun file** `docs/gdpr-compliance-checklist.md` esiste ancora

### Dipendenze e vincoli

- Dipende da T-01 (placeholder privacy policy deve esistere) — gia completato
- E' un task puramente documentale — nessun codice da scrivere/testare
- Il documento deve essere utile al titolare del trattamento (non-tecnico) con azioni concrete
- Deve riferirsi ai componenti tecnici gia implementati (dove modificare il testo, dove trovare le configurazioni)
- Deve essere aggiunto sia in `README.md` (tabella indice) che in `CLAUDE.md` (lista docs)

## Piano di esecuzione

### Step 1: Creare `docs/gdpr-compliance-checklist.md`

**File:** `docs/gdpr-compliance-checklist.md` (NUOVO)

Struttura del documento:

```markdown
# GDPR Compliance Checklist

Introduzione: scopo del documento, prerequisiti tecnici gia implementati (link a FEAT-2)

## 1. Testo legale Privacy Policy e Terms of Service
- Cosa deve contenere la privacy policy (Art. 13-14 GDPR): identita titolare, finalita, base giuridica, destinatari, periodo conservazione, diritti interessato
- Suggerimento: usare generatori (iubenda.com, Termly) o consultare un legale
- Dove sostituire: `frontend/web/projects/app/src/app/pages/privacy-policy/privacy-policy.html` e `terms-of-service/terms-of-service.html`
- Checkbox di stato

## 2. Contatto privacy
- Decidere email dedicata (es. privacy@dominio.com)
- Indicarla nella privacy policy e nei Terms of Service
- Se necessario, nominare un DPO (Art. 37)
- Checkbox di stato

## 3. DPA con fornitori terzi
- Se si usa SMTP esterno (Brevo, Gmail), firmare un Data Processing Agreement
- Elencare altri fornitori che trattano dati personali (hosting, CDN, analytics)
- Link alla pagina DPA di Brevo e Google
- Checkbox di stato

## 4. Registro dei trattamenti (Art. 30)
- Cosa deve contenere: nome titolare, finalita, categorie di dati e interessati, destinatari, trasferimenti extra-UE, termini di cancellazione, misure di sicurezza
- Quando e obbligatorio (>250 dipendenti o trattamento non occasionale)
- Template semplificato
- Checkbox di stato

## 5. Informativa cookie (se applicabile)
- Verificare se il sito usa cookie non tecnici
- Se si, implementare cookie banner con consenso preventivo
- Checkbox di stato

## 6. Riepilogo funzionalita tecniche implementate
- Tabella con: funzionalita, riferimento tecnico, articolo GDPR
```

### Step 2: Aggiornare `README.md`

**File da modificare:** `README.md`

Aggiungere riga nella tabella indice docs (dopo la riga Monitoring o Troubleshooting):
```
| [GDPR Compliance Checklist](docs/gdpr-compliance-checklist.md) | Post-implementation checklist for GDPR compliance: legal text, DPA, data processing register |
```

### Step 3: Aggiornare `CLAUDE.md`

**File da modificare:** `CLAUDE.md`

Aggiungere alla lista docs:
```
- `docs/gdpr-compliance-checklist.md` — Post-implementation GDPR checklist: legal text, privacy contact, DPA, data processing register. Read when completing GDPR compliance or onboarding a data controller.
```

### Step 4: Verificare

```bash
# Nessun test necessario, solo verifica build frontend/backend non rotta
ng build  # (opzionale, nessun file di codice modificato)
```

## Criteri di completamento

- [ ] File `docs/gdpr-compliance-checklist.md` creato con tutte le sezioni richieste
- [ ] Sezione testo legale Privacy Policy e ToS con riferimenti Art. 13-14, suggerimento generatori, path dei file da modificare
- [ ] Sezione contatto privacy con email dedicata e menzione DPO
- [ ] Sezione DPA con fornitori terzi con riferimento a SMTP (Brevo/Gmail) e link utili
- [ ] Sezione registro trattamenti Art. 30 con contenuto, obbligatorieta, template
- [ ] Ogni sezione ha: descrizione azione, responsabile suggerito, risorse/link, checkbox di stato
- [ ] Riferimento aggiunto in `README.md` (tabella indice docs)
- [ ] Riferimento aggiunto in `CLAUDE.md` (lista docs)
- [ ] Nessun test necessario (solo documentazione)

## Risultato

- File modificati/creati:
  - `docs/gdpr-compliance-checklist.md` — **CREATO** — Checklist completa con 6 sezioni
  - `README.md` — **MODIFICATO** — Aggiunta riga nella tabella indice docs
  - `CLAUDE.md` — **MODIFICATO** — Aggiunta entry nella lista docs

- Scelte implementative e motivazioni:
  - Il documento è strutturato con sezioni numerate, ognuna con: descrizione dell'azione, responsabile suggerito, risorse/link utili, e checkbox di stato — come richiesto dal piano
  - I link ai DPA dei fornitori (Brevo, Google, Cloudflare) sono stati inclusi per rendere il documento immediatamente actionable
  - La tabella del registro trattamenti (Art. 30) include esempi concreti basati sulle funzionalità effettivamente implementate (periodi di retention, tipi di dati, misure di sicurezza)
  - La sezione 6 (riepilogo tecnico) mappa ogni funzionalità al relativo articolo GDPR per collegare implementazione tecnica e base normativa
  - La nota sulla `ConsentVersion` in sezione 1 collega la modifica del testo legale al meccanismo tecnico di re-accettazione (T-12)
  - Aggiunta sezione 5 sui cookie come previsto dal piano, con nota che l'app attuale usa solo cookie tecnici

- Eventuali deviazioni dal piano e perché:
  - Nessuna deviazione dal piano
