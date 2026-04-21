import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { SubscriptionService } from '../services/subscription.service';

export function requiresPlanGuard(...planNames: string[]): CanActivateFn {
  return () => {
    const subscriptionService = inject(SubscriptionService);
    const router = inject(Router);

    if (subscriptionService.hasAnyPlan(planNames)) {
      return true;
    }
    return router.createUrlTree(['/pricing']);
  };
}
