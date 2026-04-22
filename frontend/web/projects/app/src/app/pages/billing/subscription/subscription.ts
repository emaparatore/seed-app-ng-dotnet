import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { BillingService } from '../../pricing/billing.service';
import { CreateInvoiceRequest, InvoiceRequest, UserSubscription } from '../../pricing/billing.models';
import { InvoiceRequestDialog } from './invoice-request-dialog';

@Component({
  selector: 'app-subscription',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './subscription.html',
  styleUrl: './subscription.scss',
})
export class Subscription implements OnInit {
  private readonly billingService = inject(BillingService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly loading = signal(true);
  protected readonly subscription = signal<UserSubscription | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly portalLoading = signal(false);
  protected readonly requestingInvoice = signal(false);
  private lastInvoiceRequest: InvoiceRequest | null = null;

  protected readonly trialDaysRemaining = computed(() => {
    const sub = this.subscription();
    if (!sub?.trialEnd) return null;
    const now = new Date();
    const trialEnd = new Date(sub.trialEnd);
    const diff = Math.ceil((trialEnd.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));
    return diff > 0 ? diff : 0;
  });

  protected readonly isActive = computed(() => {
    const status = this.subscription()?.status?.toLowerCase();
    return status === 'active' || status === 'trialing';
  });

  protected readonly isTrialing = computed(() => {
    return this.subscription()?.status?.toLowerCase() === 'trialing';
  });

  protected readonly isCanceled = computed(() => {
    return this.subscription()?.status?.toLowerCase() === 'canceled';
  });

  ngOnInit(): void {
    const shouldSync = this.route.snapshot.queryParamMap.get('sync') === '1';
    if (!shouldSync) {
      this.loadSubscription();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.billingService.syncMySubscription().subscribe({
      next: () => this.loadSubscription(),
      error: () => {
        this.snackBar.open('Sincronizzazione con Stripe non riuscita. Mostro i dati locali.', 'Chiudi', { duration: 5000 });
        this.loadSubscription();
      },
    });
  }

  protected loadSubscription(): void {
    this.loading.set(true);
    this.error.set(null);
    this.billingService.getMySubscription().subscribe({
      next: (sub) => {
        this.subscription.set(sub);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Impossibile caricare i dati dell\'abbonamento.');
        this.loading.set(false);
      },
    });
  }

  openPortal(): void {
    this.portalLoading.set(true);
    this.billingService.createPortalSession(window.location.origin + '/billing/subscription?sync=1').subscribe({
      next: (response) => {
        window.location.href = response.portalUrl;
      },
      error: () => {
        this.error.set('Impossibile aprire il portale di pagamento.');
        this.portalLoading.set(false);
      },
    });
  }

  goToPricing(): void {
    this.router.navigate(['/pricing']);
  }

  openInvoiceRequest(): void {
    const prefill: Partial<CreateInvoiceRequest> | null = this.lastInvoiceRequest
      ? {
          customerType: this.lastInvoiceRequest.customerType as 'Individual' | 'Company',
          fullName: this.lastInvoiceRequest.fullName,
          companyName: this.lastInvoiceRequest.companyName ?? undefined,
          address: this.lastInvoiceRequest.address,
          city: this.lastInvoiceRequest.city,
          postalCode: this.lastInvoiceRequest.postalCode,
          country: this.lastInvoiceRequest.country,
          fiscalCode: this.lastInvoiceRequest.fiscalCode ?? undefined,
          vatNumber: this.lastInvoiceRequest.vatNumber ?? undefined,
          sdiCode: this.lastInvoiceRequest.sdiCode ?? undefined,
          pecEmail: this.lastInvoiceRequest.pecEmail ?? undefined,
        }
      : null;

    const openDialog = (data: Partial<CreateInvoiceRequest> | null) => {
      this.dialog
        .open(InvoiceRequestDialog, { width: '560px', maxWidth: '95vw', data })
        .afterClosed()
        .subscribe((result: CreateInvoiceRequest | undefined) => {
          if (!result) return;
          this.requestingInvoice.set(true);
          this.billingService.createInvoiceRequest(result).subscribe({
            next: () => {
              this.requestingInvoice.set(false);
              this.snackBar.open('Richiesta fattura inviata con successo.', 'Chiudi', { duration: 4000 });
            },
            error: (err) => {
              this.requestingInvoice.set(false);
              this.snackBar.open(err.error?.errors?.[0] ?? 'Errore nell\'invio della richiesta.', 'Chiudi', { duration: 5000 });
            },
          });
        });
    };

    if (prefill) {
      openDialog(prefill);
    } else {
      this.billingService.getMyInvoiceRequests().subscribe({
        next: (items) => {
          this.lastInvoiceRequest = items[0] ?? null;
          openDialog(this.lastInvoiceRequest ? {
            customerType: this.lastInvoiceRequest.customerType as 'Individual' | 'Company',
            fullName: this.lastInvoiceRequest.fullName,
            companyName: this.lastInvoiceRequest.companyName ?? undefined,
            address: this.lastInvoiceRequest.address,
            city: this.lastInvoiceRequest.city,
            postalCode: this.lastInvoiceRequest.postalCode,
            country: this.lastInvoiceRequest.country,
            fiscalCode: this.lastInvoiceRequest.fiscalCode ?? undefined,
            vatNumber: this.lastInvoiceRequest.vatNumber ?? undefined,
            sdiCode: this.lastInvoiceRequest.sdiCode ?? undefined,
            pecEmail: this.lastInvoiceRequest.pecEmail ?? undefined,
          } : null);
        },
        error: () => openDialog(null),
      });
    }
  }
}
