import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const mustChangePasswordGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const isChangePasswordRoute = route.routeConfig?.path === 'change-password';

  if (authService.mustChangePassword()) {
    if (isChangePasswordRoute) {
      return true;
    }
    return router.createUrlTree(['/change-password']);
  }

  if (isChangePasswordRoute) {
    return router.createUrlTree(['/']);
  }

  return true;
};
