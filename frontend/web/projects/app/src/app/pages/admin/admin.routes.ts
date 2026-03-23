import { Routes } from '@angular/router';
import { permissionGuard } from 'shared-auth';

export const adminRoutes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'dashboard',
    loadComponent: () => import('./dashboard/dashboard').then((m) => m.Dashboard),
    canActivate: [permissionGuard('Dashboard.ViewStats')],
    data: { title: 'Dashboard' },
  },
  {
    path: 'users',
    canActivate: [permissionGuard('Users.Read')],
    children: [
      {
        path: '',
        loadComponent: () => import('./users/user-list/user-list').then((m) => m.UserList),
        data: { title: 'Utenti' },
      },
      {
        path: ':id',
        loadComponent: () => import('./users/user-detail/user-detail').then((m) => m.UserDetail),
        data: { title: 'Dettaglio utente' },
      },
    ],
  },
  {
    path: 'roles',
    loadComponent: () => import('./placeholder').then((m) => m.AdminPlaceholder),
    canActivate: [permissionGuard('Roles.Read')],
    data: { title: 'Ruoli' },
  },
  {
    path: 'audit-log',
    loadComponent: () => import('./placeholder').then((m) => m.AdminPlaceholder),
    canActivate: [permissionGuard('AuditLog.Read')],
    data: { title: 'Audit Log' },
  },
  {
    path: 'settings',
    loadComponent: () => import('./placeholder').then((m) => m.AdminPlaceholder),
    canActivate: [permissionGuard('Settings.Read')],
    data: { title: 'Impostazioni' },
  },
  {
    path: 'system-health',
    loadComponent: () => import('./placeholder').then((m) => m.AdminPlaceholder),
    canActivate: [permissionGuard('SystemHealth.Read')],
    data: { title: 'Stato Sistema' },
  },
];
