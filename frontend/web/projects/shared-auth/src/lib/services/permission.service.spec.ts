import { TestBed } from '@angular/core/testing';
import { PermissionService } from './permission.service';
import { AuthService } from './auth.service';

describe('PermissionService', () => {
  function setup(permissions: string[]) {
    const authService = { permissions: () => permissions };

    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: authService }],
    });

    return TestBed.inject(PermissionService);
  }

  describe('hasPermission', () => {
    it('should return true when user has the permission', () => {
      const service = setup(['Users.Read', 'Roles.Read']);
      expect(service.hasPermission('Users.Read')).toBe(true);
    });

    it('should return false when user does not have the permission', () => {
      const service = setup(['Users.Read']);
      expect(service.hasPermission('Roles.Read')).toBe(false);
    });

    it('should return false when user has no permissions', () => {
      const service = setup([]);
      expect(service.hasPermission('Users.Read')).toBe(false);
    });
  });

  describe('hasAnyPermission', () => {
    it('should return true when user has at least one of the permissions', () => {
      const service = setup(['Users.Read']);
      expect(service.hasAnyPermission(['Users.Read', 'Roles.Read'])).toBe(true);
    });

    it('should return false when user has none of the permissions', () => {
      const service = setup(['Settings.Read']);
      expect(service.hasAnyPermission(['Users.Read', 'Roles.Read'])).toBe(false);
    });

    it('should return false for empty permissions array', () => {
      const service = setup(['Users.Read']);
      expect(service.hasAnyPermission([])).toBe(false);
    });
  });

  describe('isAdmin', () => {
    it('should return true when user has at least one permission', () => {
      const service = setup(['Dashboard.ViewStats']);
      expect(service.isAdmin()).toBe(true);
    });

    it('should return false when user has no permissions', () => {
      const service = setup([]);
      expect(service.isAdmin()).toBe(false);
    });
  });
});
