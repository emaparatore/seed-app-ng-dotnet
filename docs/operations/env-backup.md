# .env Backup

Daily backup of the `.env` file for production and staging environments via cron job.

## Backup location

```
/opt/seed-app/backups/
├── production/          # DB backups (existing)
├── staging/             # DB backups (existing)
└── env/
    ├── production/      # .env backups
    └── staging/         # .env backups
```

Files are named `.env.YYYYMMDD` (e.g. `.env.20260409`).

## Setup

On the VPS:

### 1. Create backup directories

```bash
mkdir -p /opt/seed-app/backups/env/production
mkdir -p /opt/seed-app/backups/env/staging
```

### 2. Add cron jobs

```bash
crontab -e
```

```cron
# Daily .env backup at 2:00 AM
0 2 * * * cp /opt/seed-app/production/.env /opt/seed-app/backups/env/production/.env.$(date +\%Y\%m\%d) 2>/dev/null
0 2 * * * cp /opt/seed-app/staging/.env /opt/seed-app/backups/env/staging/.env.$(date +\%Y\%m\%d) 2>/dev/null

# Cleanup .env backups older than 30 days at 3:00 AM
0 3 * * * find /opt/seed-app/backups/env -name ".env.*" -mtime +30 -delete 2>/dev/null
```

### 3. Verify

```bash
crontab -l
```

## Restore

```bash
# Production
cp /opt/seed-app/backups/env/production/.env.20260409 /opt/seed-app/production/.env

# Staging
cp /opt/seed-app/backups/env/staging/.env.20260409 /opt/seed-app/staging/.env
```

Then restart the services:

```bash
cd /opt/seed-app/<environment>
docker compose -f docker-compose.deploy.yml down
docker compose -f docker-compose.deploy.yml up -d
```
