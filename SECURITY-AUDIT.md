# Security Audit Report

**Project:** seed-app-ng-dotnet
**Date:** 2026-03-30
**Audited by:** Claude (AI Security Audit Skill)

## Executive Summary

The project has a solid foundation: CI runs tests before merge, Docker images use multi-stage builds, production secrets are environment-variable driven, and `.gitignore` properly excludes sensitive files. However, there are **2 critical**, **2 high**, and **5 medium** findings — primarily around missing container hardening in production and absence of automated security scanning in CI. The top priority is adding a non-root `USER` directive to the production API and web Dockerfiles.

---

## Findings by Domain

### 1. Sandbox & Container Isolation

| # | Finding | Severity |
|---|---------|----------|
| 1.1 | Sandbox container has passwordless sudo | ✅ ACCEPTED |
| 1.2 | Sandbox mounts entire project directory read-write | 🟡 MEDIUM |
| 1.3 | Sandbox has unrestricted network access | 🟡 MEDIUM |
| 1.4 | Claude Code permissions are scoped | ✅ PASS |
| 1.5 | No Docker socket mount | ✅ PASS |
| 1.6 | No `--privileged` or dangerous capabilities | ✅ PASS |

**1.1 — Sandbox container has passwordless sudo** ✅ ACCEPTED
In [Dockerfile.sandbox:21](docker/Dockerfile.sandbox#L21), the `claude` user is granted `NOPASSWD:ALL` sudo. The agent runs with `--dangerously-skip-permissions`, so sudo doesn't materially expand the attack surface. The blast radius is limited: all code is under git (worst case = a burned branch), and the only non-recoverable data is the mounted `.claude/` config directory, which is considered an acceptable loss.

**1.2 — Sandbox mounts entire project directory read-write** 🟡 MEDIUM
In [docker-compose.yml:124](docker/docker-compose.yml#L124), the sandbox mounts `..:/project` with read-write access. The agent can modify any file in the repo, including `.github/workflows/`, `Dockerfile`, and `docker-compose.yml`. A malicious or buggy agent could alter CI pipelines to disable checks.
**Fix:** Consider mounting CI/CD configs as read-only via a secondary mount: `- ../.github:/project/.github:ro`. This allows the agent to write code but not modify pipeline definitions.

**1.3 — Sandbox has unrestricted network access** 🟡 MEDIUM
The sandbox container has no network restrictions. The agent can make arbitrary outbound requests (exfiltrate code, download arbitrary packages, access internal services on the Docker network including the database).
**Fix:** For code-generation-only tasks, consider `network_mode: none`. If network access is needed (e.g., `npm install`), restrict egress to specific domains via a proxy or firewall rule. At minimum, put the sandbox on a separate Docker network from the database.

---

### 2. Code Safety & Review Gates

| # | Finding | Severity |
|---|---------|----------|
| 2.1 | No PR template | 🟢 LOW |
| 2.2 | Branch protection enforcement — verify externally | 🟡 MEDIUM |
| 2.3 | PR-based workflow is established | ✅ PASS |
| 2.4 | CI runs on all PRs to dev and master | ✅ PASS |
| 2.5 | Conventional commits with scopes | ✅ PASS |

**2.1 — No PR template** 🟢 LOW
No `.github/pull_request_template.md` exists. A template ensures every PR (human or AI-generated) includes a consistent description, test plan, and security considerations section.
**Fix:** Create `.github/pull_request_template.md` with sections for Summary, Key decisions, How to test, and a Security checklist (e.g., "Does this change handle user input? Does it modify auth logic?").

**2.2 — Branch protection — verify externally** 🟡 MEDIUM
CLAUDE.md documents a PR-based workflow, and the CI runs on PRs to `dev` and `master`. However, branch protection rules (required reviews, no force pushes, required status checks) must be configured in GitHub repo Settings. This cannot be verified from the repo alone.
**Fix:** Verify in GitHub Settings > Branches that `master` and `dev` have: (1) Require pull request reviews, (2) Require status checks to pass (CI), (3) Do not allow force pushes, (4) Do not allow deletions.

---

### 3. CI/CD Pipeline Security

| # | Finding | Severity |
|---|---------|----------|
| 3.1 | No dependency vulnerability scanning | ✅ FIXED |
| 3.2 | No SAST / static security analysis | ✅ FIXED |
| 3.3 | No container image scanning | ✅ FIXED |
| 3.4 | No secret scanning (Gitleaks / GitHub) | 🟡 MEDIUM |
| 3.5 | No Dependabot configuration | 🟡 MEDIUM |
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

**3.4 — No secret scanning** 🟡 MEDIUM
No Gitleaks config (`.gitleaks.toml`) or similar tool in CI. If GitHub Advanced Security is not enabled on this repo, accidentally committed secrets will not be detected.
**Fix:** Add Gitleaks to CI or enable GitHub secret scanning in repo Settings > Code security.

**3.5 — No Dependabot configuration** 🟡 MEDIUM
No `.github/dependabot.yml` exists. Dependencies are not automatically monitored for updates or security patches.
**Fix:** Create `.github/dependabot.yml`:
```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: /backend
    schedule:
      interval: weekly
  - package-ecosystem: npm
    directory: /frontend/web
    schedule:
      interval: weekly
  - package-ecosystem: docker
    directory: /backend/src/Seed.Api
    schedule:
      interval: weekly
  - package-ecosystem: github-actions
    directory: /
    schedule:
      interval: weekly
```

---

### 4. Production Permissions & Blast Radius

| # | Finding | Severity |
|---|---------|----------|
| 4.1 | Production API container runs as root | 🔴 CRITICAL |
| 4.2 | Production web container runs as root | 🟠 HIGH (same class) |
| 4.3 | Seq has no authentication in production | 🟡 MEDIUM |
| 4.4 | Health checks configured | ✅ PASS |
| 4.5 | Structured logging with Serilog + Seq | ✅ PASS |
| 4.6 | Environment separation (dev/staging/prod) | ✅ PASS |
| 4.7 | Production config uses empty placeholders, not hardcoded creds | ✅ PASS |

**4.1 — Production API container runs as root** 🔴 CRITICAL
The API [Dockerfile](backend/src/Seed.Api/Dockerfile) has no `USER` directive in the runtime stage. The `aspnet:10.0` base image defaults to root. If an attacker achieves RCE through a vulnerability, they have root inside the container, making container escapes easier and lateral movement more impactful.
**Fix:** Add a non-root user to the runtime stage:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN adduser --disabled-password --gecos "" appuser
# ... existing COPY steps ...
USER appuser
```
Note: The `EXPOSE 8080` port is >1024 so no root is needed for binding.

**4.2 — Production web container runs as root** 🟠 HIGH
The web [Dockerfile](frontend/web/Dockerfile) also has no `USER` directive. Same issue as 4.1.
**Fix:** Add `USER node` before the `CMD` — the `node:22-slim` image already includes a `node` user:
```dockerfile
FROM node:22-slim AS runtime
WORKDIR /app
COPY --from=build /app/dist ./dist
USER node
ENV PORT=80
EXPOSE 80
CMD ["node", "dist/app/server/server.mjs"]
```
Note: Port 80 inside the container requires a change — either use port 3000 or use a capability. Recommend changing `ENV PORT=3000` and updating the compose file accordingly.

**4.3 — Seq has no authentication in production** 🟡 MEDIUM
In [docker-compose.deploy.yml:23](docker/docker-compose.deploy.yml#L23), Seq runs with `SEQ_FIRSTRUN_NOAUTHENTICATION=true`. While Seq is only exposed on `127.0.0.1`, anyone with SSH access to the VPS can read all application logs, which may contain PII, request details, or error traces with sensitive data.
**Fix:** After initial setup, configure Seq authentication and remove the `NOAUTHENTICATION` flag. Alternatively, set an API key for ingestion.

---

### 5. Secrets & Credential Hygiene

| # | Finding | Severity |
|---|---------|----------|
| 5.1 | Dev credentials in appsettings.json (committed) | 🟡 MEDIUM |
| 5.2 | `.env` properly gitignored | ✅ PASS |
| 5.3 | `.env.example` contains only placeholder/dev values | ✅ PASS |
| 5.4 | `.env.prod.example` contains no real credentials | ✅ PASS |
| 5.5 | Production config uses empty strings, overridden by env vars | ✅ PASS |
| 5.6 | No secret files (`.pem`, `.key`, `.pfx`) in repo | ✅ PASS |
| 5.7 | `.dockerignore` excludes `.env` | ✅ PASS (backend via `.git`; web via `.git`) |
| 5.8 | Claude Code tool permissions are explicitly scoped | ✅ PASS |

**5.1 — Dev credentials in appsettings.json** 🟡 MEDIUM
[appsettings.json](backend/src/Seed.Api/appsettings.json) contains the dev JWT secret and database password in plain text. [appsettings.Development.json](backend/src/Seed.Api/appsettings.Development.json) contains the SuperAdmin password `Admin123!`. While these are clearly development-only values and production overrides them with environment variables, the pattern normalizes committing secrets. A developer could accidentally put a real secret here.
**Fix:** This is acceptable for a seed/starter project, but add a comment in the file: `// DEV ONLY — production values MUST be set via environment variables`. Consider moving even dev secrets to `appsettings.Local.json` (which is gitignored) and documenting the setup in the README.

---

## Priority Actions

1. **🔴 Add non-root USER to production Dockerfiles** — [backend/src/Seed.Api/Dockerfile](backend/src/Seed.Api/Dockerfile) and [frontend/web/Dockerfile](frontend/web/Dockerfile). Immediate risk reduction, ~5 lines of code each.

2. **✅ ~~Add dependency vulnerability scanning to CI~~** — FIXED. Added to [ci.yml](.github/workflows/ci.yml).

3. **✅ ~~Add container image scanning~~** — FIXED. Added Trivy to [docker-publish.yml](.github/workflows/docker-publish.yml).

4. **✅ ~~Enable SAST scanning~~** — FIXED. Added Semgrep to [semgrep.yml](.github/workflows/semgrep.yml).

5. **🟡 Configure Dependabot** — Create `.github/dependabot.yml` for automated dependency update PRs.

7. **🟡 Enable secret scanning** — Gitleaks in CI or GitHub native secret scanning.

8. **🟡 Verify branch protection rules** — In GitHub Settings, ensure `master` and `dev` require PR reviews + passing CI.

9. **🟡 Protect CI config from sandbox agent** — Mount `.github/` as read-only in the sandbox container.

10. **🟡 Configure Seq authentication** — Remove `SEQ_FIRSTRUN_NOAUTHENTICATION` after initial setup in production.

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
