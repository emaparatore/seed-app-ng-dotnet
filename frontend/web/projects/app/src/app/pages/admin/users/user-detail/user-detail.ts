import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { HasPermissionDirective, PERMISSIONS, PermissionService } from 'shared-auth';
import { AdminUsersService } from '../users.service';
import { AdminRole, AdminUserDetail } from '../models/user.models';
import { ConfirmDialog } from '../confirm-dialog/confirm-dialog';

@Component({
  selector: 'app-user-detail',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatChipsModule,
    MatDividerModule,
    HasPermissionDirective,
  ],
  templateUrl: './user-detail.html',
  styleUrl: './user-detail.scss',
})
export class UserDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly usersService = inject(AdminUsersService);
  private readonly permissionService = inject(PermissionService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  protected readonly permissions = PERMISSIONS;
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly user = signal<AdminUserDetail | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly roles = signal<AdminRole[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
  });

  protected readonly rolesControl = this.fb.control<string[]>([]);

  private userId = '';

  ngOnInit(): void {
    this.userId = this.route.snapshot.params['id'];
    this.loadUser();
    this.loadRoles();
  }

  protected hasPermission(permission: string): boolean {
    return this.permissionService.hasPermission(permission);
  }

  protected getInitials(): string {
    const u = this.user();
    if (!u) return '';
    return `${u.firstName.charAt(0)}${u.lastName.charAt(0)}`.toUpperCase();
  }

  protected saveChanges(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.usersService.updateUser(this.userId, this.form.getRawValue()).subscribe({
      next: () => {
        this.saving.set(false);
        this.snackBar.open('Utente aggiornato con successo', 'Chiudi', { duration: 3000 });
        this.loadUser();
      },
      error: (err) => {
        this.saving.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Errore durante il salvataggio', 'Chiudi', { duration: 5000 });
      },
    });
  }

  protected saveRoles(): void {
    this.usersService.assignRoles(this.userId, this.rolesControl.value ?? []).subscribe({
      next: () => {
        this.snackBar.open('Ruoli aggiornati con successo', 'Chiudi', { duration: 3000 });
        this.loadUser();
      },
      error: (err) => {
        this.snackBar.open(err.error?.errors?.[0] ?? 'Errore durante l\'aggiornamento dei ruoli', 'Chiudi', {
          duration: 5000,
        });
      },
    });
  }

  protected forcePasswordChange(): void {
    const dialogRef = this.dialog.open(ConfirmDialog, {
      width: '400px',
      data: {
        title: 'Forza cambio password',
        message: 'L\'utente sarà obbligato a cambiare la password al prossimo accesso. Continuare?',
        confirmText: 'Conferma',
        cancelText: 'Annulla',
      },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.usersService.forcePasswordChange(this.userId).subscribe({
          next: () => {
            this.snackBar.open('Cambio password forzato con successo', 'Chiudi', { duration: 3000 });
            this.loadUser();
          },
          error: (err) => {
            this.snackBar.open(err.error?.errors?.[0] ?? 'Errore durante l\'operazione', 'Chiudi', { duration: 5000 });
          },
        });
      }
    });
  }

  protected resetPassword(): void {
    const dialogRef = this.dialog.open(ConfirmDialog, {
      width: '400px',
      data: {
        title: 'Reset password',
        message: 'Verrà inviata un\'email all\'utente per il reset della password. Continuare?',
        confirmText: 'Conferma',
        cancelText: 'Annulla',
      },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.usersService.resetPassword(this.userId).subscribe({
          next: () => {
            this.snackBar.open('Email di reset password inviata', 'Chiudi', { duration: 3000 });
          },
          error: (err) => {
            this.snackBar.open(err.error?.errors?.[0] ?? 'Errore durante il reset', 'Chiudi', { duration: 5000 });
          },
        });
      }
    });
  }

  protected toggleStatus(): void {
    const u = this.user();
    if (!u) return;

    const newStatus = !u.isActive;
    const action = newStatus ? 'attivare' : 'disattivare';

    const dialogRef = this.dialog.open(ConfirmDialog, {
      width: '400px',
      data: {
        title: `${newStatus ? 'Attiva' : 'Disattiva'} utente`,
        message: `Sei sicuro di voler ${action} questo utente?`,
        confirmText: newStatus ? 'Attiva' : 'Disattiva',
        cancelText: 'Annulla',
      },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.usersService.toggleUserStatus(this.userId, newStatus).subscribe({
          next: () => {
            this.snackBar.open(`Utente ${newStatus ? 'attivato' : 'disattivato'} con successo`, 'Chiudi', {
              duration: 3000,
            });
            this.loadUser();
          },
          error: (err) => {
            this.snackBar.open(err.error?.errors?.[0] ?? 'Errore durante l\'operazione', 'Chiudi', { duration: 5000 });
          },
        });
      }
    });
  }

  protected deleteUser(): void {
    const u = this.user();
    if (!u) return;

    const dialogRef = this.dialog.open(ConfirmDialog, {
      width: '400px',
      data: {
        title: 'Elimina utente',
        message: `Sei sicuro di voler eliminare l'utente ${u.firstName} ${u.lastName}? Questa azione non può essere annullata.`,
        confirmText: 'Elimina',
        cancelText: 'Annulla',
      },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.usersService.deleteUser(this.userId).subscribe({
          next: () => {
            this.snackBar.open('Utente eliminato con successo', 'Chiudi', { duration: 3000 });
            this.router.navigate(['/admin/users']);
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

  protected goBack(): void {
    this.router.navigate(['/admin/users']);
  }

  protected reload(): void {
    this.loadUser();
  }

  private loadUser(): void {
    this.loading.set(true);
    this.error.set(null);
    this.usersService.getUserById(this.userId).subscribe({
      next: (user) => {
        this.user.set(user);
        this.form.patchValue({
          firstName: user.firstName,
          lastName: user.lastName,
          email: user.email,
        });
        this.rolesControl.setValue(user.roles as string[]);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore nel caricamento dell\'utente.');
        this.loading.set(false);
      },
    });
  }

  private loadRoles(): void {
    this.usersService.getRoles().subscribe({
      next: (roles) => this.roles.set(roles),
    });
  }
}
