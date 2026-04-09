import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-consent-update-dialog',
  imports: [MatDialogModule, MatButtonModule, RouterLink],
  template: `
    <h2 mat-dialog-title>Privacy Policy Updated</h2>

    <mat-dialog-content>
      <p>
        Our Privacy Policy and Terms of Service have been updated. Please review the changes and
        accept the updated terms to continue using the application.
      </p>
      <p>
        <a routerLink="/privacy-policy" target="_blank">Privacy Policy</a> |
        <a routerLink="/terms-of-service" target="_blank">Terms of Service</a>
      </p>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="onDecline()">Decline</button>
      <button mat-flat-button (click)="onAccept()">Accept</button>
    </mat-dialog-actions>
  `,
  styles: `
    mat-dialog-content a {
      color: var(--mat-sys-primary);
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
export class ConsentUpdateDialog {
  constructor(private readonly dialogRef: MatDialogRef<ConsentUpdateDialog>) {}

  onAccept(): void {
    this.dialogRef.close(true);
  }

  onDecline(): void {
    this.dialogRef.close(false);
  }
}
