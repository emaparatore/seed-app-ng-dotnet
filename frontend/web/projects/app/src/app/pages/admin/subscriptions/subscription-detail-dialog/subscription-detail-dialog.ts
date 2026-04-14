import { Component, inject } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AdminSubscriptionDetail } from '../admin-subscriptions.models';

@Component({
  selector: 'app-subscription-detail-dialog',
  imports: [
    DatePipe,
    DecimalPipe,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './subscription-detail-dialog.html',
  styles: `
    .detail-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px 24px;
      min-width: min(520px, 80vw);
    }

    .detail-section {
      grid-column: 1 / -1;
      margin-top: 12px;

      h4 {
        margin: 0 0 8px;
        font-size: 13px;
        font-weight: 600;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
        text-transform: uppercase;
        letter-spacing: 0.5px;
      }
    }

    .detail-item {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .detail-label {
      font-size: 12px;
      color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
    }

    .detail-value {
      font-size: 14px;
      color: var(--mat-sys-on-surface, #000);
      word-break: break-all;
    }

    .status-badge {
      display: inline-block;
      padding: 4px 12px;
      border-radius: 16px;
      font-size: 12px;
      font-weight: 500;
      white-space: nowrap;

      &.active {
        background: #e8f5e9;
        color: #2e7d32;
      }

      &.trialing {
        background: #e3f2fd;
        color: #1565c0;
      }

      &.past-due {
        background: #fff3e0;
        color: #e65100;
      }

      &.canceled {
        background: #ffebee;
        color: #c62828;
      }

      &.expired {
        background: #f5f5f5;
        color: #757575;
      }
    }

    .none-value {
      color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.4));
      font-style: italic;
    }

    @media (max-width: 480px) {
      .detail-grid {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class SubscriptionDetailDialog {
  protected readonly data = inject<AdminSubscriptionDetail>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<SubscriptionDetailDialog>);

  protected close(): void {
    this.dialogRef.close();
  }
}
