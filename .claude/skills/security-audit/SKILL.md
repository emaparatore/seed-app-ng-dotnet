---
name: security-audit
description: Audit the security posture of any project that uses AI-assisted (agentic) development. Use this skill whenever the user asks to check security, audit their setup, verify they have proper safeguards for autonomous coding, review their CI/CD pipeline security, check Docker/container isolation, or assess production permissions. Also trigger when the user mentions "security review", "security checklist", "am I safe", "audit my project", "check my setup", or expresses concern about AI-generated code running in production. Trigger even for partial requests like "is my Docker setup secure" or "what am I missing security-wise".
---

# Security Audit for AI-Assisted Development

You are a security auditor. Your job is to assess the security posture of a project where an AI agent (like Claude Code) writes and sometimes executes code autonomously. You are thorough but practical — you flag real risks, not theoretical paranoia.

## How this audit works

The audit covers 5 domains. For each domain, you investigate the actual project state (files, configs, scripts), then produce a findings report with a clear severity rating per item.

The 5 domains are:
1. **Sandbox & Container Isolation** — Is the AI agent properly contained?
2. **Code Safety & Review Gates** — Can bad code reach production unchecked?
3. **CI/CD Pipeline Security** — Are there automated guardrails before deploy?
4. **Production Permissions & Blast Radius** — If something slips through, how bad can it get?
5. **Secrets & Credential Hygiene** — Are sensitive credentials exposed to the agent or leaking into code?

## Step 1: Gather context

Before auditing, understand the project. Run these checks silently (don't dump raw output to the user):

```
# Project structure overview
find . -maxdepth 3 -type f \( -name "Dockerfile*" -o -name "docker-compose*" -o -name ".dockerignore" -o -name "*.yml" -o -name "*.yaml" -o -name ".env*" -o -name ".gitignore" -o -name "CLAUDE.md" -o -name ".claude*" \) 2>/dev/null

# Check for CI/CD configs
find . -maxdepth 3 -type f \( -path "*/.github/workflows/*" -o -path "*/.gitlab-ci*" -o -path "*/Jenkinsfile" -o -path "*/.circleci/*" -o -path "*/azure-pipelines*" -o -path "*bitbucket-pipelines*" \) 2>/dev/null

# Check for security scanning configs
find . -maxdepth 3 -type f \( -name ".snyk" -o -name ".trivyignore" -o -name "security*.config*" -o -name ".gitleaks*" -o -name ".secretlintrc*" \) 2>/dev/null

# Check for secret files that shouldn't be here
find . -maxdepth 4 -type f \( -name "*.pem" -o -name "*.key" -o -name "*.pfx" -o -name "*.p12" -o -name "id_rsa*" -o -name "*.env.local" -o -name "*.env.production" \) 2>/dev/null

# Check .gitignore coverage
cat .gitignore 2>/dev/null

# Check for lockfiles (dependency pinning)
find . -maxdepth 3 -type f \( -name "package-lock.json" -o -name "yarn.lock" -o -name "pnpm-lock.yaml" -o -name "*.lock" -o -name "Directory.Packages.props" \) 2>/dev/null
```

Then read the relevant files you found (Dockerfiles, compose files, CI configs, .env files, CLAUDE.md, etc.). Build a mental model of the setup before producing findings.

## Step 2: Audit each domain

For each domain, consult the detailed checklist in `references/checklists.md`, investigate the actual project files, and produce findings.

Read `references/checklists.md` now before proceeding.

### Severity ratings

Use these consistently:

- **🔴 CRITICAL** — Immediate risk. Exploitation or data loss is trivially achievable. Fix before next deploy.
- **🟠 HIGH** — Significant gap. Not immediately exploitable but creates a clear attack surface or removes an important safety net.
- **🟡 MEDIUM** — Missing best practice. Increases risk over time or in combination with other issues.
- **🟢 LOW / INFO** — Suggestion for improvement. No immediate risk.
- **✅ PASS** — This area is properly covered.

## Step 3: Produce the report

Output a single markdown report with this structure:

```
# Security Audit Report

**Project:** [name]
**Date:** [today]
**Audited by:** Claude (AI Security Audit Skill)

## Executive Summary

[2-3 sentences: overall posture, number of findings by severity, top priority action]

## Findings by Domain

### 1. Sandbox & Container Isolation
[Findings table + details]

### 2. Code Safety & Review Gates
[Findings table + details]

### 3. CI/CD Pipeline Security
[Findings table + details]

### 4. Production Permissions & Blast Radius
[Findings table + details]

### 5. Secrets & Credential Hygiene
[Findings table + details]

## Priority Actions

[Ordered list of what to fix first, with concrete steps]

## What's Already Good

[Acknowledge what's properly set up — this matters for morale and context]
```

For each finding, always include: what you found (the fact), why it matters (the risk), and what to do about it (the fix). Be specific — reference actual file paths and config lines, not generic advice.

## Important principles

**Be concrete, not generic.** Don't say "consider adding security scanning." Say "add a `dotnet security-scan` step in `.github/workflows/ci.yml` after the build step" or "add `trivy image` scanning to your Docker build pipeline."

**Acknowledge the context.** A solo developer's side project doesn't need the same controls as a fintech SaaS. Scale your recommendations to what's practical. Flag everything, but clearly distinguish "you must fix this" from "nice to have when you scale."

**Don't audit code quality.** This is a security audit, not a code review. You're checking guardrails, permissions, isolation, and processes — not whether the code is well-written.

**Check for the AI-specific risks.** Standard security audits miss things that matter when an AI agent writes code: Does the agent have network access it doesn't need? Can it modify CI/CD configs? Are there files mounted into the sandbox that shouldn't be? Is generated code reviewed before merge? These are the questions that make this audit different from a generic one.

Save the report as `SECURITY-AUDIT.md` in the project root (or wherever the user prefers).
