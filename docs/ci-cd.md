# CI/CD Pipeline

## Branch Strategy

```
master        ← production (always deployable)
  ↑ PR
dev           ← staging / integration
  ↑ PR
feature/*     ← feature branches

hotfix/*      → PR direct to master (auto back-merge to dev)
```

| Branch | Purpose | Deploys to |
|--------|---------|------------|
| `master` | Production-ready code | Production |
| `dev` | Integration / staging | Staging |
| `feature/*` | New features | - |
| `hotfix/*` | Urgent fixes | Production (then back-merged to dev) |

### Flow

1. Create `feature/my-feature` from `dev`
2. Open PR `feature/my-feature` → `dev` (CI runs)
3. Merge to `dev` (Docker images pushed with `dev` tag)
4. Open PR `dev` → `master` (CI runs)
5. Merge to `master` (Docker images pushed with `latest` tag, deploy to production)

### Hotfix Flow

1. Create `hotfix/fix-name` from `master`
2. Open PR `hotfix/fix-name` → `master` (CI runs)
3. Merge to `master` (deploy to production)
4. Automatic PR `master` → `dev` is created for back-merge

## Workflows

### 1. CI (`ci.yml`)

**Trigger:** Pull requests targeting `dev` or `master`

**Jobs:**

| Job | Runs when | What it does |
|-----|-----------|--------------|
| `changes` | Always | Detects which paths changed (`backend/`, `frontend/web/`) |
| `backend-build-test` | Backend changed | Restore, build, unit tests, integration tests |
| `frontend-build-test` | Frontend changed | npm ci, build shared libs, tests, build app |
| `ci-success` | Always | Gate job - fails if any test job failed, passes if all passed or were skipped |

**Path filtering:** If a PR only changes frontend code, the backend job is skipped (and vice versa). The `ci-success` gate job handles this correctly - skipped jobs don't block the PR.

**Branch protection:** `ci-success` is the only required check on both `dev` and `master`. This avoids issues with skipped jobs not reporting status.

### 2. Docker Publish (`docker-publish.yml`)

**Trigger:** Push to `master` or `dev` (after PR merge), or manual `workflow_dispatch`

**Manual trigger inputs:**

| Input | Type | Description |
|-------|------|-------------|
| `force_api` | boolean | Force API image rebuild (bypasses path filter) |
| `force_web` | boolean | Force Web image rebuild (bypasses path filter) |

On push, only changed images are rebuilt (path filtering). On manual trigger, you can selectively force rebuild one or both images — useful for the first deploy when no images exist on GHCR yet.

**Registry:** GitHub Container Registry (`ghcr.io`)

**Tagging:**

| Branch | Tags |
|--------|------|
| `master` | `latest`, `sha-<short>`, semver (if git tag `v*` exists) |
| `dev` | `dev`, `dev-sha-<short>` |

**Images:**
- `ghcr.io/<owner>/<repo>/api` - Backend API
- `ghcr.io/<owner>/<repo>/web` - Frontend web

Path filtering applies here too — only changed images are rebuilt on push. Use `workflow_dispatch` with force inputs to bypass this.

### 3. Deploy (`deploy.yml`)

**Trigger:** After successful Docker Publish workflow

**Active method:** Docker Compose on VPS via SSH — pulls images from GHCR, runs EF Core migrations, deploys with health checks, and prunes old images.

**Deploy script behavior — partial updates + infrastructure ensure:**
The script updates only the services that were rebuilt in the current run (`api` and/or `web`) using `--no-deps`. After those updates, it always runs `docker compose up -d nginx` to ensure infrastructure services are up. This means:
- `nginx` is started if it isn't running (e.g., first deploy, server restart, container crash)
- `postgres` and `seq` are started as dependencies of `nginx` → `web` → `api` on the first `up`

> **Important:** If you add a new infrastructure service to `docker-compose.deploy.yml` (e.g. Redis, a background worker), make sure to add an explicit `docker compose up -d <service>` in the deploy script — otherwise it will only start when the full stack is restarted manually.

Alternative options (commented out in the workflow):
- **Option B:** Azure Container Apps
- **Option C:** Kubernetes (kubectl)

Uses GitHub Environments:
- `staging` - auto-deploy on `dev` push → deploys to `/opt/seed-app/staging/`
- `production` - deploy on `master` push (configure required reviewers if needed) → deploys to `/opt/seed-app/production/`

**Dual-environment deploy paths:**

| Branch | Environment | Deploy dir | Backup dir | Image tag prefix |
|--------|-------------|------------|------------|------------------|
| `master` | production | `/opt/seed-app/production` | `/opt/seed-app/backups/production` | `sha-` |
| `dev` | staging | `/opt/seed-app/staging` | `/opt/seed-app/backups/staging` | `dev-sha-` |

Il CI scrive il tag SHA immutabile del commit deployato (es. `sha-6d7da25`) nel `.env` del VPS, separatamente per ogni servizio (`API_IMAGE_TAG`, `WEB_IMAGE_TAG`). Solo i servizi effettivamente rebuildati vengono aggiornati.

The CI creates the directory structure and copies these files to the deploy dir on each run:
- `docker/docker-compose.deploy.yml`
- `docker/scripts/migrate.sh`, `seed.sh`, `restore.sh`
- `docker/nginx/nginx.conf` and `docker/nginx/templates/*`

No manual file copying is needed — even on the first deploy, the CI handles everything. The only prerequisite on the VPS is the root directory (`/opt/seed-app/` owned by the deploy user) and the `.env` file, which is **never overwritten by CI** and must be created manually once (see [VPS Setup Guide](vps-setup-guide.md#6-configurazione-delle-variabili-dambiente)).

### 4. Hotfix Back-merge (`hotfix-backmerge.yml`)

**Trigger:** PR from `hotfix/*` merged into `master`

**Action:** Automatically creates a PR `master` → `dev` to sync changes back.

## Branch Protection Rules

Both `dev` and `master` have these rules:

- Require pull request before merging (0 approvals for solo dev, increase for teams)
- Require `ci-success` status check to pass
- Require conversation resolution before merging
- No force pushes
- No deletions
- Do not allow bypassing (applies to admins too)

## GitHub Settings Required

### Actions Permissions

**Settings > Actions > General > Workflow permissions:**
- "Allow GitHub Actions to create and approve pull requests" must be enabled (needed for hotfix back-merge)

### Environments

**Settings > Environments:**
- Create `staging` environment (no required reviewers — auto-deploy)
- Create `production` environment (add required reviewers when working in a team)

> Both environments use the same repository secrets (`DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `GHCR_TOKEN`) since they deploy to the same VPS. The deploy dir is determined automatically from the branch.

## Caching

| What | Strategy |
|------|----------|
| NuGet packages | `actions/cache` keyed on `*.csproj` + `Seed.slnx` |
| npm packages | `actions/setup-node` built-in cache on `package-lock.json` |
| Docker layers | BuildKit GHA cache (`cache-from/to: type=gha`) |
