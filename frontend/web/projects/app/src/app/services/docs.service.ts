import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import type { DocsCategory, DocsEntry, DocsManifest, DocsNavItem } from '../types/docs';

type MarkedModule = typeof import('marked');
type DomPurifyModule = typeof import('dompurify');

let markedModulePromise: Promise<MarkedModule> | null = null;
let domPurifyPromise: Promise<DomPurifyModule> | null = null;

async function getMarked(): Promise<MarkedModule> {
  markedModulePromise ??= import('marked');
  return markedModulePromise;
}

async function getDomPurify(): Promise<DomPurifyModule> {
  domPurifyPromise ??= import('dompurify');
  return domPurifyPromise;
}

function dirname(p: string): string {
  const idx = p.lastIndexOf('/');
  return idx >= 0 ? p.substring(0, idx) : '';
}

function resolveRelativePath(link: string, baseDir: string): string {
  const baseParts = baseDir ? baseDir.split('/').filter(Boolean) : [];
  const linkParts = link.split('/');
  const resolved: string[] = [...baseParts];

  for (const part of linkParts) {
    if (part === '.' || part === '') {
      continue;
    }
    if (part === '..') {
      if (resolved.length > 0) {
        resolved.pop();
      }
    } else {
      resolved.push(part);
    }
  }

  return resolved.join('/');
}

function stripHtmlTags(value: string): string {
  return value.replace(/<[^>]*>/g, '');
}

