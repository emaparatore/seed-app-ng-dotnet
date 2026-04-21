import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { requiresPlanGuard } from './requires-plan.guard';
import { SubscriptionService } from '../services/subscription.service';

describe('requiresPlanGuard', () => {
  const mockRoute = {} as ActivatedRouteSnapshot;
  const mockState = {} as RouterStateSnapshot;
  const mockUrlTree = {} as UrlTree;

  function setup(hasAnyPlan: boolean) {
    const subscriptionService = { hasAnyPlan: vi.fn().mockReturnValue(hasAnyPlan) };
    const router = { createUrlTree: vi.fn().mockReturnValue(mockUrlTree) };

    TestBed.configureTestingModule({
      providers: [
        { provide: SubscriptionService, useValue: subscriptionService },
        { provide: Router, useValue: router },
      ],
    });

    return { router, subscriptionService };
  }

  it('should return true when user has the required plan', () => {
    setup(true);
    const guard = requiresPlanGuard('Pro');
    const result = TestBed.runInInjectionContext(() => guard(mockRoute, mockState));
    expect(result).toBe(true);
  });

  it('should redirect to /pricing when user lacks the required plan', () => {
    const { router } = setup(false);
    const guard = requiresPlanGuard('Pro');
    const result = TestBed.runInInjectionContext(() => guard(mockRoute, mockState));
    expect(result).toEqual(mockUrlTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/pricing']);
  });

  it('should return true when subscription is null (payments module disabled)', () => {
    setup(true);
    const guard = requiresPlanGuard('Pro');
    const result = TestBed.runInInjectionContext(() => guard(mockRoute, mockState));
    expect(result).toBe(true);
  });
});
