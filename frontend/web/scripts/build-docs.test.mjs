#!/usr/bin/env node
import { promises as fs } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { tmpdir } from 'node:os';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const REPO_ROOT = path.resolve(__dirname, '..', '..', '..');
const SCRIPT = path.join(__dirname, 'build-docs.mjs');

function assert(cond, msg) {
  if (!cond) {
    console.error(`[build-docs.test] FAIL: ${msg}`);
    process.exit(1);
  }
  console.log(`[build-docs.test] ok: ${msg}`);
}

async function main() {
  const tempRoot = await fs.mkdtemp(path.join(tmpdir(), 'docs-build-'));
  const sourceDocs = path.join(tempRoot, 'docs');
  const projectApp = path.join(tempRoot, 'app', 'projects', 'app', 'public', 'docs');
  const projectRoot = path.join(tempRoot, 'app', 'projects', 'app');
  const webRoot = path.join(tempRoot, 'app');
  const scriptsDir = path.join(webRoot, 'scripts');

  await fs.mkdir(sourceDocs, { recursive: true });
  for (const [subdir, files] of Object.entries({
    architecture: { 'auth.md': '# Auth\n\nbody', 'should-be-excluded.md': '# Nope\n\nhidden' },
    modules: { 'smtp.md': '# SMTP\n\ncfg' },
  })) {
    const dir = path.join(sourceDocs, subdir);
    await fs.mkdir(dir, { recursive: true });
    for (const [name, content] of Object.entries(files)) {
      await fs.writeFile(path.join(dir, name), content, 'utf-8');
    }
  }

  await fs.mkdir(scriptsDir, { recursive: true });
  const scriptBody = await fs.readFile(SCRIPT, 'utf-8');
  const patched = scriptBody
    .replace(
      /const EXCLUDED_FILES = new Set\(\[[\s\S]*?\]\);/,
      "const EXCLUDED_FILES = new Set([\n  'docs/architecture/should-be-excluded.md',\n]);",
    )
    .replace(
      "path.resolve(__dirname, '..', '..', '..')",
      "process.env.TEST_REPO_ROOT",
    )
    .replace(
      "path.join(__dirname, '..', 'projects', 'app', 'public', 'docs')",
      "process.env.TEST_PUBLIC_OUTPUT",
    );
  const patchedScript = path.join(scriptsDir, 'build-docs.mjs');
  await fs.writeFile(patchedScript, patched, 'utf-8');

  const env = {
    ...process.env,
    TEST_REPO_ROOT: tempRoot,
    TEST_PUBLIC_OUTPUT: projectApp,
  };
  const result = spawnSync(process.execPath, [patchedScript], { env, encoding: 'utf-8' });
  if (result.status !== 0) {
    console.error(result.stdout);
    console.error(result.stderr);
    process.exit(1);
  }

  const manifest = JSON.parse(
    await fs.readFile(path.join(projectApp, 'manifest.json'), 'utf-8'),
  );

  assert(manifest.docs.length === 2, 'manifest contains exactly 2 docs (excluded file skipped)');
  assert(
    manifest.docs.every((d) => d.path !== 'docs/architecture/should-be-excluded.md'),
    'excluded file is not in manifest',
  );
  assert(
    manifest.docs.find((d) => d.path === 'docs/architecture/auth.md')?.order === 1,
    'remaining docs keep contiguous order (no holes)',
  );
  assert(
    manifest.docs.find((d) => d.path === 'docs/modules/smtp.md')?.order === 1,
    'modules order restarts at 1',
  );

  const authExists = await fs
    .stat(path.join(projectApp, 'architecture', 'auth.md'))
    .then(() => true)
    .catch(() => false);
  const excludedExists = await fs
    .stat(path.join(projectApp, 'architecture', 'should-be-excluded.md'))
    .then(() => true)
    .catch(() => false);
  assert(authExists, 'included file copied to output');
  assert(!excludedExists, 'excluded file NOT copied to output');

  console.log('[build-docs.test] All assertions passed');
  await fs.rm(tempRoot, { recursive: true, force: true });
}

main().catch((err) => {
  console.error('[build-docs.test] crashed:', err);
  process.exit(1);
});
