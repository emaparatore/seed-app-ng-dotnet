# In-App Documentation Viewer

## Overview

The web app exposes selected project documentation from the repository `docs/` folder through a public in-app documentation section.

Users can open `/docs` from the main navbar and browse Markdown documents with:

- A left sidebar grouped by documentation category
- Deep-linkable routes for each document
- Markdown rendering with sanitization
- Previous/next document navigation

The feature is frontend-only. The backend does not serve documentation files and no API endpoint is involved.

## Routes

| Route | Behavior |
|---|---|
| `/docs` | Redirects to the first available document from the generated manifest |
| `/docs/:category` | Redirects to the first document in the category |
| `/docs/:category/:slug` | Loads and renders the selected Markdown file |

The route tree lives in `frontend/web/projects/app/src/app/pages/docs/docs.routes.ts` and is registered from `app.routes.ts`.

## Build-Time Bundle Generation

The viewer does not read `docs/` directly at runtime. Instead, a build script copies selected Markdown files into the Angular public assets folder and generates a manifest.

Script:

```bash
frontend/web/scripts/build-docs.mjs
```

Generated output:

```text
frontend/web/projects/app/public/docs/
|-- manifest.json
|-- architecture/*.md
|-- compliance/*.md
|-- getting-started/*.md
|-- modules/*.md
|-- operations/*.md
`-- seed/*.md
```

The generated output is ignored by Git because it is a build artifact:

```gitignore
frontend/web/projects/app/public/docs/
```

It is regenerated automatically by these package scripts:

```json
"prestart": "node scripts/build-docs.mjs",
"prebuild": "node scripts/build-docs.mjs"
```

This means `npm start` and `npm run build` always rebuild the documentation bundle before Angular starts or builds.

## Included Categories

The script currently includes these public documentation categories:

| Source folder | Viewer category |
|---|---|
| `docs/getting-started/` | Getting Started |
| `docs/architecture/` | Architecture |
| `docs/modules/` | Modules |
| `docs/operations/` | Operations |
| `docs/compliance/` | Compliance |
| `docs/seed/` | Seed |

Internal working folders such as `docs/plans/`, `docs/requirements/`, `docs/wishes/`, and `docs/skills/` are not included.

## Excluding Specific Documents

Specific files can be hidden from the in-app viewer without removing them from the repository. Add them to the `EXCLUDED_FILES` set in `build-docs.mjs`:

```js
const EXCLUDED_FILES = new Set([
  'docs/operations/auto-execute.md',
  'docs/operations/adding-collaborators.md',
]);
```

Excluded files are not copied into `public/docs/` and do not appear in `manifest.json`.

Use this for maintainer-only, internal, or operational documents that should stay in the repo but not be visible in the app.

## Adding A New Visible Document

1. Add the Markdown file to one of the included source folders, for example `docs/modules/my-feature.md`.
2. Use a top-level `# Title` heading. The build script uses it as the display title.
3. Make sure the file is not listed in `EXCLUDED_FILES`.
4. Run:

```bash
cd frontend/web
npm run build:docs
```

5. Open `/docs` and verify the document appears under the expected category.

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

## Tests And Verification

Docs bundle generation has a focused smoke test:

```bash
cd frontend/web
npm run test:docs
```

The Angular service is covered by:

```text
frontend/web/projects/app/src/app/services/docs.service.spec.ts
```

Recommended verification after changes:

```bash
cd frontend/web
npm run test:docs
npm test -- --watch=false
npm run build
```

The production build may print existing Angular budget warnings. Treat actual `ERROR` output as blocking.
