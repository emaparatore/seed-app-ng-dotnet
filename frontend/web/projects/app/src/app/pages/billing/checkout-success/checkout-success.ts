import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BillingService } from '../../pricing/billing.service';

@Component({
  selector: 'app-checkout-success',
  imports: [RouterLink, MatCardModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './checkout-success.html',
  styleUrl: './checkout-success.scss',
})
export class CheckoutSuccess implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly billingService = inject(BillingService);

  protected readonly loading = signal(true);
  protected readonly confirmed = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    const sessionId = this.route.snapshot.queryParamMap.get('session_id');
    if (!sessionId) {
      this.loading.set(false);
      this.error.set('Sessione di checkout non valida.');
      return;
    }

    this.billingService.confirmCheckoutSession(sessionId).subscribe({
      next: () => {
        this.confirmed.set(true);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Pagamento registrato su Stripe, ma non ancora confermato in piattaforma. Contatta il supporto.');
        this.loading.set(false);
      },
    });
  }
}
