import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

@Component({
  selector: 'app-confirm-delete-dialog',
  imports: [
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
  ],
  template: `
    <h2 mat-dialog-title>Delete Account</h2>

    <mat-dialog-content>
      <p class="warning-text">
        <strong>This action is permanent and cannot be undone.</strong>
      </p>
      <p>
        All your data, including your profile information and account settings, will be permanently
        deleted. You will not be able to recover your account.
      </p>

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Password</mat-label>
        <input
          matInput
          type="password"
          [(ngModel)]="password"
          placeholder="Enter your password to confirm"
        />
      </mat-form-field>

      <mat-checkbox [(ngModel)]="confirmed">
        I understand that this action is permanent and all my data will be lost
      </mat-checkbox>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button
        mat-flat-button
        color="warn"
        [disabled]="!password() || !confirmed()"
        (click)="onConfirm()"
      >
        Delete My Account
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .warning-text {
      color: var(--mat-sys-error);
    }

    .full-width {
      width: 100%;
      margin-top: 1rem;
    }

    mat-checkbox {
      margin-top: 0.5rem;
    }

    mat-dialog-actions {
      display: flex;
      justify-content: flex-end;
      align-items: center;
      padding: 0.5rem 1.5rem 1rem;
      gap: 8px;
    }
  `,
})
export class ConfirmDeleteDialog {
  protected readonly password = signal('');
  protected readonly confirmed = signal(false);

  constructor(private readonly dialogRef: MatDialogRef<ConfirmDeleteDialog>) {}

  onConfirm(): void {
    this.dialogRef.close(this.password());
  }
}
