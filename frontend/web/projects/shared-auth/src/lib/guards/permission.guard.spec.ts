import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { permissionGuard } from './permission.guard';
import { PermissionService } from '../services/permission.service';

describe('permissionGuard', () => {
  const mockRoute = {} as ActivatedRouteSnapshot;
  const mockState = {} as RouterStateSnapshot;
  const mockUrlTree = {} as UrlTree;

  function setup(hasPermission: boolean) {
    const permissionService = { hasPermission: vi.fn().mockReturnValue(hasPermission) };
    const router = { createUrlTree: vi.fn().mockReturnValue(mockUrlTree) };

    TestBed.configureTestingModule({
      providers: [
        { provide: PermissionService, useValue: permissionService },
        { provide: Router, useValue: router },
      ],
    });

    return { router, permissionService };
  }

  it('should return true when user has the required permission', () => {
    setup(true);
    const guard = permissionGuard('Users.Read');
    const result = TestBed.runInInjectionContext(() => guard(mockRoute, mockState));
    expect(result).toBe(true);
  });

  it('should redirect to /admin when user lacks the required permission', () => {
    const { router } = setup(false);
    const guard = permissionGuard('Users.Read');
    const result = TestBed.runInInjectionContext(() => guard(mockRoute, mockState));
    expect(result).toEqual(mockUrlTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/admin']);
  });
});
