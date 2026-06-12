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
    ],
    docs: [
      { category: 'architecture', slug: 'authentication', title: 'Auth', order: 1, path: 'docs/architecture/authentication.md' },
      { category: 'architecture', slug: 'bootstrap-console', title: 'Bootstrap', order: 2, path: 'docs/architecture/bootstrap-console.md' },
      { category: 'modules', slug: 'smtp', title: 'SMTP', order: 1, path: 'docs/modules/smtp-configuration.md' },
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
    expect(groups.length).toBe(2);
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
    expect(last?.next).toBeNull();
  });

  it('renders markdown and sanitizes dangerous HTML', async () => {
    const dirty = '# Hello <script>alert(1)</script>\n\n[link](https://example.com)';
    const html = await service.renderMarkdown(dirty);
    expect(html).toContain('<h1>');
    expect(html).toContain('Hello');
    expect(html).not.toContain('<script>');
    expect(html).toContain('href="https://example.com"');
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
