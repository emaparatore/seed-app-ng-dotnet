# Requirements Workflow — Guida Pratica

Guida all'uso della skill `requirements-workflow` con Claude Code.
Esempi di prompt per ogni fase del workflow.

---

## Setup

### Installazione

Copia la cartella `requirements-workflow` dentro `.claude/skills/` nella
root del progetto:

```
tuo-progetto/
  .claude/
    skills/
      requirements-workflow/
        SKILL.md
        references/
          requirements-guide.md
          plan-guide.md
          adr-guide.md
```

Claude Code la rileva automaticamente, non serve configurazione aggiuntiva.

### Aggiunta consigliata al CLAUDE.md

Non è obbligatoria, ma aiuta Claude Code a lavorare meglio con il workflow:

```markdown
## Feature Implementation Workflow

Quando si implementa una nuova feature:
- I requisiti strutturati vanno in `docs/requirements/FEAT-<n>.md`
- I piani di implementazione vanno in `docs/plans/PLAN-<n>.md`
- Le decisioni architetturali vanno in `docs/decisions/ADR-<NNN>-<titolo>.md`
- Lavora un task alla volta, aggiorna il piano dopo ogni task completato
- Non procedere oltre un task con ⚠️ DECISION REQUIRED senza approvazione

## Dimensionamento dei task
Prima di iniziare l'esecuzione, valuta il peso dei prossimi task:
- Task leggero (1-3 file, logica lineare): raggruppa fino a 3-4 per sessione
- Task medio (4-8 file, cross-layer): massimo 1-2 per sessione
- Task pesante (refactoring, molti file, debugging): 1 per sessione
```

### Struttura dei documenti prodotti

```
docs/
  requirements/
    FEAT-001-gestione-utenti.md     ← requisiti strutturati
  plans/
    PLAN-001-gestione-utenti.md     ← piano di implementazione (vivente)
  decisions/
    ADR-001-cursor-pagination.md    ← decisioni architetturali
```

---

## Phase 1 — Da desiderata a requisiti strutturati

Scrivi i tuoi desideri in un file (anche informale, anche in italiano) e
passalo a Claude Code.

### Prompt per creare i requisiti

```
Ho scritto i miei desiderata in docs/wishes/gestione-utenti.md.
Aiutami a trasformarli in requisiti strutturati.
Solo Phase 1, non procedere con il piano.
```

Claude Code produrrà `docs/requirements/FEAT-001-gestione-utenti.md` con
requisiti funzionali, non funzionali, user stories con acceptance criteria,
dipendenze, e una sezione di open questions.

### Rispondere alle open questions

Apri il file dei requisiti e rispondi direttamente sotto ogni domanda:

```markdown
### Open Questions

1. **Notifications:** US-005 implies email notifications — is a notification
   system in scope for this feature?

   → Sì, usiamo IEmailService già presente. Solo email per ora.

2. **Roles:** Which capabilities are admin-only?

   → Gestione utenti e configurazione sistema. Il resto è per tutti.
```

Poi torna in Claude Code:

```
Ho risposto alle open questions in docs/requirements/FEAT-001-gestione-utenti.md.
Aggiorna i requisiti e le user stories in base alle mie risposte.
```

---

## Phase 2 — Dal requisiti al piano di implementazione

### Prompt per creare il piano

```
Crea il piano di implementazione partendo da
docs/requirements/FEAT-001-gestione-utenti.md
```

Se hai già risposto alle open questions e vuoi che faccia tutto insieme:

```
Ho risposto alle open questions in docs/requirements/FEAT-001-gestione-utenti.md.
Aggiorna i requisiti in base alle mie risposte e poi crea il piano
di implementazione. Aggiorna anche i decision gate collegati e crea ADR
se servono.
```

Claude Code produrrà `docs/plans/PLAN-001-gestione-utenti.md` con la
story coverage table, i task in sequenza, le definition of done, e i
decision gate marcati con ⚠️.

### Rivedere il piano

Leggi il piano con attenzione. Controlla:
- La sequenza dei task ha senso?
- I task sono abbastanza piccoli? (3-7 punti nella definition of done)
- I decision gate coprono le scelte che vuoi controllare?
- Ogni user story è coperta da almeno un task?

---

## Phase 3 — Esecuzione

### Prompt per avviare l'esecuzione

Il modo più efficiente è dare autonomia con regole di stop chiare:

```
Esegui il piano in docs/plans/PLAN-001-gestione-utenti.md partendo
dal primo task incompleto. Procedi task per task, aggiorna il piano
dopo ciascuno. Fermati ai decision gate e quando un task risulta
più grande del previsto.
```

### Riprendere dopo un'interruzione

```
Continua il piano PLAN-001 dal prossimo task incompleto. Fai 2-3 task.
```

Il piano aggiornato fa da ponte tra sessioni — Claude Code lo rilegge e
sa dove riprendere.

### Limitare il lavoro per sessione

Se vuoi controllare quanto lavoro fare in una sessione:

```
Esegui i prossimi 2 task dal piano PLAN-001.
```

Regola pratica per il dimensionamento:
- Task leggeri (1-3 file): 3-4 per sessione
- Task medi (4-8 file, cross-layer): 1-2 per sessione
- Task pesanti (refactoring, molti file): 1 per sessione

### Monitorare l'avanzamento

In qualsiasi momento puoi aprire il piano e guardare la story coverage
table in cima per vedere lo stato di ogni user story:

```markdown
| Story  | Description        | Tasks      | Status         |
|--------|--------------------|------------|----------------|
| US-001 | User can register  | T-01, T-02 | ✅ Done         |
| US-002 | User can log in    | T-03       | 🔄 In Progress |
| US-003 | Admin can manage   | T-05, T-06 | ⏳ Not Started  |
```

Se una storia è "Done", tutti i task collegati sono completati e i test
passano.

### Quando Claude Code incontra un decision gate

Claude Code si ferma e ti presenta la decisione con opzioni e trade-off.
Tu decidi, lui aggiorna il piano e l'ADR, e prosegue.

---

## Phase 4 — Chiusura

### Prompt per la verifica finale

```
Tutti i task in PLAN-001 sono completati. Fai la verifica di chiusura.
```

Claude Code:
- Verifica che ogni user story e ogni acceptance criterion abbia un test
- Riepiloga tutte le decisioni architetturali (ADR) prese
- Documenta eventuali cambiamenti di scope rispetto al piano originale
- Aggiunge un sommario in cima al piano con data e stato finale

Il piano completato diventa la documentazione permanente della feature.

---

## Riepilogo dei prompt essenziali

| Fase | Prompt |
|------|--------|
| Strutturare requisiti | `Ho i miei desiderata in docs/wishes/X.md. Trasformali in requisiti strutturati.` |
| Aggiornare dopo open questions | `Ho risposto alle open questions in FEAT-001. Aggiorna requisiti e piano.` |
| Creare il piano | `Crea il piano di implementazione da docs/requirements/FEAT-001.md` |
| Avviare esecuzione | `Esegui il piano PLAN-001 dal primo task incompleto. Fermati ai decision gate.` |
| Riprendere | `Continua il piano PLAN-001 dal prossimo task incompleto. Fai 2-3 task.` |
| Controllare stato | Apri il piano e guarda la story coverage table |
| Chiusura | `Tutti i task in PLAN-001 sono completati. Fai la verifica di chiusura.` |
