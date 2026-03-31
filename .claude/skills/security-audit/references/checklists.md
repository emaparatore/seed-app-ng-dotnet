# Security Audit Checklists

Detailed checklists for each audit domain. For every item, check the actual project files — don't assume. If a config file doesn't exist, that's a finding.

---

## 1. Sandbox & Container Isolation

The goal: the AI agent should only be able to affect what it needs to, and nothing else.

### Docker / Container Configuration

- **Container runs as non-root.** Check Dockerfiles for `USER` directive. If absent, the container runs as root inside, which is worse than necessary. Look for `RUN adduser` or `RUN useradd` patterns. **Context matters for sandbox containers:** if the project uses git, the blast radius of destructive actions is limited to non-versioned files. Weigh sudo/root findings against what the agent can actually destroy permanently (e.g., local config, untracked files) rather than flagging in isolation.
- **No Docker socket mount.** Check docker-compose files and run commands for `-v /var/run/docker.sock`. This gives the container full control over the host's Docker daemon — effectively root on the host. This is almost always a CRITICAL finding.
- **Minimal volume mounts.** Check what host paths are mounted. Only the project directory should be mounted. Watch for home directories (`~/`), SSH directories (`~/.ssh`), credential directories, or system paths (`/etc`, `/usr/local/bin`).
- **Read-only mounts where possible.** Volumes that the agent only needs to read should use `:ro` flag. Check if source code or config mounts could be read-only.
- **No `--privileged` flag.** Check for `privileged: true` in compose files or `--privileged` in run commands. This disables almost all container isolation.
- **No excessive capabilities.** Check for `cap_add` in compose files. Capabilities like `SYS_ADMIN`, `NET_ADMIN`, `SYS_PTRACE` weaken isolation significantly.
- **Network isolation.** Check if the agent container has network access it doesn't need. For pure code generation tasks, `network_mode: none` or a restricted network is ideal. Check for `network_mode: host` which gives full host network access.
- **.dockerignore exists and is comprehensive.** Check that `.env`, `.git`, `node_modules`, secrets, and build artifacts are excluded from the Docker build context.

### Claude Code / Agent Configuration

- **CLAUDE.md doesn't grant excessive permissions.** Review CLAUDE.md for overly broad instructions like "you have full access" or permission patterns that bypass safety.
- **Allowed/disallowed tools are configured.** If the agent framework supports tool restrictions, check if dangerous tools (shell execution, file deletion, network access) are properly scoped.
- **Working directory is constrained.** The agent should work within the project directory, not have access to the entire filesystem.

---

## 2. Code Safety & Review Gates

The goal: no code reaches production without human or automated verification.

### Git & Version Control

- **Branch protection is enabled.** If using GitHub/GitLab, check if the main/production branch requires pull requests. This can't be verified from the repo alone — note it as "verify in your Git provider settings."
- **AI-generated commits are identifiable.** Check if there's a convention (commit message prefix, author tag, or branch naming) that distinguishes AI-generated code from human-written code. This helps with forensics if something goes wrong.
- **Meaningful commit granularity.** Check recent git log — are AI-generated changes atomic (one logical change per commit) or massive blobs? Atomic commits are reviewable; blobs hide problems.
- **No force pushes to protected branches.** Flag if git config or CI allows force pushes to main/production branches.

### Code Review Process

- **Pull requests exist in the workflow.** Check for PR templates (`.github/pull_request_template.md`), branch naming conventions, or CI triggers that imply a PR-based workflow.
- **Diff review is feasible.** If AI generates 5000-line commits, no human will review them effectively. Check if the workflow encourages small, reviewable changes.
- **No auto-merge without checks.** Check CI configs for auto-merge rules. Auto-merge is fine IF all required checks pass, but dangerous without checks.

---

## 3. CI/CD Pipeline Security

The goal: automated checks catch what humans miss, and deploys can't happen without them.

### Automated Security Scanning

