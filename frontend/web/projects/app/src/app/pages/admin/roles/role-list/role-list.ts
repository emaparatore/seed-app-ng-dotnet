import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { HasPermissionDirective, PERMISSIONS, PermissionService } from 'shared-auth';
import { AdminRolesService } from '../roles.service';
import { AdminRole } from '../models/role.models';
import { ConfirmDialog } from '../../users/confirm-dialog/confirm-dialog';
import { CreateRoleDialog } from '../create-role-dialog/create-role-dialog';

@Component({
  selector: 'app-role-list',
  imports: [
    DatePipe,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    HasPermissionDirective,
  ],
  templateUrl: './role-list.html',
  styleUrl: './role-list.scss',
})
export class RoleList implements OnInit {
  private readonly rolesService = inject(AdminRolesService);
  private readonly permissionService = inject(PermissionService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  protected readonly permissions = PERMISSIONS;
  protected readonly displayedColumns = ['name', 'description', 'userCount', 'system', 'createdAt', 'actions'];

  protected readonly loading = signal(true);
  protected readonly roles = signal<AdminRole[]>([]);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadRoles();
  }

  protected navigateToDetail(role: AdminRole): void {
    this.router.navigate(['/admin/roles', role.id]);
  }

  protected openCreateDialog(): void {
    const dialogRef = this.dialog.open(CreateRoleDialog, {
      width: '600px',
      data: { existingRoles: this.roles() },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadRoles();
        this.snackBar.open('Ruolo creato con successo', 'Chiudi', { duration: 3000 });
      }
    });
  }

  protected deleteRole(role: AdminRole, event: Event): void {
    event.stopPropagation();

    if (role.isSystemRole) return;

    let message = `Sei sicuro di voler eliminare il ruolo "${role.name}"? Questa azione non può essere annullata.`;
    if (role.userCount > 0) {
      message = `Il ruolo "${role.name}" è assegnato a ${role.userCount} utent${role.userCount === 1 ? 'e' : 'i'}. Non è possibile eliminarlo.`;
    }

    if (role.userCount > 0) {
      this.snackBar.open(message, 'Chiudi', { duration: 5000 });
      return;
    }

    const dialogRef = this.dialog.open(ConfirmDialog, {
      width: '400px',
      data: {
        title: 'Elimina ruolo',
        message,
        confirmText: 'Elimina',
        cancelText: 'Annulla',
      },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.rolesService.deleteRole(role.id).subscribe({
          next: () => {
            this.loadRoles();
            this.snackBar.open('Ruolo eliminato con successo', 'Chiudi', { duration: 3000 });
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

  protected reload(): void {
    this.loadRoles();
  }

  private loadRoles(): void {
    this.loading.set(true);
    this.error.set(null);

    this.rolesService.getRoles().subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore nel caricamento dei ruoli.');
        this.loading.set(false);
      },
    });
  }
}
