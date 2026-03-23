import { Routes } from '@angular/router';
import { permissionGuard } from 'shared-auth';

export const adminRoutes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'dashboard',
    loadComponent: () => import('./placeholder').then((m) => m.AdminPlaceholder),
    canActivate: [permissionGuard('Dashboard.ViewStats')],
    data: { title: 'Dashboard' },
  },
  {
    path: 'users',
    loadComponent: () => import('./placeholder').then((m) => m.AdminPlaceholder),
    canActivate: [permissionGuard('Users.Read')],
    data: { title: 'Utenti' },
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
