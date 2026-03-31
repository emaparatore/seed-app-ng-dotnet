# Adding Collaborators

Checklist of actions to take before adding collaborators to this repository. The project is currently configured for a solo developer — these steps harden the setup for a multi-developer team.

## 1. Branch Protection — Required Reviews

**Where:** GitHub > Settings > Branches > Branch protection rules

For both `master` and `dev`:

- [ ] Set **Required approving review count** to at least **1**
- [ ] Enable **Dismiss stale pull request approvals when new commits are pushed** — ensures that if new commits are added after a review, the approval is invalidated and a fresh review is required before merge
- [ ] Enable **Require review from Code Owners** (optional, requires a `CODEOWNERS` file — see section below)
- [ ] Enable **Require approval of the most recent reviewable push** — prevents the person who pushed the last commit from also being the one to approve it

Additionally, on `dev`:
- [ ] Enable **Require conversation resolution before merging** (already active on `master`)

## 2. CODEOWNERS (Optional)

If you want specific people to be required reviewers for certain areas of the codebase, create `.github/CODEOWNERS`:

```
# Default owner for everything
* @your-username

# Backend
/backend/ @backend-dev

# Frontend
/frontend/ @frontend-dev

# Infrastructure (CI, Docker, deploy)
/.github/ @your-username
/docker/ @your-username
```

This requires **Require review from Code Owners** to be enabled in branch protection.

## 3. PR Template

Create `.github/pull_request_template.md` so every PR has a consistent format:

```markdown
## Summary
<!-- What changed and why (1-3 bullets) -->

## Key decisions
<!-- Design choices worth noting, if any -->

## How to test
<!-- Steps or commands to verify the change -->

## Security checklist
- [ ] Does this change handle user input?
- [ ] Does it modify auth or permissions logic?
- [ ] Does it change database schema?
- [ ] Does it add new dependencies?
```

## 4. Repository Access Levels

**Where:** GitHub > Settings > Collaborators and teams

- Grant **Write** access to active contributors (can push branches, open PRs)
- Grant **Maintain** access to trusted leads (can manage PRs and branches, but not settings)
- Keep **Admin** access to yourself only

Avoid granting Admin to collaborators — `enforce_admins` is enabled, so admin access doesn't bypass branch protection, but it does allow changing repo settings.

## 5. Secret Scanning

With multiple contributors, the risk of accidentally committed secrets increases.

- [ ] Enable **GitHub secret scanning** in Settings > Code security and analysis (free for public repos, requires Advanced Security for private)
- [ ] Alternatively, add [Gitleaks](https://github.com/gitleaks/gitleaks) to CI (see SECURITY-AUDIT.md finding 3.4)

## 6. Signed Commits (Optional)

For high-trust environments, require signed commits to verify authorship:

**Where:** Branch protection rules > **Require signed commits**

All contributors will need to set up GPG or SSH signing. This adds friction, so only enable it if your threat model requires it.

## 7. Claude Code / AI Agent Access

If collaborators use the sandbox (Claude Code in Docker):

- The sandbox mounts `.github/` and `docker/` as read-only (see SECURITY-AUDIT.md finding 1.2)
- Review the `.claude/settings.local.json` permissions — they scope which commands the agent can run
- Consider whether each collaborator should have their own sandbox config or share the project defaults

## Quick Reference

| Setting | Current (solo) | Recommended (team) |
|---------|----------------|-------------------|
| Required reviews | 0 | 1+ |
| Dismiss stale reviews | Off | On |
| Last push approval | Off | On |
| Conversation resolution (`dev`) | Off | On |
| Code owners | None | Optional |
| Secret scanning | Not configured | On |
