import { Routes } from '@angular/router';
import { authGuard, guestGuard, mustChangePasswordGuard, adminGuard } from 'shared-auth';

export const routes: Routes = [
  {
    path: 'admin',
    loadComponent: () => import('./pages/admin/admin-layout').then((m) => m.AdminLayout),
    canActivate: [authGuard, adminGuard],
    loadChildren: () => import('./pages/admin/admin.routes').then((m) => m.adminRoutes),
  },
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login').then((m) => m.Login),
    canActivate: [guestGuard],
  },
  {
    path: 'register',
    loadComponent: () => import('./pages/register/register').then((m) => m.Register),
    canActivate: [guestGuard],
  },
  {
    path: 'forgot-password',
    loadComponent: () => import('./pages/forgot-password/forgot-password').then((m) => m.ForgotPassword),
    canActivate: [guestGuard],
  },
  {
    path: 'reset-password',
    loadComponent: () => import('./pages/reset-password/reset-password').then((m) => m.ResetPassword),
    canActivate: [guestGuard],
  },
  {
    path: 'confirm-email',
    loadComponent: () => import('./pages/confirm-email/confirm-email').then((m) => m.ConfirmEmail),
    canActivate: [guestGuard],
  },
  {
    path: 'change-password',
    loadComponent: () => import('./pages/change-password/change-password').then((m) => m.ChangePassword),
    canActivate: [authGuard, mustChangePasswordGuard],
  },
  {
    path: '',
    loadComponent: () => import('./pages/home/home').then((m) => m.Home),
    pathMatch: 'full',
    canActivate: [mustChangePasswordGuard],
  },
  {
    path: 'profile',
    loadComponent: () => import('./pages/profile/profile').then((m) => m.Profile),
    canActivate: [authGuard, mustChangePasswordGuard],
  },
  {
    path: 'privacy-policy',
    loadComponent: () => import('./pages/privacy-policy/privacy-policy').then((m) => m.PrivacyPolicy),
  },
  {
    path: 'terms-of-service',
    loadComponent: () => import('./pages/terms-of-service/terms-of-service').then((m) => m.TermsOfService),
  },
  {
    path: 'pricing',
    loadComponent: () => import('./pages/pricing/pricing').then((m) => m.Pricing),
  },
  { path: '**', redirectTo: '' },
];
