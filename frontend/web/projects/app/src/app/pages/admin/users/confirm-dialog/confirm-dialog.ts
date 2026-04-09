import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p>{{ data.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close(false)">{{ data.cancelText ?? 'Annulla' }}</button>
      <button mat-flat-button color="warn" (click)="dialogRef.close(true)">{{ data.confirmText ?? 'Conferma' }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    p {
      margin: 0;
      color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
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
export class ConfirmDialog {
  readonly dialogRef = inject(MatDialogRef<ConfirmDialog>);
  readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}
