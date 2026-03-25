import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { mustChangePasswordGuard } from './must-change-password.guard';
import { AuthService } from '../services/auth.service';

describe('mustChangePasswordGuard', () => {
  const mockState = {} as RouterStateSnapshot;
  const mockUrlTree = {} as UrlTree;

  function setup(mustChangePassword: boolean, routePath?: string) {
    const authService = { mustChangePassword: () => mustChangePassword };
    const router = { createUrlTree: vi.fn().mockReturnValue(mockUrlTree) };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router },
      ],
    });

    const mockRoute = { routeConfig: { path: routePath } } as unknown as ActivatedRouteSnapshot;

    return { router, mockRoute };
  }

  it('should return true when mustChangePassword is false and route is not change-password', () => {
    const { mockRoute } = setup(false, 'profile');
    const result = TestBed.runInInjectionContext(() => mustChangePasswordGuard(mockRoute, mockState));
    expect(result).toBe(true);
  });

  it('should redirect to /change-password when mustChangePassword is true and route is not change-password', () => {
    const { router, mockRoute } = setup(true, 'profile');
    const result = TestBed.runInInjectionContext(() => mustChangePasswordGuard(mockRoute, mockState));
    expect(result).toEqual(mockUrlTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/change-password']);
  });

  it('should return true when mustChangePassword is true and route is change-password', () => {
    const { mockRoute } = setup(true, 'change-password');
    const result = TestBed.runInInjectionContext(() => mustChangePasswordGuard(mockRoute, mockState));
    expect(result).toBe(true);
  });

  it('should redirect to / when mustChangePassword is false and route is change-password', () => {
    const { router, mockRoute } = setup(false, 'change-password');
    const result = TestBed.runInInjectionContext(() => mustChangePasswordGuard(mockRoute, mockState));
    expect(result).toEqual(mockUrlTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/']);
  });
});
