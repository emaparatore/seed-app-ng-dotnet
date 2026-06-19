import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { DocsService } from './docs.service';
import type { DocsManifest } from '../types/docs';

describe('DocsService', () => {
  let service: DocsService;
  let httpMock: HttpTestingController;

  const manifest: DocsManifest = {
    categories: [
      { slug: 'architecture', title: 'Architecture', order: 1 },
      { slug: 'modules', title: 'Modules', order: 2 },
      { slug: 'getting-started', title: 'Getting Started', order: 3 },
    ],
    docs: [
      { category: 'architecture', slug: 'authentication', title: 'Auth', order: 1, path: 'docs/architecture/authentication.md', sourcePath: 'docs/architecture/authentication.md' },
      { category: 'architecture', slug: 'bootstrap-console', title: 'Bootstrap', order: 2, path: 'docs/architecture/bootstrap-console.md', sourcePath: 'docs/architecture/bootstrap-console.md' },
      { category: 'modules', slug: 'smtp', title: 'SMTP', order: 1, path: 'docs/modules/smtp-configuration.md', sourcePath: 'docs/modules/smtp-configuration.md' },
      { category: 'getting-started', slug: 'project-overview', title: 'Project Overview', order: 1, path: 'docs/getting-started/project-overview.md', sourcePath: 'README.md' },
    ],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DocsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loads manifest and groups docs by category', async () => {
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush(manifest);
    await promise;

    const groups = service.docsByCategory();
    expect(groups.length).toBe(3);
    expect(groups[0].category.slug).toBe('architecture');
    expect(groups[0].docs.map((d) => d.slug)).toEqual(['authentication', 'bootstrap-console']);
    expect(groups[1].category.slug).toBe('modules');
    expect(groups[1].docs.map((d) => d.slug)).toEqual(['smtp']);
  });

  it('caches the manifest after first load', async () => {
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush(manifest);
    await promise;
    await service.ensureLoaded();
    httpMock.expectNone('docs/manifest.json');
  });

  it('finds docs and returns null for unknown', async () => {
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush(manifest);
    await promise;

    expect(service.findDoc('architecture', 'authentication')?.path).toBe(
      'docs/architecture/authentication.md',
    );
    expect(service.findDoc('architecture', 'missing')).toBeNull();
  });

  it('computes nav item with previous and next across categories', async () => {
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush(manifest);
    await promise;

    const first = service.getNavItem('architecture', 'authentication');
    expect(first?.previous).toBeNull();
    expect(first?.next?.slug).toBe('bootstrap-console');

    const last = service.getNavItem('modules', 'smtp');
    expect(last?.previous?.slug).toBe('bootstrap-console');
    expect(last?.next?.slug).toBe('project-overview');
  });

  it('renders markdown without duplicating the leading title and sanitizes dangerous HTML', async () => {
    const dirty = '# Hello <script>alert(1)</script>\n\n[link](https://example.com)';
    const html = await service.renderMarkdown(dirty, 'docs/test.md');
    expect(html).not.toContain('<script>');
    expect(html).toContain('href="https://example.com"');
    expect(html).not.toContain('<h1>');
    expect(html).toBe('<p><a href="https://example.com">link</a></p>\n');
  });

  it('keeps lower-level headings after removing the leading title', async () => {
    const markdown = '# Hello\n\n## Details\n\nBody';
    const html = await service.renderMarkdown(markdown, 'docs/test.md');
    expect(html).toContain('<h2 id="details">Details</h2>');
    expect(html).toContain('<p>Body</p>');
    expect(html).not.toContain('<h1>Hello</h1>');
  });

  it('rewrites internal markdown links from docs path', async () => {
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush(manifest);
    await promise;

    const md = 'See [Auth](docs/architecture/authentication.md) for details.';
    const html = await service.renderMarkdown(md, 'docs/modules/smtp-configuration.md');
    expect(html).toContain('href="/docs/architecture/authentication"');
    expect(html).not.toContain('href="docs/architecture/authentication.md"');
  });

  it('rewrites internal markdown links relative to source folder', async () => {
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush(manifest);
    await promise;

    const md = 'See [Bootstrap](bootstrap-console.md) for details.';
    const html = await service.renderMarkdown(md, 'docs/architecture/authentication.md');
    expect(html).toContain('href="/docs/architecture/bootstrap-console"');
    expect(html).not.toContain('href="bootstrap-console.md"');
  });

  it('rewrites internal markdown links with fragments', async () => {
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush(manifest);
    await promise;

    const md = 'See [Bootstrap](bootstrap-console.md#usage) for details.';
    const html = await service.renderMarkdown(md, 'docs/architecture/authentication.md');
    expect(html).toContain('href="/docs/architecture/bootstrap-console#usage"');
    expect(html).not.toContain('href="bootstrap-console.md#usage"');
  });

  it('rewrites same-document anchor links when current doc is known', async () => {
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush(manifest);
    await promise;

    const md = 'Jump to [SMTP config](#setup).';
    const html = await service.renderMarkdown(md, 'docs/modules/smtp-configuration.md');
    expect(html).toContain('href="/docs/modules/smtp#setup"');
  });

  it('rewrites internal markdown links from root-level source like README.md', async () => {
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush(manifest);
    await promise;

    const md = 'See [Auth](docs/architecture/authentication.md) for details.';
    const html = await service.renderMarkdown(md, 'README.md');
    expect(html).toContain('href="/docs/architecture/authentication"');
    expect(html).not.toContain('href="docs/architecture/authentication.md"');
  });

  it('does not rewrite external links or anchors', async () => {
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush(manifest);
    await promise;

    const md = 'External [GitHub](https://github.com) and [anchor](#section) and [mail](mailto:test@test.com).';
    const html = await service.renderMarkdown(md, 'docs/test.md');
    expect(html).toContain('href="https://github.com"');
    expect(html).toContain('href="#section"');
    expect(html).toContain('href="mailto:test@test.com"');
  });

  it('adds stable ids to headings for table-of-contents anchors', async () => {
    const markdown = '# Title\n\n## Sistema di permessi (RBAC)\n\n### 6.5 Proteggi staging con Cloudflare Access';
    const html = await service.renderMarkdown(markdown, 'docs/test.md');
    expect(html).toContain('<h2 id="sistema-di-permessi-rbac">Sistema di permessi (RBAC)</h2>');
    expect(html).toContain('<h3 id="65-proteggi-staging-con-cloudflare-access">6.5 Proteggi staging con Cloudflare Access</h3>');
  });

  it('does not rewrite links to unknown markdown files', async () => {
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush(manifest);
    await promise;

    const md = 'See [Unknown](unknown-file.md).';
    const html = await service.renderMarkdown(md, 'docs/test.md');
    expect(html).toContain('href="unknown-file.md"');
  });

  it('captures load error when manifest request fails', async () => {
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush('not found', { status: 404, statusText: 'Not Found' });
    await promise;
    expect(service.getLoadError()).toBeTruthy();
    expect(service.docs().length).toBe(0);
  });

  it('loads markdown content for a given path', async () => {
    const promise = service.loadMarkdown('docs/architecture/authentication.md');
    httpMock.expectOne('docs/architecture/authentication.md').flush('# Auth\n\nbody');
    const md = await promise;
    expect(md).toBe('# Auth\n\nbody');
  });

  it('returns null from getFirstDoc and getFirstDocForCategory when empty', async () => {
    const emptyManifest: DocsManifest = { categories: [], docs: [] };
    const promise = service.ensureLoaded();
    httpMock.expectOne('docs/manifest.json').flush(emptyManifest);
    await promise;
    expect(service.getFirstDoc()).toBeNull();
    expect(service.getFirstDocForCategory('architecture')).toBeNull();
  });
});
