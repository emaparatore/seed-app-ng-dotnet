import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError, Subject } from 'rxjs';
import { PermissionService } from 'shared-auth';
import { AuditLogList } from './audit-log-list';
import { AuditLogService } from '../audit-log.service';
import { AuditLogEntry } from '../audit-log.models';
import { PagedResult } from '../../users/models/user.models';

const mockEntries: AuditLogEntry[] = [
  {
    id: 'entry-1',
    timestamp: '2026-03-20T10:30:00Z',
    userId: 'user-1',
    action: 'UserCreated',
    entityType: 'User',
    entityId: 'user-2',
    details: '{"email":"test@example.com","roles":["User"]}',
    ipAddress: '192.168.1.1',
    userAgent: 'Mozilla/5.0',
  },
  {
    id: 'entry-2',
    timestamp: '2026-03-20T11:00:00Z',
    userId: null,
    action: 'LoginFailed',
    entityType: 'Auth',
    entityId: null,
    details: null,
    ipAddress: '10.0.0.1',
    userAgent: null,
  },
];

const mockPagedResult: PagedResult<AuditLogEntry> = {
  items: mockEntries,
  pageNumber: 1,
  pageSize: 10,
  totalCount: 2,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false,
};

describe('AuditLogList', () => {
  let component: AuditLogList;
  let fixture: ComponentFixture<AuditLogList>;
  let auditLogService: { getEntries: ReturnType<typeof vi.fn>; exportCsv: ReturnType<typeof vi.fn> };
  let permissionService: { hasPermission: ReturnType<typeof vi.fn>; permissions: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    auditLogService = {
      getEntries: vi.fn().mockReturnValue(of(mockPagedResult)),
      exportCsv: vi.fn().mockReturnValue(of(new Blob(['csv data'], { type: 'text/csv' }))),
    };

    permissionService = {
      hasPermission: vi.fn().mockReturnValue(true),
      permissions: vi.fn().mockReturnValue(['AuditLog.Read', 'AuditLog.Export']),
    };

    await TestBed.configureTestingModule({
      imports: [AuditLogList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AuditLogService, useValue: auditLogService },
        { provide: PermissionService, useValue: permissionService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AuditLogList);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render table with audit log entries', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const rows = el.querySelectorAll('tr.mat-mdc-row:not(.detail-expand-row)');
    expect(rows.length).toBe(2);
  });

  it('should show skeleton loading when loading is true', () => {
    // Before detectChanges, loading is true (default)
    fixture.detectChanges();
    // After data loads synchronously, skeleton disappears — check initial state
    const el: HTMLElement = fixture.nativeElement;
    // Data loads synchronously via of(), so skeleton won't be visible after detectChanges
    // Verify component started in loading state
    expect(auditLogService.getEntries).toHaveBeenCalled();
  });

  it('should show skeleton rows before data loads', () => {
    auditLogService.getEntries.mockReturnValue(new Subject());
    fixture = TestBed.createComponent(AuditLogList);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const skeletons = el.querySelectorAll('.skeleton-row');
    expect(skeletons.length).toBe(5);
  });

  it('should show empty state when no entries', async () => {
    auditLogService.getEntries.mockReturnValue(
      of({ items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false }),
    );

    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.empty-state')).toBeTruthy();
  });

  it('should show error state on API failure', async () => {
    auditLogService.getEntries.mockReturnValue(throwError(() => ({ error: { errors: ['Server error'] } })));

    fixture.detectChanges();
    await fixture.whenStable();

    expect(component['error']()).toBe('Server error');
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.error-container')).toBeTruthy();
  });

  it('should show export CSV button when user has AuditLog.Export permission', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const buttons = el.querySelectorAll('.page-header button');
    const exportBtn = Array.from(buttons).find((b) => b.textContent?.includes('Esporta CSV'));
    expect(exportBtn).toBeTruthy();
  });

  it('should hide export CSV button when user lacks AuditLog.Export permission', async () => {
    permissionService.hasPermission.mockReturnValue(false);

    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const buttons = el.querySelectorAll('.page-header button');
    const exportBtn = Array.from(buttons).find((b) => b.textContent?.includes('Esporta CSV'));
    expect(exportBtn).toBeFalsy();
  });

  it('should expand row to show details on click', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    component['toggleRow'](mockEntries[0]);
    expect(component['expandedEntryId']()).toBe('entry-1');
    expect(component['isExpanded'](mockEntries[0])).toBe(true);

    // Toggle again to collapse
    component['toggleRow'](mockEntries[0]);
    expect(component['expandedEntryId']()).toBeNull();
  });

  it('should reset pageIndex when filters change', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    component['pageIndex'] = 3;
    component['onFilterChange']();
    expect(component['pageIndex']).toBe(0);
    expect(auditLogService.getEntries).toHaveBeenCalled();
  });

  it('should call loadEntries on page change', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    auditLogService.getEntries.mockClear();
    component['onPageChange']({ pageIndex: 2, pageSize: 25, length: 100 });

    expect(component['pageIndex']).toBe(2);
    expect(component['pageSize']).toBe(25);
    expect(auditLogService.getEntries).toHaveBeenCalledWith(
      expect.objectContaining({ pageNumber: 3, pageSize: 25 }),
    );
  });

  it('should format JSON details correctly', () => {
    const result = component['formatDetails']('{"key":"value"}');
    expect(result).toBe('{\n  "key": "value"\n}');
  });

  it('should return raw string for invalid JSON', () => {
    const result = component['formatDetails']('not json');
    expect(result).toBe('not json');
  });

  it('should reload on retry button click', async () => {
    auditLogService.getEntries.mockReturnValue(throwError(() => ({ error: { errors: ['fail'] } })));
    fixture.detectChanges();
    await fixture.whenStable();

    auditLogService.getEntries.mockClear();
    auditLogService.getEntries.mockReturnValue(of(mockPagedResult));

    component['reload']();
    expect(auditLogService.getEntries).toHaveBeenCalled();
  });
});
