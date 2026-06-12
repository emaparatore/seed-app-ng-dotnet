# In-App Documentation Viewer

## Overview

The web app exposes selected project documentation through a public in-app Markdown viewer available at `/docs`.

The feature is frontend-only:

- The backend does not serve documentation files
- No API endpoint is involved
- Production serves the documentation as Angular static assets

Users can browse public docs with:

- A left sidebar grouped by documentation category
- Deep-linkable routes for each document
- Markdown rendering with sanitization
- Previous/next document navigation

## Default-Private Publishing Model

Documentation is private by default.

A file in the root `docs/` folder is visible in the app only if it is explicitly listed in:

```text
docs/public-docs.json
```

This keeps internal, planning, operational, or maintainer-only documents out of the app unless a developer intentionally publishes them.

## Source And Snapshot

There are two layers:

| Layer | Path | Role |
|---|---|---|
| Source of truth | `docs/**/*.md` | Canonical documentation edited by developers |
| Public frontend snapshot | `frontend/web/projects/app/public/docs/**` | Static assets served by Angular in dev/staging/production |

The public snapshot is committed to Git on purpose. This keeps the web Docker build simple: it only needs the `frontend/web` build context and does not need access to the root `docs/` folder.

## Public Docs Allowlist

The public allowlist contains the exact source files exposed in the app:

```json
{
  "documents": [
    "docs/modules/in-app-documentation.md"
  ]
}
```

Rules:

- Paths must start with `docs/`
- Paths must end with `.md`
- Paths must exist
- Duplicates are rejected
- Unsupported categories are rejected

Supported categories:

| Source folder | Viewer category |
|---|---|
| `docs/getting-started/` | Getting Started |
| `docs/architecture/` | Architecture |
| `docs/modules/` | Modules |
| `docs/operations/` | Operations |
| `docs/compliance/` | Compliance |
| `docs/seed/` | Seed |

Folders such as `docs/plans/`, `docs/requirements/`, `docs/wishes/`, and `docs/skills/` are not public unless support is intentionally added to the sync script.

## Publishing A New Document

To make a document visible in the app:

1. Create or update a source document under `docs/`, for example `docs/modules/my-feature.md`.
2. Add the file path to `docs/public-docs.json`.
3. Run the sync command:

```bash
cd frontend/web
npm run sync:docs
```

4. Commit both the source doc and the generated frontend snapshot:

```text
docs/modules/my-feature.md
docs/public-docs.json
frontend/web/projects/app/public/docs/**
```

5. Open `/docs` and verify the document appears under the expected category.

## Keeping A Document Private

To keep a document private, do nothing special.

If the document is not listed in `docs/public-docs.json`, it is not copied into the frontend snapshot and does not appear in `/docs`.

## Sync And CI Check

The sync script is:

```bash
frontend/web/scripts/build-docs.mjs
```

Package scripts:

```bash
cd frontend/web
npm run sync:docs        # regenerate public/docs snapshot
npm run check:docs-sync  # fail if public/docs is out of sync
npm run test:docs        # alias for check:docs-sync
```

CI runs `npm run check:docs-sync` when documentation sources, the public snapshot, or the sync script change.

The check fails if:

- A public source doc path is invalid
- A public source doc is missing
- The generated `manifest.json` is stale
- A generated Markdown file is stale, missing, or extra

This enforces the rule: if you publish a doc, the frontend snapshot must be committed with it.

## Routes

| Route | Behavior |
|---|---|
| `/docs` | Redirects to the first available document from the generated manifest |
| `/docs/:category` | Redirects to the first document in the category |
| `/docs/:category/:slug` | Loads and renders the selected Markdown file |

The route tree lives in:

```text
frontend/web/projects/app/src/app/pages/docs/docs.routes.ts
```

The route is registered from `app.routes.ts`.

## Rendering And Security

Markdown rendering is handled by `DocsService`:

```text
frontend/web/projects/app/src/app/services/docs.service.ts
```

The service:

- Loads `docs/manifest.json` from Angular public assets
- Fetches the selected Markdown file as text
- Renders Markdown with `marked`
- Sanitizes rendered HTML with `DOMPurify`

`marked` and `DOMPurify` are dynamically imported, so they are loaded only when the docs viewer needs to render a document.

## UI Structure

Main frontend files:

| File | Purpose |
|---|---|
| `docs-layout.*` | Shell with sidebar and document outlet |
| `docs-sidebar.*` | Category/document index and active/hover styling |
| `docs-viewer.*` | Markdown loading, rendering, breadcrumb, previous/next navigation |
| `docs.routes.ts` | `/docs` route tree and redirects |
| `docs.service.ts` | Manifest loading, document fetching, markdown rendering |
| `types/docs.ts` | Manifest and navigation types |

The navbar link is defined in:

```text
frontend/web/projects/app/src/app/app.html
```

## Production Behavior

Production does not generate documentation during the Docker build.

The committed public snapshot is already inside `frontend/web`, so the existing web Docker build context (`frontend/web`) is enough. This avoids coupling the web image build to the repository root.

Recommended verification after docs viewer changes:

```bash
cd frontend/web
npm run check:docs-sync
npm test -- --watch=false
npm run build
```

The production build may print existing Angular budget warnings. Treat actual `ERROR` output as blocking.
