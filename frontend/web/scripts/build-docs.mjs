#!/usr/bin/env node
import { promises as fs } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const REPO_ROOT = path.resolve(__dirname, '..', '..', '..');
const DOCS_SOURCE = path.join(REPO_ROOT, 'docs');
const PUBLIC_OUTPUT = path.join(__dirname, '..', 'projects', 'app', 'public', 'docs');

const CATEGORIES = [
  { slug: 'getting-started', title: 'Getting Started', order: 1 },
  { slug: 'architecture', title: 'Architecture', order: 2 },
  { slug: 'modules', title: 'Modules', order: 3 },
  { slug: 'operations', title: 'Operations', order: 4 },
  { slug: 'compliance', title: 'Compliance', order: 5 },
  { slug: 'seed', title: 'Seed', order: 6 },
];

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

async function cleanOutput() {
  await fs.rm(PUBLIC_OUTPUT, { recursive: true, force: true });
  await fs.mkdir(PUBLIC_OUTPUT, { recursive: true });
}

async function buildCategory(category) {
  const sourceDir = path.join(DOCS_SOURCE, category.slug);
  const outputDir = path.join(PUBLIC_OUTPUT, category.slug);

  let entries;
  try {
    entries = await fs.readdir(sourceDir, { withFileTypes: true });
  } catch (err) {
    if (err.code === 'ENOENT') {
      console.warn(`[docs] Skipping missing category: ${category.slug}`);
      return [];
    }
    throw err;
  }

  await fs.mkdir(outputDir, { recursive: true });

  const docs = [];
  let order = 1;
  for (const entry of entries) {
    if (!entry.isFile() || !entry.name.endsWith('.md')) {
      continue;
    }
    const sourceFile = path.join(sourceDir, entry.name);
    const content = await fs.readFile(sourceFile, 'utf-8');
    const title = extractTitle(content) ?? entry.name.replace(/\.md$/, '');
    const fileBase = entry.name.replace(/\.md$/, '');
    const slug = slugifyTitle(fileBase);
    const destFile = path.join(outputDir, `${slug}.md`);

    await fs.writeFile(destFile, content, 'utf-8');

    docs.push({
      category: category.slug,
      slug,
      title,
      order: order++,
      path: `docs/${category.slug}/${slug}.md`,
    });
  }

  docs.sort((a, b) => a.title.localeCompare(b.title));
  return docs.map((doc, index) => ({ ...doc, order: index + 1 }));
}

async function main() {
  await cleanOutput();

  const allDocs = [];
  for (const category of CATEGORIES) {
    const docs = await buildCategory(category);
    allDocs.push(...docs);
  }

  const manifest = {
    categories: CATEGORIES,
    docs: allDocs,
  };

  await fs.writeFile(
    path.join(PUBLIC_OUTPUT, 'manifest.json'),
    JSON.stringify(manifest, null, 2),
    'utf-8',
  );

  console.log(
    `[docs] Built ${allDocs.length} docs across ${CATEGORIES.length} categories -> ${path.relative(REPO_ROOT, PUBLIC_OUTPUT)}`,
  );
}

main().catch((err) => {
  console.error('[docs] Build failed:', err);
  process.exit(1);
});
