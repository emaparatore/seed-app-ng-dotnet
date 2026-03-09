# Strategia Migration in Produzione

Questo documento descrive come vengono gestite le migrazioni EF Core in produzione.

## Panoramica

Le migrazioni vengono applicate automaticamente durante il deploy tramite un **EF Core Migration Bundle** — un eseguibile standalone incluso nell'immagine Docker dell'API. Il flusso è:

```
Push su master/dev
  → CI: build + test + validazione migrazioni
  → Docker Publish: build immagine API (include bundle migrazioni)
  → Deploy:
      1. Pull nuova immagine API
      2. Backup database (pg_dump compresso)
      3. Applica migrazioni (efbundle)
      4. Restart API
      5. Health check
      6. Pull/restart web (se cambiato)
```

Se la migrazione fallisce, lo script si interrompe e l'API vecchia resta attiva.

## Come funziona

### EF Core Migration Bundle

Il bundle viene generato nel Dockerfile dell'API (`backend/src/Seed.Api/Dockerfile`) durante la build:

```dockerfile
RUN dotnet ef migrations bundle \
    --project src/Seed.Infrastructure \
    --startup-project src/Seed.Api \
    -o /app/efbundle \
    --self-contained -r linux-x64 --force
```

Il bundle è un eseguibile Linux che:
- Contiene tutte le migrazioni dell'applicazione
- È **idempotente**: se il database è già aggiornato, non fa nulla
- Non richiede .NET SDK a runtime
- Viene copiato nell'immagine runtime a `/app/efbundle`

### Script di migrazione

Lo script `docker/scripts/migrate.sh` viene eseguito sulla VPS durante il deploy. Sequenza:

1. **Backup** — `pg_dump` compresso in `/opt/seed-app/backups/seeddb_YYYYMMDD_HHMMSS.sql.gz`
2. **Validazione** — verifica che il backup non sia vuoto
3. **Migrazione** — esegue il bundle via `docker compose run --rm --no-deps --entrypoint /app/efbundle api`
4. **Health check** — verifica `pg_isready`
5. **Cleanup** — rimuove backup più vecchi di 7 giorni

Il workflow `deploy.yml`:
- Copia `scripts/migrate.sh` sulla VPS via SCP
- Lo esegue dopo il `docker pull` e prima del `docker compose up -d`
- Dopo il restart, aspetta che `/health/ready` risponda (max 60 secondi)

### Validazione in CI

Il workflow `ci.yml` esegue due check aggiuntivi sulle PR:

1. **Pending model changes** — `dotnet ef migrations has-pending-model-changes` fallisce se un'entità è stata modificata senza generare la migrazione corrispondente
2. **Bundle build** — verifica che il bundle compili correttamente

## Creare una nuova migrazione

Da `backend/`:

```bash
# 1. Crea la migrazione
dotnet ef migrations add <NomeDescrittivo> \
  --project src/Seed.Infrastructure \
  --startup-project src/Seed.Api

# 2. Rivedi i file generati in src/Seed.Infrastructure/Migrations/

# 3. (Opzionale) Verifica che il bundle compili
dotnet ef migrations bundle \
  --project src/Seed.Infrastructure \
  --startup-project src/Seed.Api \
  -o efbundle --force

# 4. Commit e push — CI valida automaticamente
```

## Regole per migrazioni production-safe

### Sempre sicuro (un singolo deploy)

- Aggiungere una nuova tabella
- Aggiungere una colonna nullable
- Aggiungere una colonna con valore di default
- Aggiungere un indice (su tabelle piccole)

### Richiede due deploy (expand-contract pattern)

**Rinominare una colonna:**
1. Deploy 1: aggiungi nuova colonna, copia i dati, aggiorna il codice per scrivere su entrambe le colonne
2. Deploy 2: rimuovi la vecchia colonna

**Cambiare il tipo di una colonna:**
1. Deploy 1: aggiungi nuova colonna con il tipo corretto, migra i dati, aggiorna il codice
2. Deploy 2: rimuovi la vecchia colonna

### Richiede tre step

**Aggiungere una colonna NOT NULL a una tabella esistente:**
1. Aggiungi la colonna come nullable
2. Backfill i dati (via migration o script)
3. Altera la colonna a NOT NULL

