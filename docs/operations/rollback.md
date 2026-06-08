# Rollback Guide

Strategie per tornare a uno stato funzionante dopo un deploy problematico.

---

## Panoramica

Il sistema di deploy applica le migrazioni DB **prima** di riavviare l'API. Questo crea una finestra di sicurezza naturale: se la migrazione fallisce, l'API vecchia resta attiva senza interruzioni. I tre scenari di rollback coprono tutto il resto.

---

## Scenario 1 — La migrazione fallisce

**Cosa succede:** `migrate.sh` usa `set -euo pipefail` e il bundle di migrazione esegue ogni migrazione in una transazione. Se una migrazione fallisce, Postgres fa rollback della transazione, il DB resta intatto, il job GitHub Actions fallisce e il deploy non procede.

**L'API vecchia è ancora in esecuzione. Non serve fare nulla di operativo.**

**Azione:**
1. Leggi i log del job GitHub Actions per capire l'errore
2. Fixa la migrazione (crea una nuova migrazione correttiva, non modificare quella esistente)
3. Ri-deploya tramite il normale flusso CI/CD

---

## Scenario 2 — Migrazione OK, app rotta

La migrazione è andata a buon fine ma l'API nuova non funziona (crash all'avvio, errori runtime, comportamento errato).

Se hai seguito le regole di migrazione production-safe (expand-contract, mai `DROP` su colonne in uso), il vecchio codice è ancora compatibile con il nuovo schema. Puoi tornare all'immagine Docker precedente **senza toccare il DB**.

### Rollback rapido — modifica `.env` sulla VPS

```bash
cd /opt/seed-app/production   # o /opt/seed-app/staging

# Imposta il tag SHA del commit precedente
# (leggibile dalla history di GitHub Actions o dal .env di staging)
sed -i 's/^API_IMAGE_TAG=.*/API_IMAGE_TAG=sha-<commit-precedente>/' .env

# L'immagine è già su GHCR con quel tag SHA
docker compose -f docker-compose.deploy.yml pull api
docker compose -f docker-compose.deploy.yml up -d --no-deps api
```

Tempo di ripristino: ~30 secondi.

> **Nota:** questo non cambia git. Al prossimo deploy CI, il `.env` verrà sovrascritto con il nuovo tag. Assicurati di fare anche un revert commit (vedi sotto) per evitare che il problema venga ridistribuito.

### Fix permanente — revert commit

```bash
git revert <commit-sha>
git push origin master   # → CI → Docker Publish → Deploy automatico
```

Il revert crea un nuovo commit che annulla le modifiche, mantiene la history pulita e attiva l'intero flusso CI/CD. Usalo quando non è un'emergenza immediata o dopo aver già fatto il rollback rapido sulla VPS.

### Flusso consigliato in un'emergenza

1. **Subito:** modifica `.env` sulla VPS → app torna su in 30 secondi
2. **Dopo:** `git revert` su `master` → il CI rideploya la versione corretta in modo pulito
3. Il prossimo deploy sovrascrive il `.env` con il tag corretto

---

## Scenario 3 — Dati corrotti (opzione nucleare)

Usare solo se la migrazione ha corrotto o perso dati in modo irreversibile. Prima di procedere, verifica che un rollback dell'immagine Docker (Scenario 2) non sia sufficiente.

Il backup `pg_dump` viene creato automaticamente prima di ogni migrazione in `/opt/seed-app/backups/<env>/`.

```bash
cd /opt/seed-app/production   # o /opt/seed-app/staging

# Elenca i backup disponibili
ls -lh /opt/seed-app/backups/production/

# Esegui il restore (chiede conferma interattiva)
bash scripts/restore.sh /opt/seed-app/backups/production/seeddb_YYYYMMDD_HHMMSS.sql.gz
```

Lo script:
1. Chiede conferma (`yes/no`)
2. Stoppa l'API
3. Droppa e ricrea lo schema `public`
4. Ripristina il dump
5. Riavvia l'API

> **Attenzione:** i dati scritti **dopo** il deploy vengono persi. Questo è inevitabile in qualunque scenario di rollback del DB.

### Perché non usare il `Down()` di EF Core

EF Core genera un metodo `Down()` in ogni migrazione, ma non è adatto alla produzione:

- Se la migrazione ha aggiunto una colonna e l'app ha già scritto dati, `Down()` fa `DROP COLUMN` e quei dati sono persi
- Il `Down()` non è mai testato nel flusso normale di CI/CD e può contenere errori
- Il backup `pg_dump` è uno snapshot esatto e affidabile del DB pre-migrazione

Il `Down()` è utile in sviluppo locale per tornare indietro tra branch, non in produzione.

---

## Riepilogo

| Scenario | Tocca git | Tocca DB | Tempo | Quando |
|---|---|---|---|---|
| Migrazione fallisce | No | No | — | Automatico, nessuna azione necessaria |
| Modifica `.env` + restart | No | No | ~30 sec | Emergenza immediata, app rotta |
| `git revert` + CI | Sì | No | ~5-10 min | Fix permanente / post-emergenza |
| `restore.sh` dal backup | No | **Sì** | ~2-5 min | Dati corrotti (rarissimo) |

---

## Dove trovare il tag SHA precedente

- **GitHub Actions:** tab `Actions` → workflow `Deploy` → run precedente → step "Set image tags"
- **Staging `.env`:** se production è rotto ma staging funziona, il tag di staging è un punto di riferimento
- **GHCR:** `ghcr.io/<owner>/<repo>/api` — lista dei tag disponibili

---

## File coinvolti

| File | Ruolo |
|---|---|
| `docker/scripts/migrate.sh` | Backup + migrazione automatica prima del deploy |
| `docker/scripts/restore.sh` | Restore manuale da backup |
| `/opt/seed-app/backups/<env>/` | Backup automatici (retention 7 giorni) |
| `/opt/seed-app/<env>/.env` | Tag SHA delle immagini deployate, modificabile per rollback rapido |
