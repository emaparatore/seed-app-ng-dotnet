# Using This Seed

This document is for the original seed repository. It explains how to turn the seed into a product repository and what can be cleaned up afterwards.

If you are already working inside your product repository, treat this as a temporary handoff document. Once the project is renamed, branded and deployed, you can delete this file and rewrite the seed-specific introduction in `README.md`.

## Goal

Use the seed to start from a working application baseline:

- local Docker development
- backend, web frontend and mobile project structure
- authentication and admin features
- email configuration with console fallback
- subscription/payment module
- production bootstrap and seeding
- CI/CD, Docker image publishing, VPS deploy, monitoring and rollback documentation

The goal is not to keep the product visibly tied to the seed. After setup, the repository should read like the documentation for your own application.

## Create The Product Repository

Use GitHub's template flow or clone the seed and change the remote:

```bash
git clone https://github.com/TUO_USERNAME/seed-app-ng-dotnet.git nuovo-progetto
cd nuovo-progetto
git remote set-url origin https://github.com/TUO_USERNAME/nuovo-progetto.git
```

Then push to the new repository.

## Choose Stable Project Values

Choose these values early because they affect deploy naming and user-facing behavior:

- `PROJECT_SLUG`: stable slug for images and default deploy path, for example `nuovo-progetto`
- production domain, for example `nuovodominio.com`
- optional staging domain, for example `staging.nuovodominio.com`
- app name, for example `General__AppName=Nuovo Progetto`
- sender name and email for SMTP

The default seed values are safe for local development, but should be replaced before a real deploy.

## What To Rename First

Minimum recommended before first deploy:

- GitHub repository name
- `PROJECT_SLUG` GitHub Actions variable
- domain and `CLIENT_BASE_URL`
- `General__AppName`
- visible frontend branding, logo and favicon
- SMTP sender name and email if using real email

Can be delayed:

- full C# namespace rename from `Seed.*`
- solution and project file rename
- deep cosmetic refactors
- removal of demo text not visible to users

## Deployment Naming

The deploy pipeline is driven by `PROJECT_SLUG`.

If `PROJECT_SLUG=nuovo-progetto`:

- images are published under `ghcr.io/<owner>/nuovo-progetto/api` and `ghcr.io/<owner>/nuovo-progetto/web`
- default deploy root is `/opt/nuovo-progetto`
- production deploy dir is `/opt/nuovo-progetto/production`
- staging deploy dir is `/opt/nuovo-progetto/staging`
- backups are stored under `/opt/nuovo-progetto/backups`

If you need a custom path, set the GitHub Actions variable `DEPLOY_ROOT`.

## First Deploy Path

Use the regular project documentation:

1. If the VPS is new, prepare it with [VPS Setup Guide](../getting-started/vps-setup-guide.md)
2. Deploy the app with [New Project Deploy Guide](../getting-started/new-project-deploy-guide.md)
3. Use [Seed Checklist](../getting-started/seed-checklist.md) if you only need the shortest operational checklist

## Make The Repository Yours

After the first successful deploy, update the repository so it no longer feels like a generic seed:

- rewrite the top of `README.md` with the product name and product-specific description
- rewrite the seed-specific introduction in `README.md`
- delete this file if it is no longer useful
- keep the operational docs that are still relevant to the product
- update screenshots, logos, favicon and visible demo copy
- remove or adapt any feature catalog entries that are not part of the product offer

## Keep Or Delete Seed-Specific Docs

Keep these while bootstrapping the product:

- `docs/seed/using-this-seed.md`
- `docs/getting-started/seed-checklist.md`

Delete or rewrite them once they are no longer useful to the product team. The rest of the documentation should remain useful as normal project documentation.
