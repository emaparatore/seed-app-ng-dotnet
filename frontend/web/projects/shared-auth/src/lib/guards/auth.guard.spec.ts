import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';

describe('authGuard', () => {
  const mockRoute = {} as ActivatedRouteSnapshot;
  const mockState = {} as RouterStateSnapshot;
  const mockUrlTree = {} as UrlTree;

  function setup(isAuthenticated: boolean) {
    const authService = { isAuthenticated: () => isAuthenticated };
    const router = { createUrlTree: vi.fn().mockReturnValue(mockUrlTree) };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router },
      ],
    });

    return { router };
  }

  it('should return true when authenticated', () => {
    setup(true);
    const result = TestBed.runInInjectionContext(() => authGuard(mockRoute, mockState));
    expect(result).toBe(true);
  });

  it('should redirect to /login when not authenticated', () => {
    const { router } = setup(false);
    const result = TestBed.runInInjectionContext(() => authGuard(mockRoute, mockState));
    expect(result).toEqual(mockUrlTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
  });
});
