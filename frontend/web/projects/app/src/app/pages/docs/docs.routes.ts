import { inject } from '@angular/core';
import { Router, type CanActivateFn, type Routes } from '@angular/router';
import { DocsService } from '../../services/docs.service';
import { DocsLayout } from './docs-layout';
import { DocsViewer } from './docs-viewer';

const docsBootstrapGuard: CanActivateFn = async () => {
  const docs = inject(DocsService);
  const router = inject(Router);
  await docs.ensureLoaded();
  const first = docs.getFirstDoc();
  if (!first) {
    return router.parseUrl('/');
  }
  return router.parseUrl(`/docs/${first.category}/${first.slug}`);
};

const categoryBootstrapGuard: CanActivateFn = async (route) => {
  const docs = inject(DocsService);
  const router = inject(Router);
  const category = route.paramMap.get('category') ?? '';
  await docs.ensureLoaded();
  const first = docs.getFirstDocForCategory(category) ?? docs.getFirstDoc();
  if (!first) {
    return router.parseUrl('/');
  }
  if (first.category === category) {
    return router.parseUrl(`/docs/${first.category}/${first.slug}`);
  }
  return router.parseUrl(`/docs/${first.category}/${first.slug}`);
};

export const docsRoutes: Routes = [
  {
    path: '',
    component: DocsLayout,
    children: [
      {
        path: '',
        pathMatch: 'full',
        canActivate: [docsBootstrapGuard],
        loadComponent: () => import('./docs-viewer').then((m) => m.DocsViewer),
      },
      {
        path: ':category',
        canActivate: [categoryBootstrapGuard],
        loadComponent: () => import('./docs-viewer').then((m) => m.DocsViewer),
      },
      {
        path: ':category/:slug',
        loadComponent: () => import('./docs-viewer').then((m) => m.DocsViewer),
      },
    ],
  },
];

export { DocsLayout, DocsViewer };
