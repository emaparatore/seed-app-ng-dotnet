import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { PermissionService } from 'shared-auth';
import { UserDetail } from './user-detail';
import { AdminUsersService } from '../users.service';
import { AdminUserDetail } from '../models/user.models';
import { ConfigService } from '../../../../services/config.service';

const mockUser: AdminUserDetail = {
  id: '1',
  email: 'mario@example.com',
  firstName: 'Mario',
  lastName: 'Rossi',
  isActive: true,
  roles: ['Admin'],
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-03-20T10:00:00Z',
  mustChangePassword: false,
  emailConfirmed: true,
  subscription: null,
};

describe('UserDetail', () => {
  let component: UserDetail;
  let fixture: ComponentFixture<UserDetail>;
  let usersService: {
    getUserById: ReturnType<typeof vi.fn>;
    getRoles: ReturnType<typeof vi.fn>;
    updateUser: ReturnType<typeof vi.fn>;
  };
  let permissionService: { hasPermission: ReturnType<typeof vi.fn>; permissions: ReturnType<typeof vi.fn> };
  let configService: { paymentsEnabled: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    usersService = {
      getUserById: vi.fn().mockReturnValue(of(mockUser)),
      getRoles: vi.fn().mockReturnValue(of([])),
      updateUser: vi.fn().mockReturnValue(of(undefined)),
    };

    permissionService = {
      hasPermission: vi.fn().mockReturnValue(true),
      permissions: vi.fn().mockReturnValue([
        'Users.Read',
        'Users.Update',
        'Users.Delete',
        'Users.ToggleStatus',
        'Users.AssignRoles',
      ]),
    };

    configService = {
      paymentsEnabled: vi.fn().mockReturnValue(true),
    };

    await TestBed.configureTestingModule({
      imports: [UserDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AdminUsersService, useValue: usersService },
        { provide: PermissionService, useValue: permissionService },
        { provide: ConfigService, useValue: configService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { params: { id: '1' } } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserDetail);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load and display user details', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    expect(usersService.getUserById).toHaveBeenCalledWith('1');
    expect(component['user']()).toEqual(mockUser);
    expect(component['loading']()).toBe(false);

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Mario');
    expect(el.textContent).toContain('Rossi');
  });

  it('should show edit form fields', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const inputs = el.querySelectorAll('input[matinput]');
    expect(inputs.length).toBeGreaterThanOrEqual(3);
  });

  it('should populate form with user data', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const form = component['form'];
    expect(form.controls.firstName.value).toBe('Mario');
    expect(form.controls.lastName.value).toBe('Rossi');
    expect(form.controls.email.value).toBe('mario@example.com');
  });

  it('should show action buttons based on permissions', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Forza cambio');
    expect(el.textContent).toContain('Reset password');
    expect(el.textContent).toContain('Disattiva');
    expect(el.textContent).toContain('Elimina');
  });

  it('should show error state on load failure', async () => {
    usersService.getUserById.mockReturnValue(throwError(() => ({ error: { errors: ['Not found'] } })));

    fixture.detectChanges();
    await fixture.whenStable();

    expect(component['error']()).toBe('Not found');
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.error-container')).toBeTruthy();
  });

  it('should show back button', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const backBtn = el.querySelector('.page-header button');
    expect(backBtn).toBeTruthy();
  });

  it('should show subscription info when payments are enabled', async () => {
    usersService.getUserById.mockReturnValue(
      of({
        ...mockUser,
        subscription: {
          currentPlan: 'Pro',
          subscriptionStatus: 'Active',
          trialEndsAt: '2026-04-30T10:00:00Z',
        },
      }),
    );

    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Piano attivo');
    expect(el.textContent).toContain('Pro');
    expect(el.textContent).toContain('Attivo');
  });

  it('should hide subscription info when payments are disabled', async () => {
    configService.paymentsEnabled.mockReturnValue(false);

    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).not.toContain('Piano attivo');
  });
});
