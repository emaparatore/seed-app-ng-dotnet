import { Component, inject, OnInit, signal, DestroyRef } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { HasPermissionDirective, PERMISSIONS, PermissionService } from 'shared-auth';
import { AdminUsersService } from '../users.service';
import { AdminUser, AdminRole, GetUsersParams } from '../models/user.models';
import { ConfirmDialog } from '../confirm-dialog/confirm-dialog';
import { CreateUserDialog } from '../create-user-dialog/create-user-dialog';

@Component({
  selector: 'app-user-list',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    MatDatepickerModule,
    MatNativeDateModule,
    HasPermissionDirective,
  ],
  templateUrl: './user-list.html',
  styleUrl: './user-list.scss',
})
export class UserList implements OnInit {
  private readonly usersService = inject(AdminUsersService);
  private readonly permissionService = inject(PermissionService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly permissions = PERMISSIONS;
  protected readonly displayedColumns = ['name', 'email', 'roles', 'status', 'createdAt', 'actions'];

  protected readonly loading = signal(true);
  protected readonly users = signal<AdminUser[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly totalCount = signal(0);
  protected readonly roles = signal<AdminRole[]>([]);

  protected readonly searchControl = new FormControl('');
  protected readonly roleFilterControl = new FormControl('');
  protected readonly statusFilterControl = new FormControl<string>('');
  protected readonly dateFromControl = new FormControl<Date | null>(null);
  protected readonly dateToControl = new FormControl<Date | null>(null);

  protected pageIndex = 0;
  protected pageSize = 10;
  protected sortBy = '';
  protected sortDescending = false;

  ngOnInit(): void {
    this.loadUsers();
    this.loadRoles();

    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.pageIndex = 0;
        this.loadUsers();
      });
  }

  protected onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadUsers();
  }

  protected onSortChange(sort: Sort): void {
    this.sortBy = sort.active;
    this.sortDescending = sort.direction === 'desc';
    this.pageIndex = 0;
    this.loadUsers();
  }

  protected onFilterChange(): void {
    this.pageIndex = 0;
    this.loadUsers();
  }

  protected clearFilters(): void {
    this.searchControl.setValue('', { emitEvent: false });
    this.roleFilterControl.setValue('');
    this.statusFilterControl.setValue('');
    this.dateFromControl.setValue(null);
    this.dateToControl.setValue(null);
    this.pageIndex = 0;
    this.loadUsers();
  }

  protected getInitials(user: AdminUser): string {
    return `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase();
  }

  protected getFullName(user: AdminUser): string {
    return `${user.firstName} ${user.lastName}`;
  }

  protected openCreateDialog(): void {
    const dialogRef = this.dialog.open(CreateUserDialog, {
      width: '500px',
      data: { roles: this.roles() },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadUsers();
        this.snackBar.open('Utente creato con successo', 'Chiudi', { duration: 3000 });
      }
    });
  }

  protected navigateToDetail(user: AdminUser): void {
    this.router.navigate(['/admin/users', user.id]);
  }

  protected toggleStatus(user: AdminUser, event: Event): void {
    event.stopPropagation();
    const newStatus = !user.isActive;
    const action = newStatus ? 'attivare' : 'disattivare';

    const dialogRef = this.dialog.open(ConfirmDialog, {
      width: '400px',
      data: {
        title: `${newStatus ? 'Attiva' : 'Disattiva'} utente`,
        message: `Sei sicuro di voler ${action} l'utente ${user.firstName} ${user.lastName}?`,
        confirmText: newStatus ? 'Attiva' : 'Disattiva',
        cancelText: 'Annulla',
      },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.usersService.toggleUserStatus(user.id, newStatus).subscribe({
          next: () => {
            this.loadUsers();
            this.snackBar.open(`Utente ${newStatus ? 'attivato' : 'disattivato'} con successo`, 'Chiudi', {
              duration: 3000,
            });
          },
          error: (err) => {
            this.snackBar.open(err.error?.errors?.[0] ?? 'Errore durante l\'operazione', 'Chiudi', { duration: 5000 });
          },
        });
      }
    });
  }

  protected deleteUser(user: AdminUser, event: Event): void {
    event.stopPropagation();
    const dialogRef = this.dialog.open(ConfirmDialog, {
      width: '400px',
      data: {
        title: 'Elimina utente',
        message: `Sei sicuro di voler eliminare l'utente ${user.firstName} ${user.lastName}? Questa azione non può essere annullata.`,
        confirmText: 'Elimina',
        cancelText: 'Annulla',
      },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.usersService.deleteUser(user.id).subscribe({
          next: () => {
            this.loadUsers();
            this.snackBar.open('Utente eliminato con successo', 'Chiudi', { duration: 3000 });
          },
          error: (err) => {
            this.snackBar.open(err.error?.errors?.[0] ?? 'Errore durante l\'eliminazione', 'Chiudi', {
              duration: 5000,
            });
          },
        });
      }
    });
  }

  protected hasPermission(permission: string): boolean {
    return this.permissionService.hasPermission(permission);
  }

  private loadUsers(): void {
    this.loading.set(true);
    this.error.set(null);

    const statusValue = this.statusFilterControl.value;
    let statusFilter: boolean | undefined;
    if (statusValue === 'active') statusFilter = true;
    else if (statusValue === 'inactive') statusFilter = false;

    const params: GetUsersParams = {
      pageNumber: this.pageIndex + 1,
      pageSize: this.pageSize,
      searchTerm: this.searchControl.value || undefined,
      roleFilter: this.roleFilterControl.value || undefined,
      statusFilter,
      dateFrom: this.dateFromControl.value?.toISOString(),
      dateTo: this.dateToControl.value?.toISOString(),
      sortBy: this.sortBy || undefined,
      sortDescending: this.sortDescending || undefined,
    };

    this.usersService.getUsers(params).subscribe({
      next: (result) => {
        this.users.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore nel caricamento degli utenti.');
        this.loading.set(false);
      },
    });
  }

  private loadRoles(): void {
    this.usersService.getRoles().subscribe({
      next: (roles) => this.roles.set(roles),
    });
  }

  protected reload(): void {
    this.loadUsers();
  }
}
