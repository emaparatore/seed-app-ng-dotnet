import { Component, computed, inject } from '@angular/core';
import { MatSidenavModule } from '@angular/material/sidenav';
import { RouterOutlet } from '@angular/router';
import { DocsService } from '../../services/docs.service';
import { DocsSidebar } from './docs-sidebar';

@Component({
  selector: 'app-docs-layout',
  imports: [MatSidenavModule, RouterOutlet, DocsSidebar],
  templateUrl: './docs-layout.html',
  styleUrl: './docs-layout.scss',
})
export class DocsLayout {
  private readonly docsService = inject(DocsService);
  protected readonly hasDocs = computed(() => this.docsService.docs().length > 0);

  constructor() {
    void this.docsService.ensureLoaded();
  }
}
