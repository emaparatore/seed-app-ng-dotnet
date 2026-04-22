import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { BillingService } from '../../pricing/billing.service';
import { CreateInvoiceRequest, InvoiceRequest, UserSubscription } from '../../pricing/billing.models';
import { InvoiceRequestDialog, InvoiceRequestDialogData } from '../subscription/invoice-request-dialog';
import { InvoiceRequestDetailDialog } from '../../shared/invoice-request-detail-dialog/invoice-request-detail-dialog';

@Component({
  selector: 'app-invoice-requests',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule,
  ],
  templateUrl: './invoice-requests.html',
  styleUrl: './invoice-requests.scss',
})
export class InvoiceRequests implements OnInit {
  private readonly billingService = inject(BillingService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly loading = signal(true);
  protected readonly requests = signal<InvoiceRequest[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly subscription = signal<UserSubscription | null>(null);

  protected readonly displayedColumns = ['createdAt', 'service', 'amountPaid', 'customerType', 'name', 'status', 'processedAt', 'actions'];

  ngOnInit(): void {
    this.loadRequests();
    this.loadSubscriptionContext();
  }

  protected loadRequests(): void {
    this.loading.set(true);
    this.error.set(null);
    this.billingService.getMyInvoiceRequests().subscribe({
      next: (items) => {
        this.requests.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Impossibile caricare le richieste fattura.');
        this.loading.set(false);
      },
    });
  }

  protected openNewRequest(): void {
    const subscription = this.subscription();
    if (!subscription) {
      this.snackBar.open('Nessun acquisto disponibile da associare alla richiesta.', 'Chiudi', { duration: 5000 });
      return;
    }

    const latest = this.requests()[0] ?? null;
    const prefill: Partial<CreateInvoiceRequest> | null = latest
      ? {
          customerType: latest.customerType as 'Individual' | 'Company',
          fullName: latest.fullName,
          companyName: latest.companyName ?? undefined,
          address: latest.address,
          city: latest.city,
          postalCode: latest.postalCode,
          country: latest.country,
          fiscalCode: latest.fiscalCode ?? undefined,
          vatNumber: latest.vatNumber ?? undefined,
          sdiCode: latest.sdiCode ?? undefined,
          pecEmail: latest.pecEmail ?? undefined,
          userSubscriptionId: latest.userSubscriptionId ?? subscription.id,
        }
      : null;

    const dialogData: InvoiceRequestDialogData = {
      prefill: prefill ?? undefined,
      purchaseContext: {
        userSubscriptionId: subscription.id,
        serviceName: subscription.planName,
        periodStart: subscription.currentPeriodStart,
        periodEnd: subscription.currentPeriodEnd,
      },
    };

    this.dialog
      .open(InvoiceRequestDialog, { width: '560px', maxWidth: '95vw', data: dialogData })
      .afterClosed()
      .subscribe((result: CreateInvoiceRequest | undefined) => {
        if (!result) return;
        this.submitting.set(true);
        this.billingService.createInvoiceRequest(result).subscribe({
          next: () => {
            this.submitting.set(false);
            this.snackBar.open('Richiesta fattura inviata con successo.', 'Chiudi', { duration: 4000 });
            this.loadRequests();
          },
          error: (err) => {
            this.submitting.set(false);
            this.snackBar.open(err.error?.errors?.[0] ?? 'Errore nell\'invio della richiesta.', 'Chiudi', { duration: 5000 });
          },
        });
      });
  }

  private loadSubscriptionContext(): void {
    this.billingService.getMySubscription().subscribe({
      next: (sub) => {
        this.subscription.set(sub && !sub.isFreeTier ? sub : null);
      },
      error: () => {
        this.subscription.set(null);
      },
    });
  }

  protected openDetails(req: InvoiceRequest): void {
    this.dialog.open(InvoiceRequestDetailDialog, {
      width: '720px',
      maxWidth: '96vw',
      data: req,
    });
  }

  protected statusLabel(status: string): string {
    switch (status) {
      case 'Requested': return 'Richiesta';
      case 'InProgress': return 'In lavorazione';
      case 'Issued': return 'Emessa';
      default: return status;
    }
  }

  protected statusClass(status: string): string {
    switch (status) {
      case 'Requested': return 'requested';
      case 'InProgress': return 'in-progress';
      case 'Issued': return 'issued';
      default: return '';
    }
  }
}
