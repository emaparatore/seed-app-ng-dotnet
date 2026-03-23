import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { AdminUsersService } from '../users.service';
import { AdminRole } from '../models/user.models';

export interface CreateUserDialogData {
  roles: AdminRole[];
}

@Component({
  selector: 'app-create-user-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatCheckboxModule,
  ],
  templateUrl: './create-user-dialog.html',
  styles: `
    .dialog-form {
      display: flex;
      flex-direction: column;
      gap: 8px;
      min-width: 400px;
    }

    .name-row {
      display: flex;
      gap: 12px;

      mat-form-field {
        flex: 1;
      }
    }

    .password-row {
      display: flex;
      align-items: center;
      gap: 8px;

      mat-form-field {
        flex: 1;
      }
    }

    .error-message {
      color: var(--mat-sys-error, #b3261e);
      font-size: 13px;
      margin: 0;
    }
  `,
})
export class CreateUserDialog {
  private readonly fb = inject(FormBuilder);
  private readonly usersService = inject(AdminUsersService);
  private readonly dialogRef = inject(MatDialogRef<CreateUserDialog>);
  readonly data = inject<CreateUserDialogData>(MAT_DIALOG_DATA);

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    roleNames: [[] as string[]],
  });

  protected togglePasswordVisibility(): void {
    this.showPassword.update((v) => !v);
  }

  protected generatePassword(): void {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%&*';
    let password = '';
    for (let i = 0; i < 16; i++) {
      password += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    this.form.controls.password.setValue(password);
    this.showPassword.set(true);
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    this.usersService.createUser(this.form.getRawValue()).subscribe({
      next: (result) => {
        this.saving.set(false);
        this.dialogRef.close(result);
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(err.error?.errors?.[0] ?? 'Errore durante la creazione dell\'utente.');
      },
    });
  }

  protected onCancel(): void {
    this.dialogRef.close(null);
  }
}
