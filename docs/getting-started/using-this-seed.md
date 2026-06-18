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

## First Deploy Path

Use the regular project documentation for all operational setup:

1. If the VPS is new, prepare it with [VPS Setup Guide](vps-setup-guide.md)
2. Deploy the app with [New Project Deploy Guide](new-project-deploy-guide.md)

Those guides cover:

- repository bootstrap from the seed
- `PROJECT_SLUG`, domain and naming choices
- `.env`, GitHub secrets and variables
- Cloudflare, SSL, CI/CD and smoke tests

## Make The Repository Yours

After the first successful deploy, update the repository so it no longer feels like a generic seed:

- rewrite the top of `README.md` with the product name and product-specific description
- rewrite the seed-specific introduction in `README.md`
- rename or remove leftover `Seed` branding visible to users
- delete this file if it is no longer useful
- keep the operational docs that are still relevant to the product
- update screenshots, logos, favicon and visible demo copy
- remove or adapt any feature catalog entries that are not part of the product offer

## Keep Or Delete Seed-Specific Docs

Keep these while bootstrapping the product:

- `docs/getting-started/using-this-seed.md`

Delete or rewrite them once they are no longer useful to the product team. The rest of the documentation should remain useful as normal project documentation.
