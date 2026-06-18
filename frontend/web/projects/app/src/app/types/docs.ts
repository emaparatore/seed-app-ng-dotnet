export interface DocsCategory {
  readonly slug: string;
  readonly title: string;
  readonly order: number;
}

export interface DocsEntry {
  readonly category: string;
  readonly slug: string;
  readonly title: string;
  readonly order: number;
  readonly path: string;
  readonly sourcePath: string;
}

export interface DocsManifest {
  readonly categories: readonly DocsCategory[];
  readonly docs: readonly DocsEntry[];
}

export interface DocsNavItem {
  readonly current: DocsEntry;
  readonly previous: DocsEntry | null;
  readonly next: DocsEntry | null;
}
