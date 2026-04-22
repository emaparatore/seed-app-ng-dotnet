import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from 'shared-auth';
import { BillingService } from './billing.service';
import { Plan, UserSubscription } from './billing.models';

@Component({
  selector: 'app-pricing',
  imports: [CurrencyPipe, MatCardModule, MatIconModule, MatButtonModule, MatButtonToggleModule, MatProgressSpinnerModule],
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
  protected readonly portalLoading = signal(false);
  protected readonly currentSubscription = signal<UserSubscription | null>(null);

  protected readonly sortedPlans = computed(() =>
    [...this.plans()].sort((a, b) => a.sortOrder - b.sortOrder),
  );
  protected readonly hasPaidSubscription = computed(() => {
    const sub = this.currentSubscription();
    return !!sub && !sub.isFreeTier;
  });

  ngOnInit(): void {
    this.loadPlans();
    if (this.authService.isAuthenticated()) {
      this.loadSubscription();
    }
  }

  protected isActivePlan(plan: Plan): boolean {
    const sub = this.currentSubscription();
    if (!sub) return false;
    return sub.planName === plan.name;
  }

  protected isScheduledPlan(plan: Plan): boolean {
    const sub = this.currentSubscription();
    if (!sub?.scheduledPlanName) return false;
    return sub.scheduledPlanName === plan.name;
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
    this.error.set(null);

    if (!this.authService.isAuthenticated()) {
      this.router.navigate([plan.isFreeTier ? '/register' : '/login']);
      return;
    }

    if (this.hasPaidSubscription()) {
      this.openPortal();
      return;
    }

    if (plan.isFreeTier) {
      this.router.navigate(['/billing/subscription']);
      return;
    }

    this.checkout(plan);
  }

  private checkout(plan: Plan): void {
    this.checkoutLoading.set(true);
    this.billingService
      .createCheckoutSession({
        planId: plan.id,
        billingInterval: this.billingInterval() === 'yearly' ? 'Yearly' : 'Monthly',
        successUrl: window.location.origin + '/billing/success?session_id={CHECKOUT_SESSION_ID}',
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

  private loadSubscription(): void {
    this.billingService.getMySubscription().subscribe({
      next: (sub) => this.currentSubscription.set(sub),
      error: () => {},
    });
  }

  private openPortal(): void {
    this.portalLoading.set(true);
    this.billingService.createPortalSession(window.location.origin + '/billing/subscription').subscribe({
      next: (response) => {
        window.location.href = response.portalUrl;
      },
      error: () => {
        this.error.set('Impossibile aprire il portale Stripe.');
        this.portalLoading.set(false);
      },
    });
  }
}
