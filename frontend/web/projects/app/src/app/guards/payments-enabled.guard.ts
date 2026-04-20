import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ConfigService } from '../services/config.service';

export const paymentsEnabledGuard: CanActivateFn = () => {
  const configService = inject(ConfigService);
  const router = inject(Router);

  if (!configService.paymentsEnabled()) {
    return router.createUrlTree(['/']);
  }
  return true;
};
