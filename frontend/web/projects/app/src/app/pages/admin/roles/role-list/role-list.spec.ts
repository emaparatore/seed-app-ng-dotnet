import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { PermissionService } from 'shared-auth';
import { RoleList } from './role-list';
import { AdminRolesService } from '../roles.service';
import { AdminRole } from '../models/role.models';

const mockRoles: AdminRole[] = [
  {
    id: '1',
    name: 'SuperAdmin',
    description: 'Accesso completo al sistema',
    isSystemRole: true,
    userCount: 1,
    createdAt: '2026-01-01T00:00:00Z',
  },
  {
    id: '2',
    name: 'Editor',
    description: 'Può modificare contenuti',
    isSystemRole: false,
    userCount: 5,
    createdAt: '2026-02-15T00:00:00Z',
  },
  {
    id: '3',
    name: 'Viewer',
    description: null,
    isSystemRole: false,
    userCount: 0,
    createdAt: '2026-03-01T00:00:00Z',
  },
];

describe('RoleList', () => {
  let component: RoleList;
  let fixture: ComponentFixture<RoleList>;
  let rolesService: { getRoles: ReturnType<typeof vi.fn>; getPermissions: ReturnType<typeof vi.fn> };
  let permissionService: { hasPermission: ReturnType<typeof vi.fn>; permissions: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    rolesService = {
      getRoles: vi.fn().mockReturnValue(of(mockRoles)),
      getPermissions: vi.fn().mockReturnValue(of([])),
    };

    permissionService = {
      hasPermission: vi.fn().mockReturnValue(true),
      permissions: vi.fn().mockReturnValue(['Roles.Read', 'Roles.Create', 'Roles.Update', 'Roles.Delete']),
    };

    await TestBed.configureTestingModule({
      imports: [RoleList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AdminRolesService, useValue: rolesService },
        { provide: PermissionService, useValue: permissionService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RoleList);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render table with roles data', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const rows = el.querySelectorAll('tr.mat-mdc-row');
    expect(rows.length).toBe(3);
  });

  it('should show system badge for system roles', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const badges = el.querySelectorAll('.system-badge');
    expect(badges.length).toBe(1);
    expect(badges[0].textContent?.trim()).toBe('Sistema');
  });

  it('should disable delete button for system roles', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const deleteButtons = el.querySelectorAll('button[mattooltip="Elimina"]');
    expect(deleteButtons.length).toBe(3);
    expect((deleteButtons[0] as HTMLButtonElement).disabled).toBe(true);
    expect((deleteButtons[1] as HTMLButtonElement).disabled).toBe(false);
    expect((deleteButtons[2] as HTMLButtonElement).disabled).toBe(false);
  });

  it('should show create button with Roles.Create permission', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const buttons = el.querySelectorAll('.page-header button');
    const createBtn = Array.from(buttons).find((b) => b.textContent?.includes('Nuovo ruolo'));
    expect(createBtn).toBeTruthy();
  });

  it('should show empty state when no roles', async () => {
    rolesService.getRoles.mockReturnValue(of([]));

    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.empty-state')).toBeTruthy();
  });

  it('should show error state on API failure', async () => {
    rolesService.getRoles.mockReturnValue(throwError(() => ({ error: { errors: ['Server error'] } })));

    fixture.detectChanges();
    await fixture.whenStable();

    expect(component['error']()).toBe('Server error');
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.error-container')).toBeTruthy();
  });

  it('should show loading skeleton initially', () => {
    expect(component['loading']()).toBe(true);

    const el: HTMLElement = fixture.nativeElement;
    fixture.detectChanges();
    const skeletons = el.querySelectorAll('.skeleton-row');
    expect(skeletons.length).toBe(0);
  });

  it('should display role names in the table', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const roleNames = el.querySelectorAll('.role-name');
    expect(roleNames.length).toBe(3);
    expect(roleNames[0].textContent?.trim()).toBe('SuperAdmin');
    expect(roleNames[1].textContent?.trim()).toBe('Editor');
    expect(roleNames[2].textContent?.trim()).toBe('Viewer');
  });
});
