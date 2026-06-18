import { Component, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { toSignal } from '@angular/core/rxjs-interop';
import { DocsService } from '../../services/docs.service';
import type { DocsCategory, DocsEntry, DocsNavItem } from '../../types/docs';

type ViewState =
  | { kind: 'loading' }
  | { kind: 'not-found' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; doc: DocsEntry; nav: DocsNavItem; html: string };

@Component({
  selector: 'app-docs-viewer',
  imports: [RouterLink, MatIconModule, MatButtonModule, MatProgressSpinnerModule],
  templateUrl: './docs-viewer.html',
  styleUrl: './docs-viewer.scss',
})
export class DocsViewer {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly docsService = inject(DocsService);

  private readonly params = toSignal(this.route.paramMap, { initialValue: null });

  protected readonly state = signal<ViewState>({ kind: 'loading' });

  protected readonly currentCategory = computed<DocsCategory | null>(() => {
    const s = this.state();
    if (s.kind !== 'ready') {
      return null;
    }
    const cat = this.docsService.categories().find((c) => c.slug === s.doc.category);
    return cat ?? null;
  });

  protected readonly currentCategoryTitle = computed(
    () => this.currentCategory()?.title ?? '',
  );

  constructor() {
    effect(() => {
      const map = this.params();
      if (!map) {
        return;
      }
      const category = map.get('category') ?? '';
      const slug = map.get('slug') ?? '';
      void this.load(category, slug);
    });
  }

  private async load(category: string, slug: string): Promise<void> {
    if (!category || !slug) {
      this.state.set({ kind: 'not-found' });
      return;
    }

    await this.docsService.ensureLoaded();

    const doc = this.docsService.findDoc(category, slug);
    if (!doc) {
      this.state.set({ kind: 'not-found' });
      return;
    }

    this.state.set({ kind: 'loading' });

    try {
      const markdown = await this.docsService.loadMarkdown(doc.path);
      const html = await this.docsService.renderMarkdown(markdown, doc.sourcePath);
      const nav = this.docsService.getNavItem(category, slug);
      if (!nav) {
        this.state.set({ kind: 'not-found' });
        return;
      }
      this.state.set({ kind: 'ready', doc, nav, html });
    } catch (err) {
      this.state.set({
        kind: 'error',
        message: err instanceof Error ? err.message : 'Failed to load document.',
      });
    }
  }

  protected retry(): void {
    const map = this.params();
    if (!map) {
      return;
    }
    void this.load(map.get('category') ?? '', map.get('slug') ?? '');
  }

  protected goHome(): void {
    void this.router.navigate(['/']);
  }
}
