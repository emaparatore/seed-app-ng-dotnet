import { Component, inject, OnInit, signal, DestroyRef } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { PermissionService, PERMISSIONS } from 'shared-auth';
import { AuditLogService } from '../audit-log.service';
import { AuditLogEntry, AUDIT_ACTIONS, GetAuditLogParams } from '../audit-log.models';

@Component({
  selector: 'app-audit-log-list',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatDatepickerModule,
    MatNativeDateModule,
  ],
  templateUrl: './audit-log-list.html',
  styleUrl: './audit-log-list.scss',
  animations: [
    trigger('detailExpand', [
      state('collapsed,void', style({ height: '0px', minHeight: '0' })),
      state('expanded', style({ height: '*' })),
      transition('expanded <=> collapsed', animate('225ms cubic-bezier(0.4, 0.0, 0.2, 1)')),
    ]),
  ],
})
export class AuditLogList implements OnInit {
  private readonly auditLogService = inject(AuditLogService);
  private readonly permissionService = inject(PermissionService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly permissions = PERMISSIONS;
  protected readonly auditActions = AUDIT_ACTIONS;
  protected readonly displayedColumns = ['timestamp', 'action', 'entityType', 'entityId', 'userId', 'expand'];
  protected readonly displayedColumnsWithExpand = [...this.displayedColumns];

  protected readonly loading = signal(true);
  protected readonly entries = signal<AuditLogEntry[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly totalCount = signal(0);
  protected readonly expandedEntryId = signal<string | null>(null);

  protected readonly searchControl = new FormControl('');
  protected readonly actionFilterControl = new FormControl('');
  protected readonly dateFromControl = new FormControl<Date | null>(null);
  protected readonly dateToControl = new FormControl<Date | null>(null);

  protected pageIndex = 0;
  protected pageSize = 10;
  protected sortDescending = true;

  ngOnInit(): void {
    this.loadEntries();

    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.pageIndex = 0;
        this.loadEntries();
      });
  }

  protected onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadEntries();
  }

  protected onFilterChange(): void {
    this.pageIndex = 0;
    this.loadEntries();
  }

  protected clearFilters(): void {
    this.searchControl.setValue('', { emitEvent: false });
    this.actionFilterControl.setValue('');
    this.dateFromControl.setValue(null);
    this.dateToControl.setValue(null);
    this.pageIndex = 0;
    this.loadEntries();
  }

  protected toggleRow(entry: AuditLogEntry): void {
    this.expandedEntryId.set(this.expandedEntryId() === entry.id ? null : entry.id);
  }

  protected isExpanded(entry: AuditLogEntry): boolean {
    return this.expandedEntryId() === entry.id;
  }

  protected formatDetails(details: string | null): string {
    if (!details) return '';
    try {
      return JSON.stringify(JSON.parse(details), null, 2);
    } catch {
      return details;
    }
  }

  protected hasPermission(permission: string): boolean {
    return this.permissionService.hasPermission(permission);
  }

  protected exportCsv(): void {
    const params = this.buildParams();
    this.auditLogService.exportCsv(params).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `audit-log-${new Date().toISOString().slice(0, 10)}.csv`;
        a.click();
        URL.revokeObjectURL(url);
      },
    });
  }

  protected reload(): void {
    this.loadEntries();
  }

  loadEntries(): void {
    this.loading.set(true);
    this.error.set(null);

    const params = this.buildParams();

    this.auditLogService.getEntries(params).subscribe({
      next: (result) => {
        this.entries.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore nel caricamento dell\'audit log.');
        this.loading.set(false);
      },
    });
  }

  private buildParams(): GetAuditLogParams {
    return {
      pageNumber: this.pageIndex + 1,
      pageSize: this.pageSize,
      searchTerm: this.searchControl.value || undefined,
      actionFilter: this.actionFilterControl.value || undefined,
      dateFrom: this.dateFromControl.value?.toISOString(),
      dateTo: this.dateToControl.value?.toISOString(),
      sortDescending: this.sortDescending,
    };
  }
}
