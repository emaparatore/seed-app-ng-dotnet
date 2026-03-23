import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-admin-placeholder',
  template: `
    <h1>{{ title }}</h1>
    <p>Questa sezione sarà disponibile a breve.</p>
  `,
  styles: `
    :host {
      display: block;
    }
    h1 {
      margin: 0 0 16px;
    }
    p {
      color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
    }
  `,
})
export class AdminPlaceholder {
  protected readonly title: string;

  constructor() {
    const route = inject(ActivatedRoute);
    this.title = route.snapshot.data['title'] ?? 'Admin';
  }
}
