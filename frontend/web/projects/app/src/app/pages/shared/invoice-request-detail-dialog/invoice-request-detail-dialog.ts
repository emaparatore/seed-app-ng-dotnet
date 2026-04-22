import { Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

export interface InvoiceRequestDetailData {
  id: string;
  customerType: string;
  fullName: string;
  companyName: string | null;
  address: string;
  city: string;
  postalCode: string;
  country: string;
  fiscalCode: string | null;
  vatNumber: string | null;
  sdiCode: string | null;
  pecEmail: string | null;
  stripePaymentIntentId: string | null;
  status: string;
  createdAt: string;
  processedAt: string | null;
  userEmail?: string;
  userFullName?: string;
}

@Component({
  selector: 'app-invoice-request-detail-dialog',
  imports: [DatePipe, MatDialogModule, MatButtonModule],
  templateUrl: './invoice-request-detail-dialog.html',
  styles: `
    .detail-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 10px 24px;
      min-width: min(620px, 92vw);
      padding-top: 8px;
    }

    .detail-section {
      grid-column: 1 / -1;
      margin-top: 8px;

      h4 {
        margin: 0;
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
      word-break: break-word;
    }

    .none-value {
      color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.45));
      font-style: italic;
    }

    .status-badge {
      display: inline-block;
      padding: 4px 12px;
      border-radius: 16px;
      font-size: 12px;
      font-weight: 500;
      white-space: nowrap;

      &.requested {
        background: #f5f5f5;
        color: #616161;
      }

      &.in-progress {
        background: #fff3e0;
        color: #e65100;
      }

      &.issued {
        background: #e8f5e9;
        color: #2e7d32;
      }
    }

    @media (max-width: 680px) {
      .detail-grid {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class InvoiceRequestDetailDialog {
  protected readonly data = inject<InvoiceRequestDetailData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<InvoiceRequestDetailDialog>);

  protected close(): void {
    this.dialogRef.close();
  }

  protected statusLabel(status: string): string {
    switch (status) {
      case 'Requested':
        return 'Richiesta';
      case 'InProgress':
        return 'In lavorazione';
      case 'Issued':
        return 'Emessa';
      default:
        return status;
    }
  }

  protected statusClass(status: string): string {
    switch (status) {
      case 'Requested':
        return 'requested';
      case 'InProgress':
        return 'in-progress';
      case 'Issued':
        return 'issued';
      default:
        return '';
    }
  }
}
