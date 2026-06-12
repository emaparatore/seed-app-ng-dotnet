# Troubleshooting

Common issues and solutions not tied to a specific documentation topic. For topic-specific troubleshooting, see the relevant document:
- [VPS Setup Guide - Troubleshooting](../getting-started/vps-setup-guide.md#troubleshooting-vps)
- [New Project Deploy Guide - Troubleshooting](../getting-started/new-project-deploy-guide.md#13-troubleshooting-primo-deploy)
- [SMTP Configuration - Troubleshooting](../modules/smtp-configuration.md#troubleshooting)
- [Migration Strategy - Rollback](../architecture/migration-strategy.md#rollback)
- [Admin Dashboard - Configurazione](../modules/admin-dashboard.md#configurazione-iniziale)

## Docker

### Cloudflare 521 — web server not responding after deploy

**Symptom:** Site returns Cloudflare error 521 ("Web server is down"). `docker ps` shows `postgres`, `api`, `seq`, and `web` running, but no `nginx` container.

**Cause:** The deploy script updates `api` and `web` individually with `--no-deps`. If `nginx` was never explicitly started (e.g. after a full stack restart, server reboot, or project rename), it won't be running.

**Fix (immediate):**
```bash
cd /opt/seed-app/production   # or /staging
docker compose -f docker-compose.deploy.yml up -d nginx
docker logs seed-production-nginx-1
```

**Root cause fix:** Added `docker compose up -d nginx` to the deploy script (PR #95) so nginx is always ensured on every deploy. See [deploy script behavior](ci-cd.md#3-deploy-deployyml) for details.

## Database

_No issues documented yet._

## General

_No issues documented yet._