- **Dependency vulnerability scanning exists.** Look for: Dependabot config (`.github/dependabot.yml`), Snyk config (`.snyk`), `npm audit` / `dotnet list package --vulnerable` in CI steps, Trivy, or Renovate.
- **Static analysis / SAST is configured.** Look for: SonarQube/SonarCloud config, CodeQL (`.github/codeql`), Semgrep, Roslyn analyzers (.NET), ESLint security plugins (JS/TS), Bandit (Python).
- **Container image scanning.** If Docker images are built in CI, check for Trivy, Snyk Container, or similar scanning of the built image before push.
- **Secret scanning is active.** Look for: GitHub secret scanning (enabled by default on public repos), Gitleaks config (`.gitleaks.toml`), TruffleHog, or similar tools in CI.

### Pipeline Integrity

- **CI config is protected.** Check if CI workflow files (`.github/workflows/`, etc.) could be modified by the AI agent. If the agent can push changes to CI configs, it can disable its own guardrails.
- **Deploy requires all checks to pass.** Look at CI config — is deployment conditional on test/scan success, or can it proceed regardless?
- **Manual approval gate for production.** Check if there's a manual approval step before production deploy. For solo developers this might be overkill, but flag its absence as MEDIUM.
- **Dependencies are pinned.** Check if lockfiles exist and are committed. Unpinned dependencies mean builds are non-reproducible and vulnerable to supply chain attacks.
- **CI environment variables are scoped.** Check if production secrets are available to all CI jobs or only to deploy jobs. Broad access means a compromised test step can leak production credentials.

---

## 4. Production Permissions & Blast Radius

The goal: even if malicious code reaches production, damage is contained.

### Application Permissions

- **Principle of least privilege.** Check if the production app runs with minimal permissions. Look at: database connection strings (is it using a superuser?), cloud IAM roles (are they scoped narrowly?), filesystem permissions.
- **Database user is not admin/sa.** Check connection strings in config files or environment variable references. The app should use a dedicated user with only the permissions it needs.
- **No hardcoded credentials in code.** Search for patterns like `password=`, `secret=`, `apikey=`, connection strings with embedded credentials in source files (not env files).
- **Environment separation.** Check if dev/staging/production are properly separated. Look for environment-specific config files, separate Docker Compose files, or deployment configs.

### Network & Infrastructure

- **Production container is not privileged.** Same checks as sandbox section, but for the production Dockerfile/compose.
- **Egress is restricted.** Can the production app make outbound connections to arbitrary hosts? Ideally, outbound access is limited to known dependencies (databases, APIs, CDNs).
- **Health checks and monitoring exist.** Check for health check endpoints, Docker HEALTHCHECK directives, or monitoring configurations. Not strictly security, but a tampered app without monitoring runs undetected.
- **Logging is configured.** Check if the app produces structured logs. If something goes wrong, you need an audit trail. Look for logging configuration in app settings.

---

## 5. Secrets & Credential Hygiene

The goal: secrets stay secret, are rotated, and are never accessible where they shouldn't be.

### Secret Storage

- **No secrets in source code.** Scan for: API keys, passwords, tokens, private keys, connection strings with credentials — in any committed file. Check git history too if possible (`git log --all -p -S "password"` or similar).
- **No secrets in Docker build context.** Check that `.dockerignore` excludes `.env` files, key files, and credential stores.
- **.env files are gitignored.** Check `.gitignore` for `.env`, `.env.local`, `.env.production`, etc.
- **.env.example exists without real values.** If the project uses env files, there should be a template with placeholder values, not real credentials.
- **Secrets use a proper secret manager in production.** Check if production configs reference a secret manager (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, Docker secrets) or just read from env files on the host.

### Agent Access to Secrets

- **The AI agent doesn't have access to production secrets.** Check if `.env` files with real credentials are mounted into the agent's sandbox. The agent should work with dummy/dev credentials only.
- **The agent can't read secret manager credentials.** If the project uses a secret manager, check that the agent's environment doesn't have the tokens/credentials to access it.
- **Git credentials are scoped.** If the agent can push to Git, check what scope the token has. It should be limited to the specific repo, not org-wide access.
- **CI/CD secrets aren't logged.** Check CI configs for `echo` or print statements that might leak secrets in build logs. Check if secret masking is enabled.
