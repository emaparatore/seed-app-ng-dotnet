import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { Dashboard } from './dashboard';
import { AdminDashboardService } from './dashboard.service';
import { DashboardStats } from './models/dashboard.models';

const mockStats: DashboardStats = {
  totalUsers: 100,
  activeUsers: 85,
  inactiveUsers: 15,
  registrationsLast7Days: 12,
  registrationsLast30Days: 45,
  registrationTrend: [
    { date: '2026-03-22', count: 3 },
    { date: '2026-03-23', count: 5 },
  ],
  usersByRole: [
    { roleName: 'Admin', userCount: 5 },
    { roleName: 'User', userCount: 95 },
  ],
  recentActivity: [
    { id: '1', timestamp: '2026-03-23T10:00:00Z', action: 'UserCreated', entityType: 'User', userId: 'u1' },
    { id: '2', timestamp: '2026-03-23T09:00:00Z', action: 'RoleAssigned', entityType: 'Role', userId: 'u2' },
  ],
};

describe('Dashboard', () => {
  let component: Dashboard;
  let fixture: ComponentFixture<Dashboard>;
  let dashboardService: { getStats: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    dashboardService = {
      getStats: vi.fn().mockReturnValue(of(mockStats)),
    };

    await TestBed.configureTestingModule({
      imports: [Dashboard],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AdminDashboardService, useValue: dashboardService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Dashboard);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should call getStats on init and display data', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    expect(dashboardService.getStats).toHaveBeenCalled();
    expect(component['stats']()).toEqual(mockStats);
    expect(component['loading']()).toBe(false);
  });

  it('should show skeleton loading before data arrives', () => {
    dashboardService.getStats.mockReturnValue(of(mockStats));
    // Before detectChanges, loading is true by default
    expect(component['loading']()).toBe(true);

    const el: HTMLElement = fixture.nativeElement;
    fixture.detectChanges();
    // After data arrives loading is false, skeleton should not be visible
    const skeletons = el.querySelectorAll('.skeleton-card');
    expect(skeletons.length).toBe(0);
  });

  it('should show error message when API call fails', async () => {
    dashboardService.getStats.mockReturnValue(throwError(() => ({ error: { errors: ['Server error'] } })));

    fixture.detectChanges();
    await fixture.whenStable();

    expect(component['error']()).toBe('Server error');
    expect(component['loading']()).toBe(false);

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.error-container')).toBeTruthy();
  });

  it('should display correct stat values in cards', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const values = el.querySelectorAll('.stat-value');
    expect(values[0].textContent?.trim()).toBe('100');
    expect(values[1].textContent?.trim()).toBe('85');
    expect(values[2].textContent?.trim()).toBe('15');
    expect(values[3].textContent?.trim()).toBe('12');
    expect(values[4].textContent?.trim()).toBe('45');
  });

  it('should have a link to audit-log', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const link = el.querySelector('a[href="/admin/audit-log"]');
    expect(link).toBeTruthy();
    expect(link?.textContent?.trim()).toBe('Vedi tutto');
  });
});
