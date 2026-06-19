#!/usr/bin/env node
import { promises as fs } from 'node:fs';
import path from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const REPO_ROOT = path.resolve(__dirname, '..', '..', '..');
const PUBLIC_DOCS_CONFIG = path.join(REPO_ROOT, 'docs', 'public-docs.json');
const PUBLIC_OUTPUT = path.join(__dirname, '..', 'projects', 'app', 'public', 'docs');

const CATEGORIES = [
  { slug: 'getting-started', title: 'Getting Started', order: 1 },
  { slug: 'architecture', title: 'Architecture', order: 2 },
  { slug: 'modules', title: 'Modules', order: 3 },
  { slug: 'operations', title: 'Operations', order: 4 },
  { slug: 'compliance', title: 'Compliance', order: 5 },
];

const CATEGORY_BY_SLUG = new Map(CATEGORIES.map((category) => [category.slug, category]));
const CHECK_MODE = process.argv.includes('--check');

function slugifyTitle(raw) {
  return raw
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

function extractTitle(content) {
  const lines = content.split(/\r?\n/);
  for (const line of lines) {
    const match = line.match(/^#\s+(.+?)\s*$/);
    if (match) {
      return match[1].trim();
    }
  }
  return null;
}

async function readPublicDocsConfig() {
  const raw = await fs.readFile(PUBLIC_DOCS_CONFIG, 'utf-8');
  const parsed = JSON.parse(raw);
  if (!Array.isArray(parsed.documents)) {
    throw new Error('docs/public-docs.json must contain a "documents" array.');
  }
  return parsed.documents;
}

function parseEntry(entry) {
  if (typeof entry === 'string') {
    return { source: entry, stringEntry: true };
  }
  if (entry && typeof entry === 'object' && typeof entry.source === 'string') {
    const { source, category, slug, title } = entry;
    if (!source.endsWith('.md')) {
      throw new Error(`Public doc source must end with ".md": ${source}`);
    }
    if (!category) {
      throw new Error(`Public doc entry for "${source}" is missing "category".`);
    }
    if (!slug) {
      throw new Error(`Public doc entry for "${source}" is missing "slug".`);
    }
    return { source, category, slug: slugifyTitle(slug), title: title ?? null, stringEntry: false };
  }
  throw new Error(
    'Each public document entry must be a string path or an object with "source", "category", "slug".',
  );
}

async function cleanOutput(outputDir) {
  await fs.rm(outputDir, { recursive: true, force: true });
  await fs.mkdir(outputDir, { recursive: true });
}

async function buildDocs(outputDir) {
  await cleanOutput(outputDir);
  const publicDocs = await readPublicDocsConfig();
  const seenSources = new Set();
  const orderByCategory = new Map();
  const docs = [];

  for (const entry of publicDocs) {
    const parsed = parseEntry(entry);
    const { source, stringEntry } = parsed;

    if (seenSources.has(source)) {
      throw new Error(`Duplicate public doc source: ${source}`);
    }
    seenSources.add(source);

    const categorySlug = stringEntry
      ? source.split('/')[1]
      : parsed.category;
    const category = CATEGORY_BY_SLUG.get(categorySlug);
    if (!category) {
      throw new Error(`Unsupported public docs category "${categorySlug}" in ${source}.`);
    }

    const sourceFile = path.join(REPO_ROOT, source);
    const stat = await fs.stat(sourceFile).catch(() => null);
    if (!stat?.isFile()) {
      throw new Error(`Public doc not found: ${source}`);
    }

    const content = await fs.readFile(sourceFile, 'utf-8');
    const extractedTitle = extractTitle(content);
    const title = stringEntry
      ? (extractedTitle ?? path.basename(source, '.md'))
      : (parsed.title ?? extractedTitle ?? path.basename(source, '.md'));

    const slug = stringEntry
      ? slugifyTitle(path.basename(source, '.md'))
      : parsed.slug;

    const outputCategoryDir = path.join(outputDir, categorySlug);
    const outputFile = path.join(outputCategoryDir, `${slug}.md`);

    await fs.mkdir(outputCategoryDir, { recursive: true });
    await fs.writeFile(outputFile, content, 'utf-8');

    const order = (orderByCategory.get(categorySlug) ?? 0) + 1;
    orderByCategory.set(categorySlug, order);

    docs.push({
      category: categorySlug,
      slug,
      title,
      order,
      path: `docs/${categorySlug}/${slug}.md`,
      sourcePath: source,
    });
  }

  const manifest = {
    categories: CATEGORIES,
    docs,
  };

  await fs.writeFile(
    path.join(outputDir, 'manifest.json'),
    `${JSON.stringify(manifest, null, 2)}\n`,
    'utf-8',
  );

  return manifest;
}

async function listFiles(rootDir) {
  const files = [];

  async function walk(currentDir) {
    const entries = await fs.readdir(currentDir, { withFileTypes: true }).catch((err) => {
      if (err.code === 'ENOENT') {
        return [];
      }
      throw err;
    });

    for (const entry of entries) {
      const fullPath = path.join(currentDir, entry.name);
      if (entry.isDirectory()) {
        await walk(fullPath);
        continue;
      }
      if (entry.isFile()) {
        files.push(path.relative(rootDir, fullPath).replace(/\\/g, '/'));
      }
    }
  }

  await walk(rootDir);
  return files.sort();
}

async function assertDirectoriesEqual(expectedDir, actualDir) {
  const [expectedFiles, actualFiles] = await Promise.all([
    listFiles(expectedDir),
    listFiles(actualDir),
  ]);

  const expectedSet = new Set(expectedFiles);
  const actualSet = new Set(actualFiles);
  const missing = expectedFiles.filter((file) => !actualSet.has(file));
  const extra = actualFiles.filter((file) => !expectedSet.has(file));

  if (missing.length > 0 || extra.length > 0) {
    throw new Error([
      'Generated public docs snapshot is out of sync.',
      missing.length > 0 ? `Missing files: ${missing.join(', ')}` : null,
      extra.length > 0 ? `Extra files: ${extra.join(', ')}` : null,
      'Run: npm run sync:docs',
    ].filter(Boolean).join('\n'));
  }

  for (const file of expectedFiles) {
    const [expected, actual] = await Promise.all([
      fs.readFile(path.join(expectedDir, file), 'utf-8'),
      fs.readFile(path.join(actualDir, file), 'utf-8'),
    ]);
    if (expected !== actual) {
      throw new Error([
        'Generated public docs snapshot is out of sync.',
        `Changed file: ${file}`,
        'Run: npm run sync:docs',
      ].join('\n'));
    }
  }
}

async function checkDocsSync() {
  const tempDir = await fs.mkdtemp(path.join(tmpdir(), 'public-docs-'));
  try {
    await buildDocs(tempDir);
    await assertDirectoriesEqual(tempDir, PUBLIC_OUTPUT);
    console.log('[docs] Public docs snapshot is up to date.');
  } finally {
    await fs.rm(tempDir, { recursive: true, force: true });
  }
}

async function main() {
  if (CHECK_MODE) {
    await checkDocsSync();
    return;
  }

  const manifest = await buildDocs(PUBLIC_OUTPUT);
  console.log(
    `[docs] Synced ${manifest.docs.length} public docs -> ${path.relative(REPO_ROOT, PUBLIC_OUTPUT)}`,
  );
}

main().catch((err) => {
  console.error('[docs] Build failed:', err);
  process.exit(1);
});