export function slugifyHeadingForDocs(value: string): string {
  return stripHtmlTags(value)
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/&[a-z0-9#]+;/g, '')
    .replace(/[^a-z0-9\s-]/g, '')
    .trim()
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-');
}

@Injectable({ providedIn: 'root' })
export class DocsService {
  private readonly http = inject(HttpClient);

  private readonly manifest = signal<DocsManifest | null>(null);
  private readonly loadError = signal<string | null>(null);

  readonly categories = computed<readonly DocsCategory[]>(
    () => this.manifest()?.categories ?? [],
  );

  readonly docs = computed<readonly DocsEntry[]>(
    () => this.manifest()?.docs ?? [],
  );

  readonly docsByCategory = computed<readonly { category: DocsCategory; docs: DocsEntry[] }[]>(
    () => {
      const manifest = this.manifest();
      if (!manifest) {
        return [];
      }
      return manifest.categories
        .map((category) => ({
          category,
          docs: manifest.docs
            .filter((doc) => doc.category === category.slug)
            .slice()
            .sort((a, b) => a.order - b.order),
        }))
        .filter((group) => group.docs.length > 0);
    },
  );

  private readonly docsBySourcePath = computed<Map<string, DocsEntry>>(() => {
    const map = new Map<string, DocsEntry>();
    for (const doc of this.docs()) {
      map.set(doc.sourcePath, doc);
    }
    return map;
  });

  constructor() {
    void this.warmupRenderers();
  }

  private async warmupRenderers(): Promise<void> {
    const [marked, dompurify] = await Promise.all([getMarked(), getDomPurify()]);
    marked.marked.setOptions({ gfm: true, breaks: false });
    void dompurify.default;
  }

  async ensureLoaded(): Promise<void> {
    if (this.manifest()) {
      return;
    }
    try {
      const manifest = await firstValueFrom(
        this.http.get<DocsManifest>('docs/manifest.json'),
      );
      this.manifest.set(manifest);
      this.loadError.set(null);
    } catch (err) {
      this.loadError.set(this.formatError(err));
    }
  }

  isLoaded(): boolean {
    return this.manifest() !== null;
  }

  getLoadError(): string | null {
    return this.loadError();
  }

  findDoc(category: string, slug: string): DocsEntry | null {
    return this.docs().find((doc) => doc.category === category && doc.slug === slug) ?? null;
  }

  getFirstDocForCategory(category: string): DocsEntry | null {
    const group = this.docsByCategory().find((g) => g.category.slug === category);
    return group?.docs[0] ?? null;
  }

  getFirstDoc(): DocsEntry | null {
    return this.docs()[0] ?? null;
  }

  getNavItem(category: string, slug: string): DocsNavItem | null {
    const sorted = this.docs().slice().sort((a, b) => {
      if (a.category === b.category) {
        return a.order - b.order;
      }
      const aCat = this.categories().find((c) => c.slug === a.category);
      const bCat = this.categories().find((c) => c.slug === b.category);
      return (aCat?.order ?? 0) - (bCat?.order ?? 0);
    });
    const index = sorted.findIndex(
      (doc) => doc.category === category && doc.slug === slug,
    );
    if (index === -1) {
      return null;
    }
    return {
      current: sorted[index],
      previous: index > 0 ? sorted[index - 1] : null,
      next: index < sorted.length - 1 ? sorted[index + 1] : null,
    };
  }

  async loadMarkdown(path: string): Promise<string> {
    const response = await firstValueFrom(
      this.http.get(path, { responseType: 'text' }),
    );
    return response;
  }

  async renderMarkdown(markdown: string, sourcePath: string): Promise<string> {
    const rewritten = this.rewriteInternalLinks(
      this.stripLeadingTitle(markdown),
      sourcePath,
    );
    const [marked, dompurify] = await Promise.all([getMarked(), getDomPurify()]);
    const raw = marked.marked.parse(rewritten, { async: false }) as string;
    const withAnchors = this.addHeadingAnchors(raw);
    return dompurify.default.sanitize(withAnchors, {
      ADD_ATTR: ['target', 'rel'],
    });
  }

  private stripLeadingTitle(markdown: string): string {
    return markdown.replace(/^#\s+.+?(?:\r?\n){1,2}/, '');
  }

  private rewriteInternalLinks(markdown: string, sourcePath: string): string {
    const baseDir = dirname(sourcePath);
    const currentDoc = this.docsBySourcePath().get(sourcePath) ?? null;

    return markdown.replace(
      /\[([^\]]*)\]\((#[^)\s]+|[^)\s]+\.md(?:#[^)\s]+)?)(?:\s+"[^"]*")?\)/g,
      (match, text, linkPath) => {
        if (/^(https?:\/\/|mailto:)/.test(linkPath)) {
          return match;
        }

        if (linkPath.startsWith('#')) {
          if (!currentDoc) {
            return match;
          }

          return `[${text}](/docs/${currentDoc.category}/${currentDoc.slug}${linkPath})`;
        }

        const [docPath, fragment] = linkPath.split('#', 2);

        const resolved = this.resolveDocSourcePath(docPath, baseDir);
        if (!resolved) {
          return match;
        }

        const doc = this.docsBySourcePath().get(resolved);
        if (!doc) {
          return match;
        }

        const docUrl = `/docs/${doc.category}/${doc.slug}`;
        return `[${text}](${fragment ? `${docUrl}#${fragment}` : docUrl})`;
      },
    );
  }

  private addHeadingAnchors(html: string): string {
    const slugCounts = new Map<string, number>();

    return html.replace(/<h([1-6])>([\s\S]*?)<\/h\1>/g, (match, level, content) => {
      const baseSlug = slugifyHeadingForDocs(content);
      if (!baseSlug) {
        return match;
      }

      const count = slugCounts.get(baseSlug) ?? 0;
      slugCounts.set(baseSlug, count + 1);
      const slug = count === 0 ? baseSlug : `${baseSlug}-${count}`;

      return `<h${level} id="${slug}">${content}</h${level}>`;
    });
  }

  private resolveDocSourcePath(linkPath: string, baseDir: string): string | null {
    if (linkPath.startsWith('docs/')) {
      return linkPath;
    }

    if (!baseDir) {
      return linkPath;
    }

    return resolveRelativePath(linkPath, baseDir);
  }

  private formatError(err: unknown): string {
    if (err instanceof Error) {
      return err.message;
    }
    return 'Failed to load documentation manifest.';
  }
}
