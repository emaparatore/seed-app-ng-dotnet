import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { DocsService } from '../../services/docs.service';

@Component({
  selector: 'app-docs-sidebar',
  imports: [RouterLink, RouterLinkActive, MatListModule, MatIconModule],
  templateUrl: './docs-sidebar.html',
  styleUrl: './docs-sidebar.scss',
})
export class DocsSidebar {
  private readonly docsService = inject(DocsService);
  protected readonly groups = computed(() => this.docsService.docsByCategory());
  protected readonly loadError = computed(() => this.docsService.getLoadError());
  protected readonly hasGroups = computed(() => this.groups().length > 0);
}
