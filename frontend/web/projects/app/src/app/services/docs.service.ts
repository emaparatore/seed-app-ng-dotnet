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

  constructor() {
    void this.warmupRenderers();
  }

  private async warmupRenderers(): Promise<void> {
    const [marked, dompurify] = await Promise.all([getMarked(), getDomPurify()]);
    marked.marked.setOptions({ gfm: true, breaks: false });
    // Touch the default export so the bundler keeps it.
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

  async renderMarkdown(markdown: string): Promise<string> {
    const [marked, dompurify] = await Promise.all([getMarked(), getDomPurify()]);
    const raw = marked.marked.parse(markdown, { async: false }) as string;
    return dompurify.default.sanitize(raw, {
      ADD_ATTR: ['target', 'rel'],
    });
  }

  private formatError(err: unknown): string {
    if (err instanceof Error) {
      return err.message;
    }
    return 'Failed to load documentation manifest.';
  }
}
