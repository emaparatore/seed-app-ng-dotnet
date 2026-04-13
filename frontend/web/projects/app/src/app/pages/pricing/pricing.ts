import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from 'shared-auth';
import { BillingService } from './billing.service';
import { Plan } from './billing.models';

@Component({
  selector: 'app-pricing',
  imports: [CurrencyPipe, RouterLink, MatCardModule, MatIconModule, MatButtonModule, MatButtonToggleModule, MatProgressSpinnerModule],
  templateUrl: './pricing.html',
  styleUrl: './pricing.scss',
})
export class Pricing implements OnInit {
  private readonly billingService = inject(BillingService);
  private readonly router = inject(Router);
  protected readonly authService = inject(AuthService);

  protected readonly loading = signal(true);
  protected readonly plans = signal<Plan[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly billingInterval = signal<'monthly' | 'yearly'>('monthly');
  protected readonly checkoutLoading = signal(false);

  protected readonly sortedPlans = computed(() =>
    [...this.plans()].sort((a, b) => a.sortOrder - b.sortOrder),
  );

  ngOnInit(): void {
    this.loadPlans();
  }

  reload(): void {
    this.loadPlans();
  }

  protected getPrice(plan: Plan): number {
    return this.billingInterval() === 'yearly' ? plan.yearlyPrice : plan.monthlyPrice;
  }

  protected getYearlyDiscount(plan: Plan): number {
    if (plan.monthlyPrice === 0) return 0;
    const yearly = plan.yearlyPrice * 12;
    const monthly = plan.monthlyPrice * 12;
    return Math.round(((monthly - yearly) / monthly) * 100);
  }

  protected onCtaClick(plan: Plan): void {
    if (plan.isFreeTier) {
      this.router.navigate(['/register']);
      return;
    }
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/login']);
      return;
    }
    this.checkoutLoading.set(true);
    this.billingService
      .createCheckoutSession({
        planId: plan.id,
        billingInterval: this.billingInterval() === 'yearly' ? 'Yearly' : 'Monthly',
        successUrl: window.location.origin + '/billing/success',
        cancelUrl: window.location.origin + '/billing/cancel',
      })
      .subscribe({
        next: (res) => {
          window.location.href = res.checkoutUrl;
        },
        error: (err) => {
          this.error.set(err.error?.errors?.[0] ?? 'Errore durante la creazione della sessione di pagamento.');
          this.checkoutLoading.set(false);
        },
      });
  }

  private loadPlans(): void {
    this.loading.set(true);
    this.error.set(null);
    this.billingService.getPlans().subscribe({
      next: (data) => {
        this.plans.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore nel caricamento dei piani.');
        this.loading.set(false);
      },
    });
  }
}
