import { DOCUMENT } from '@angular/common';
import { Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { toSignal } from '@angular/core/rxjs-interop';
import { DocsService, slugifyHeadingForDocs } from '../../services/docs.service';
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
  private static readonly fragmentScrollOffset = 88;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly docsService = inject(DocsService);
  private readonly document = inject(DOCUMENT);
  private readonly docsBody = viewChild<ElementRef<HTMLElement>>('docsBody');
  private readonly locationHash = signal<string | null>(this.readLocationHash());

  private readonly params = toSignal(this.route.paramMap, { initialValue: null });
  private readonly fragment = toSignal(this.route.fragment, { initialValue: null });

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
    this.installHashChangeListener();

    effect(() => {
      const map = this.params();
      if (!map) {
        return;
      }
      const category = map.get('category') ?? '';
      const slug = map.get('slug') ?? '';
      void this.load(category, slug);
    });

    effect(() => {
      const fragment = this.fragment() ?? this.locationHash();
      const s = this.state();
      if (s.kind !== 'ready' || !fragment) {
        return;
      }

      this.scheduleFragmentScroll(fragment);
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

  protected onDocumentClick(event: MouseEvent): void {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }

    const target = event.target;
    const anchor = this.findAnchorFromEventTarget(target);
    if (!(anchor instanceof HTMLAnchorElement)) {
      return;
    }

    if (anchor.target && anchor.target !== '_self') {
      return;
    }

    const href = anchor.getAttribute('href');
    if (!href) {
      return;
    }

    const targetUrl = new URL(anchor.href, this.document.baseURI);
    const currentUrl = new URL(this.document.location.href);
    if (targetUrl.origin !== currentUrl.origin || !targetUrl.pathname.startsWith('/docs/')) {
      return;
    }

    event.preventDefault();

    const fragment = targetUrl.hash ? decodeURIComponent(targetUrl.hash.slice(1)) : null;
    const targetPath = `${targetUrl.pathname}${targetUrl.search}`;
    const currentPath = `${currentUrl.pathname}${currentUrl.search}`;

    if (targetPath === currentPath) {
      void this.router.navigate([], {
        relativeTo: this.route,
        fragment: fragment ?? undefined,
      });

      if (fragment) {
        this.scheduleFragmentScroll(fragment);
      }

      return;
    }

    void this.router.navigateByUrl(`${targetPath}${targetUrl.hash}`);
  }

  private findAnchorFromEventTarget(target: EventTarget | null): HTMLAnchorElement | null {
    if (target instanceof HTMLAnchorElement) {
      return target;
    }

    if (target instanceof Element) {
      const anchor = target.closest('a');
      return anchor instanceof HTMLAnchorElement ? anchor : null;
    }

    if (target instanceof Node) {
      const parent = target.parentElement;
      if (!parent) {
        return null;
      }

      const anchor = parent.closest('a');
      return anchor instanceof HTMLAnchorElement ? anchor : null;
    }

    return null;
  }

  protected goHome(): void {
    void this.router.navigate(['/']);
  }

  private scheduleFragmentScroll(fragment: string): void {
    this.applyHeadingIds();

    const scroll = () => this.scrollToFragment(fragment);
    const requestAnimationFrame = this.document.defaultView?.requestAnimationFrame;

    if (requestAnimationFrame) {
      requestAnimationFrame(() => requestAnimationFrame(() => scroll()));
      return;
    }

    setTimeout(scroll, 0);
  }

  private installHashChangeListener(): void {
    const view = this.document.defaultView;
    if (!view) {
      return;
    }

    view.addEventListener('hashchange', () => {
      const hash = this.readLocationHash();
      this.locationHash.set(hash);
    });
  }

  private readLocationHash(): string | null {
    const hash = this.document.location.hash;
    if (!hash || hash === '#') {
      return null;
    }

    return decodeURIComponent(hash.slice(1));
  }

  private applyHeadingIds(): void {
    const docsBody = this.docsBody()?.nativeElement;
    if (!docsBody) {
      return;
    }

    const slugCounts = new Map<string, number>();
    const headings = docsBody.querySelectorAll<HTMLElement>('h1, h2, h3, h4, h5, h6');

    for (const heading of headings) {
      const baseSlug = slugifyHeadingForDocs(heading.textContent ?? '');
      if (!baseSlug) {
        continue;
      }

      const count = slugCounts.get(baseSlug) ?? 0;
      slugCounts.set(baseSlug, count + 1);
      heading.id = count === 0 ? baseSlug : `${baseSlug}-${count}`;
    }
  }

  private scrollToFragment(fragment: string): void {
    const target = this.document.getElementById(fragment);
    if (!target) {
      return;
    }

    const container = this.findScrollContainer(target);
    const offset = DocsViewer.fragmentScrollOffset;

    target.scrollIntoView({ block: 'start', behavior: 'auto' });

    if (container instanceof HTMLElement) {
      container.scrollTop = Math.max(container.scrollTop - offset, 0);
      return;
    }

    const scrollingElement = this.document.scrollingElement;
    if (scrollingElement) {
      scrollingElement.scrollTop = Math.max(scrollingElement.scrollTop - offset, 0);
    }
  }

  private findScrollContainer(target: HTMLElement): HTMLElement | null {
    const view = this.document.defaultView;
    let current: HTMLElement | null = target;

    while (current) {
      if (view) {
        const style = view.getComputedStyle(current);
        const overflowY = style.overflowY;
        const isScrollable = /(auto|scroll|overlay)/.test(overflowY) && current.scrollHeight > current.clientHeight;
        if (isScrollable) {
          return current;
        }
      }

      if (current.classList.contains('docs-content') || current.classList.contains('mat-drawer-content')) {
        return current;
      }

      current = current.parentElement;
    }

    return null;
  }
}