### Mai in un singolo deploy

- Droppare una colonna usata dal codice in esecuzione
- Rinominare una tabella usata dal codice in esecuzione
- Cambiare il tipo di una colonna in-place su una tabella con dati

### Tabelle grandi (100K+ righe)

Usa SQL raw nelle migrazioni con `CONCURRENTLY`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(
        "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Orders_CreatedAt\" ON \"Orders\" (\"CreatedAt\");");
}
```

### Igiene

- **Non modificare mai** una migrazione già applicata a qualsiasi ambiente — crea sempre una nuova migrazione
- **Nomi descrittivi:** `AddOrdersTable`, `AddEmailIndexToUsers`, non `Update1` o `Fix`
- **Implementa sempre** sia `Up()` che `Down()` (utile per lo sviluppo locale, anche se in produzione non usiamo `Down()`)

## Rollback

Tre livelli, dal meno al più impattante:

### 1. La migrazione fallisce (più comune)

L'API vecchia resta attiva perché `migrate.sh` usa `set -euo pipefail` e il deploy script esegue le migrazioni **prima** di riavviare l'API. Il job GitHub Actions fallisce e il team viene notificato.

**Azione:** investiga l'errore, fixa la migrazione, ri-deploya.

### 2. Migrazione OK ma l'app è rotta

Re-deploya l'immagine API precedente. Se la migrazione è backward-compatible (come da linee guida sopra), il vecchio codice funzionerà con il nuovo schema.

```bash
# Sulla VPS
cd /opt/seed-app  # o /opt/seed-app/docker
sed -i 's/^IMAGE_TAG=.*/IMAGE_TAG=sha-<previous>/' .env
docker compose -f docker-compose.deploy.yml pull api
docker compose -f docker-compose.deploy.yml up -d --no-deps api
```

### 3. Dati corrotti (opzione nucleare)

Usa lo script di restore per ripristinare dal backup pre-migrazione:

```bash
cd /opt/seed-app  # o /opt/seed-app/docker
bash scripts/restore.sh /opt/seed-app/backups/seeddb_YYYYMMDD_HHMMSS.sql.gz
```

Lo script:
1. Stoppa l'API
2. Droppa e ricrea lo schema `public`
3. Ripristina il backup
4. Riavvia l'API

Richiede conferma interattiva (`yes/no`).

> **Nota:** Non usare i metodi `Down()` di EF Core per rollback in produzione. Sono fragili, non testati in pratica, e possono causare perdita di dati. Il restore da backup è più sicuro.

## Backup

I backup vengono creati automaticamente prima di ogni migrazione in `/opt/seed-app/backups/`:

```
/opt/seed-app/backups/
├── seeddb_20260309_143000.sql.gz
├── seeddb_20260310_120000.sql.gz
└── seeddb_20260311_090000.sql.gz
```

- Formato: `pg_dump` compresso con gzip
- Retention: 7 giorni (configurabile via `RETENTION_DAYS` in `migrate.sh`)
- Include: schema + dati, senza owner/privileges

### Backup manuale

```bash
cd /opt/seed-app  # o /opt/seed-app/docker
source .env
docker compose -f docker-compose.deploy.yml exec -T postgres \
  pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  --no-owner --no-privileges | gzip > /opt/seed-app/backups/seeddb_manual.sql.gz
```

## Setup iniziale sulla VPS

Da eseguire una sola volta:

```bash
sudo mkdir -p /opt/seed-app/backups
sudo chown deploy:deploy /opt/seed-app/backups
```

## File coinvolti

| File | Ruolo |
|------|-------|
| `backend/src/Seed.Api/Dockerfile` | Build del migration bundle nell'immagine API |
| `docker/scripts/migrate.sh` | Backup + migrazione automatica (usato dal deploy) |
| `docker/scripts/restore.sh` | Restore manuale da backup |
| `.github/workflows/deploy.yml` | SCP degli script + esecuzione migrazione + health check |
| `.github/workflows/ci.yml` | Validazione migrazioni nelle PR |
| `backend/src/Seed.Infrastructure/Migrations/` | File delle migrazioni EF Core |
