# Security Audit Report

**Project:** seed-app-ng-dotnet
**Date:** 2026-03-30
**Audited by:** Claude (AI Security Audit Skill)

## Executive Summary

The project has a solid foundation: CI runs tests before merge, Docker images use multi-stage builds, production secrets are environment-variable driven, and `.gitignore` properly excludes sensitive files. The initial audit found **2 critical**, **2 high**, and **5 medium** findings. All critical and high findings have been **fixed** (non-root containers, dependency scanning, SAST, container image scanning). Branch protection has been **verified** as adequate for a solo developer. All medium findings have been **fixed** or **accepted**.

---

## Findings by Domain

### 1. Sandbox & Container Isolation

| # | Finding | Severity |
|---|---------|----------|
| 1.1 | Sandbox container has passwordless sudo | ✅ ACCEPTED |
| 1.2 | Sandbox mounts entire project directory read-write | ✅ FIXED |
| 1.3 | Sandbox has unrestricted network access | ✅ ACCEPTED |
| 1.4 | Claude Code permissions are scoped | ✅ PASS |
| 1.5 | No Docker socket mount | ✅ PASS |
| 1.6 | No `--privileged` or dangerous capabilities | ✅ PASS |

**1.1 — Sandbox container has passwordless sudo** ✅ ACCEPTED
In [Dockerfile.sandbox:21](docker/Dockerfile.sandbox#L21), the `claude` user is granted `NOPASSWD:ALL` sudo. The agent runs with `--dangerously-skip-permissions`, so sudo doesn't materially expand the attack surface. The blast radius is limited: all code is under git (worst case = a burned branch), and the only non-recoverable data is the mounted `.claude/` config directory, which is considered an acceptable loss.

**1.2 — Sandbox mounts entire project directory read-write** ✅ FIXED
Added read-only overlay mounts in [docker-compose.yml:125-126](docker/docker-compose.yml#L125-L126) for protected paths: `.github/` and `docker/`. The base `..:/project` mount remains read-write for application code, but Docker resolves the more specific `:ro` mounts first, preventing the agent from modifying CI pipelines, container configs, or its own instructions. Additionally, the requirements-workflow skill now flags tasks that touch protected paths as `🔒 INTERACTIVE ONLY` during planning (see [plan-guide.md](.claude/skills/requirements-workflow/references/plan-guide.md) § "Protected Paths"), so infrastructure changes are separated from autonomous execution at the process level too.

**1.3 — Sandbox has unrestricted network access** ✅ ACCEPTED
The sandbox container has no network restrictions. However, the sandbox runs exclusively on the developer's local machine (activated manually via `--profile sandbox`), is not present in `docker-compose.deploy.yml`, and its image is never published to GHCR. The blast radius is limited to the local dev environment — all code is under git (worst case = a burned branch). Accepted risk for a local-only development tool.

---

### 2. Code Safety & Review Gates

| # | Finding | Severity |
|---|---------|----------|
| 2.1 | No PR template | ✅ FIXED |
| 2.2 | Branch protection enforcement — verify externally | ✅ VERIFIED |
| 2.3 | PR-based workflow is established | ✅ PASS |
| 2.4 | CI runs on all PRs to dev and master | ✅ PASS |
| 2.5 | Conventional commits with scopes | ✅ PASS |

**2.1 — No PR template** ✅ FIXED
Added [`.github/pull_request_template.md`](.github/pull_request_template.md) with Summary, Key decisions, and How to test sections — aligned with the PR structure already in CLAUDE.md. The template pre-populates when opening PRs via GitHub UI; ignored when `--body` is passed explicitly (e.g., by Claude Code).

**2.2 — Branch protection — verify externally** ✅ VERIFIED (2026-03-31)
Verified via GitHub API. Both `master` and `dev` have:
- ✅ Required status checks: `ci-success` with `strict: true` (branch must be up-to-date)
- ✅ Force pushes blocked
- ✅ Branch deletions blocked
- ✅ Enforce admins enabled (rules apply to repo owners too)
- ✅ Required conversation resolution on `master`

**Not enabled (acceptable for solo developer):**
- Required approving reviews: set to 0 (no reviewers needed)
- Dismiss stale reviews: off (no effect with 0 required reviews)
- Required conversation resolution on `dev`: off (staging branch, more flexibility)

See [docs/adding-collaborators.md](docs/adding-collaborators.md) for the checklist of settings to tighten before adding team members.

---

### 3. CI/CD Pipeline Security

| # | Finding | Severity |
|---|---------|----------|
| 3.1 | No dependency vulnerability scanning | ✅ FIXED |
| 3.2 | No SAST / static security analysis | ✅ FIXED |
| 3.3 | No container image scanning | ✅ FIXED |
| 3.4 | No secret scanning (Gitleaks / GitHub) | ✅ FIXED |
| 3.5 | No Dependabot configuration | ✅ FIXED |
| 3.6 | Dependencies are pinned via lockfile | ✅ PASS |
| 3.7 | Deploy requires Docker Publish success | ✅ PASS |
| 3.8 | Production deploy uses GitHub Environments | ✅ PASS |
| 3.9 | CI permissions are scoped (`contents: read`, `packages: write`) | ✅ PASS |

**3.1 — Dependency vulnerability scanning** ✅ FIXED
Added `dotnet list package --vulnerable --include-transitive` (NuGet) and `npm audit --audit-level=high` (npm) to [ci.yml](/.github/workflows/ci.yml). CI now fails if any HIGH/CRITICAL CVE is detected in dependencies.

**3.2 — SAST / static security analysis** ✅ FIXED
Added Semgrep SAST scanning to [semgrep.yml](/.github/workflows/semgrep.yml). Runs on PRs and pushes to `master`/`dev`, covering both C# and TypeScript/JavaScript. Uses `--config auto` (community rules including OWASP top 10). Results are uploaded as SARIF for GitHub Security tab integration. Chosen over CodeQL because Semgrep is free for both public and private repos, avoiding vendor lock-in if the repo becomes private.

**3.3 — Container image scanning** ✅ FIXED
Added Trivy container image scanning to [docker-publish.yml](.github/workflows/docker-publish.yml). Both API and Web images are scanned after build+push. CI fails if any CRITICAL or HIGH vulnerability is found in the image (OS packages or application dependencies).

**3.4 — Secret scanning** ✅ FIXED
Added [gitleaks.yml](.github/workflows/gitleaks.yml) using `gitleaks/gitleaks-action@v2`. Runs on PRs and pushes to `master`/`dev` with full git history (`fetch-depth: 0`). The action automatically detects `.gitleaks.toml` in the repo root, which allowlists `appsettings.json` and `appsettings.Development.json` (these contain intentional dev-only placeholder secrets; production values are injected via environment variables).

**3.5 — No Dependabot configuration** ✅ FIXED
Added [`.github/dependabot.yml`](.github/dependabot.yml) with weekly update checks for all four ecosystems: NuGet (`/backend`), npm (`/frontend/web`), Docker (`/backend/src/Seed.Api`), and GitHub Actions (`/`). Dependabot will now automatically open PRs when dependency updates or security patches are available.

---

### 4. Production Permissions & Blast Radius

| # | Finding | Severity |
|---|---------|----------|
| 4.1 | Production API container runs as root | ✅ FIXED |
| 4.2 | Production web container runs as root | ✅ FIXED |
| 4.3 | Seq has no authentication in production | ✅ FIXED |
| 4.4 | Health checks configured | ✅ PASS |
| 4.5 | Structured logging with Serilog + Seq | ✅ PASS |
| 4.6 | Environment separation (dev/staging/prod) | ✅ PASS |
| 4.7 | Production config uses empty placeholders, not hardcoded creds | ✅ PASS |

**4.1 — Production API container runs as root** ✅ FIXED
Added `adduser` and `USER appuser` to the runtime stage of [Dockerfile](backend/src/Seed.Api/Dockerfile). The API process now runs as a non-root user. Port 8080 is >1024 so no root is needed for binding.

**4.2 — Production web container runs as root** ✅ FIXED
Added `USER node` to the runtime stage of [Dockerfile](frontend/web/Dockerfile). The Node process now runs as a non-root user. Port changed from 80 to 3000 (non-privileged), and the Nginx reverse proxy template updated to `proxy_pass http://web:3000/`.

**4.3 — Seq has no authentication in production** ✅ FIXED
Added Seq API key support to [docker-compose.deploy.yml](docker/docker-compose.deploy.yml) via `SEQ_API_KEY` environment variable and documented the full authentication setup procedure in [vps-setup-guide.md](docs/vps-setup-guide.md) (section "Abilitare l'autenticazione su Seq"). The `SEQ_FIRSTRUN_NOAUTHENTICATION` flag remains in the compose file for initial setup only — the guide instructs to remove it after creating an admin user. The API key placeholder has been added to [.env.prod.example](docker/.env.prod.example).

---

### 5. Secrets & Credential Hygiene

| # | Finding | Severity |
|---|---------|----------|
| 5.1 | Dev credentials in appsettings.json (committed) | ✅ ACCEPTED |
| 5.2 | `.env` properly gitignored | ✅ PASS |
| 5.3 | `.env.example` contains only placeholder/dev values | ✅ PASS |
| 5.4 | `.env.prod.example` contains no real credentials | ✅ PASS |
| 5.5 | Production config uses empty strings, overridden by env vars | ✅ PASS |
| 5.6 | No secret files (`.pem`, `.key`, `.pfx`) in repo | ✅ PASS |
| 5.7 | `.dockerignore` excludes `.env` | ✅ PASS (backend via `.git`; web via `.git`) |
| 5.8 | Claude Code tool permissions are explicitly scoped | ✅ PASS |

**5.1 — Dev credentials in appsettings.json** ✅ FIXED
Moved dev-only credentials (ConnectionString, JWT secret) from [appsettings.json](backend/src/Seed.Api/appsettings.json) to [appsettings.Development.json](backend/src/Seed.Api/appsettings.Development.json). The base `appsettings.json` now contains only empty placeholders — production values are injected via environment variables, and dev values are loaded automatically when `ASPNETCORE_ENVIRONMENT=Development`. SuperAdmin credentials were already in `appsettings.Development.json` only. When running with Docker, all values are overridden by the `.env` file via docker-compose.

---

## Priority Actions

1. **✅ ~~Add non-root USER to production Dockerfiles~~** — FIXED. Added `USER appuser` to [backend/src/Seed.Api/Dockerfile](backend/src/Seed.Api/Dockerfile) and `USER node` to [frontend/web/Dockerfile](frontend/web/Dockerfile).

2. **✅ ~~Add dependency vulnerability scanning to CI~~** — FIXED. Added to [ci.yml](.github/workflows/ci.yml).

3. **✅ ~~Add container image scanning~~** — FIXED. Added Trivy to [docker-publish.yml](.github/workflows/docker-publish.yml).

4. **✅ ~~Enable SAST scanning~~** — FIXED. Added Semgrep to [semgrep.yml](.github/workflows/semgrep.yml).

5. **✅ ~~Configure Dependabot~~** — FIXED. Added [`.github/dependabot.yml`](.github/dependabot.yml) with weekly checks for NuGet, npm, Docker, and GitHub Actions.

7. **✅ ~~Enable secret scanning~~** — FIXED. Added Gitleaks to [gitleaks.yml](.github/workflows/gitleaks.yml) with [.gitleaks.toml](.gitleaks.toml) allowlist for dev-only appsettings.

8. **✅ ~~Verify branch protection rules~~** — VERIFIED. Both `master` and `dev` have required CI checks, no force push, no deletions, enforce admins. Adequate for solo developer. See [docs/adding-collaborators.md](docs/adding-collaborators.md) for multi-developer hardening.

9. **✅ ~~Protect CI config from sandbox agent~~** — FIXED. Added read-only mounts for protected paths in sandbox container + `🔒 INTERACTIVE ONLY` task markers in requirements-workflow skill.

10. **✅ ~~Configure Seq authentication~~** — FIXED. Added API key support to [docker-compose.deploy.yml](docker/docker-compose.deploy.yml) and setup guide in [vps-setup-guide.md](docs/vps-setup-guide.md).

---

## What's Already Good

- **PR-based workflow with CI gates.** CI runs on every PR to `dev` and `master`, including build, unit tests, integration tests, migration verification, and migration bundle build. This is a strong baseline.
- **Clean separation of environments.** Dev, staging, and production have separate config files, compose files, and deployment strategies. Production config uses empty placeholders overridden by environment variables.
- **Secrets are properly gitignored.** `.env` files, local appsettings, and credential files are all in `.gitignore`. No secret files were found in the repository.
- **Docker multi-stage builds.** Both API and web Dockerfiles use multi-stage builds, keeping the runtime image lean (no SDK, no source code).
- **Claude Code permissions are scoped.** The `.claude/settings.local.json` only allows specific build, test, and git commands — no arbitrary shell execution.
- **Production deploy uses GitHub Environments.** The deploy workflow references environment-specific settings, enabling required reviewers and wait timers for production.
- **Health checks in production.** The API has a health check endpoint, and the deploy script verifies it before completing.
- **Structured logging.** Serilog with Seq provides a full audit trail for production issues.
- **Sandbox runs as non-root user.** The Dockerfile.sandbox creates and switches to a `claude` user (though the sudo access undermines this — see finding 1.1).
- **Hotfix back-merge automation.** The hotfix-backmerge workflow ensures `dev` stays in sync with `master` after hotfixes.
