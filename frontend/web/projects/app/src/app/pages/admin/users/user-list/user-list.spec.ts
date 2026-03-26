import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { PermissionService } from 'shared-auth';
import { UserList } from './user-list';
import { AdminUsersService } from '../users.service';
import { AdminUser, PagedResult } from '../models/user.models';

const mockUsers: AdminUser[] = [
  {
    id: '1',
    email: 'mario@example.com',
    firstName: 'Mario',
    lastName: 'Rossi',
    isActive: true,
    roles: ['Admin'],
    createdAt: '2026-01-01T00:00:00Z',
  },
  {
    id: '2',
    email: 'luigi@example.com',
    firstName: 'Luigi',
    lastName: 'Verdi',
    isActive: false,
    roles: ['User'],
    createdAt: '2026-02-15T00:00:00Z',
  },
];

const mockPagedResult: PagedResult<AdminUser> = {
  items: mockUsers,
  pageNumber: 1,
  pageSize: 10,
  totalCount: 2,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false,
};

describe('UserList', () => {
  let component: UserList;
  let fixture: ComponentFixture<UserList>;
  let usersService: { getUsers: ReturnType<typeof vi.fn>; getRoles: ReturnType<typeof vi.fn> };
  let permissionService: { hasPermission: ReturnType<typeof vi.fn>; permissions: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    usersService = {
      getUsers: vi.fn().mockReturnValue(of(mockPagedResult)),
      getRoles: vi.fn().mockReturnValue(of([])),
    };

    permissionService = {
      hasPermission: vi.fn().mockReturnValue(true),
      permissions: vi.fn().mockReturnValue(['Users.Read', 'Users.Create', 'Users.Delete', 'Users.ToggleStatus']),
    };

    await TestBed.configureTestingModule({
      imports: [UserList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AdminUsersService, useValue: usersService },
        { provide: PermissionService, useValue: permissionService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserList);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render table with users data', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const rows = el.querySelectorAll('tr.mat-mdc-row');
    expect(rows.length).toBe(2);
  });

  it('should show loading skeleton initially', () => {
    expect(component['loading']()).toBe(true);

    const el: HTMLElement = fixture.nativeElement;
    fixture.detectChanges();
    // After data loads, skeleton should be gone
    const skeletons = el.querySelectorAll('.skeleton-row');
    expect(skeletons.length).toBe(0);
  });

  it('should call service with pagination params on page change', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    usersService.getUsers.mockClear();
    component['onPageChange']({ pageIndex: 1, pageSize: 25, length: 50 });

    expect(usersService.getUsers).toHaveBeenCalledWith(
      expect.objectContaining({ pageNumber: 2, pageSize: 25 }),
    );
  });

  it('should call service with search term on search input', async () => {
    vi.useFakeTimers();
    fixture.detectChanges();
    await fixture.whenStable();

    usersService.getUsers.mockClear();
    component['searchControl'].setValue('mario');

    vi.advanceTimersByTime(300);

    expect(usersService.getUsers).toHaveBeenCalledWith(
      expect.objectContaining({ searchTerm: 'mario' }),
    );

    vi.useRealTimers();
  });

  it('should show create button only with Users.Create permission', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const buttons = el.querySelectorAll('.page-header button');
    const createBtn = Array.from(buttons).find((b) => b.textContent?.includes('Nuovo utente'));
    expect(createBtn).toBeTruthy();
  });

  it('should show delete button only with Users.Delete permission', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const deleteButtons = el.querySelectorAll('button[mattooltip="Elimina"]');
    expect(deleteButtons.length).toBe(2);
  });

  it('should show empty state when no results', async () => {
    usersService.getUsers.mockReturnValue(
      of({ items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false }),
    );

    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.empty-state')).toBeTruthy();
  });

  it('should show error state on API failure', async () => {
    usersService.getUsers.mockReturnValue(throwError(() => ({ error: { errors: ['Server error'] } })));

    fixture.detectChanges();
    await fixture.whenStable();

    expect(component['error']()).toBe('Server error');
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.error-container')).toBeTruthy();
  });
});
