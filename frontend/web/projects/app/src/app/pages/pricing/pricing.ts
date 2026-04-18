import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from 'shared-auth';
import { BillingService } from './billing.service';
import { Plan, UserSubscription } from './billing.models';
import { ConfirmDialog } from '../admin/users/confirm-dialog/confirm-dialog';

@Component({
  selector: 'app-pricing',
  imports: [CurrencyPipe, MatCardModule, MatIconModule, MatButtonModule, MatButtonToggleModule, MatProgressSpinnerModule],
  templateUrl: './pricing.html',
  styleUrl: './pricing.scss',
})
export class Pricing implements OnInit {
  private readonly billingService = inject(BillingService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  protected readonly authService = inject(AuthService);
  private readonly currencyFormatter = new Intl.NumberFormat('it-IT', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });

  protected readonly loading = signal(true);
  protected readonly plans = signal<Plan[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly billingInterval = signal<'monthly' | 'yearly'>('monthly');
  protected readonly checkoutLoading = signal(false);
  protected readonly currentSubscription = signal<UserSubscription | null>(null);

  protected readonly sortedPlans = computed(() =>
    [...this.plans()].sort((a, b) => a.sortOrder - b.sortOrder),
  );

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
    if (plan.isFreeTier) {
      this.router.navigate(['/register']);
      return;
    }
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/login']);
      return;
    }

    const sub = this.currentSubscription();
    const hasActiveSubscription = sub && !sub.isFreeTier;

    if (hasActiveSubscription) {
      this.confirmChangePlan(plan, sub);
    } else {
      this.checkout(plan);
    }
  }

  private confirmChangePlan(plan: Plan, subscription: UserSubscription): void {
    const preview = this.getPlanChangePreview(plan, subscription);
    const dialogRef = this.dialog.open(ConfirmDialog, {
      width: '520px',
      data: {
        title: 'Conferma cambio piano',
        message: this.buildChangePlanConfirmationMessage(plan, subscription, preview),
        confirmText: 'Conferma cambio',
        cancelText: 'Annulla',
      },
    });

    dialogRef.afterClosed().subscribe((confirmed: boolean | undefined) => {
      if (!confirmed) return;
      this.changePlan(plan);
    });
  }

  private changePlan(plan: Plan): void {
    this.checkoutLoading.set(true);
    this.billingService
      .changePlan({
        planId: plan.id,
        billingInterval: this.billingInterval() === 'yearly' ? 'Yearly' : 'Monthly',
        returnUrl: window.location.origin + '/billing/subscription',
      })
      .subscribe({
        next: (res) => {
          if (res.redirectUrl) {
            window.location.href = res.redirectUrl;
          } else {
            this.checkoutLoading.set(false);
            this.router.navigate(['/billing/subscription']);
          }
        },
        error: (err) => {
          this.error.set(err.error?.errors?.[0] ?? 'Errore durante il cambio piano.');
          this.checkoutLoading.set(false);
        },
      });
  }

  private checkout(plan: Plan): void {
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

  private loadSubscription(): void {
    this.billingService.getMySubscription().subscribe({
      next: (sub) => this.currentSubscription.set(sub),
      error: () => {},
    });
  }

  private buildChangePlanConfirmationMessage(
    plan: Plan,
    subscription: UserSubscription,
    preview: { changeLabel: string; timingLabel: string; currentPrice: number; targetPrice: number },
  ): string {
    const currentIntervalLabel = this.getIntervalLabel(this.inferBillingInterval(subscription));
    const targetIntervalLabel = this.getIntervalLabel(this.billingInterval());
    const currentPrice = this.formatCurrency(preview.currentPrice);
    const targetPrice = this.formatCurrency(preview.targetPrice);

    return `Passerai da ${subscription.planName} (${currentPrice}/${currentIntervalLabel}) a ${plan.name} ` +
      `(${targetPrice}/${targetIntervalLabel}). ` +
      `Tipo cambio: ${preview.changeLabel}. ` +
      `${preview.timingLabel}`;
  }

  private getPlanChangePreview(
    plan: Plan,
    subscription: UserSubscription,
  ): { changeLabel: string; timingLabel: string; currentPrice: number; targetPrice: number } {
    const currentInterval = this.inferBillingInterval(subscription);
    const targetInterval = this.billingInterval();
    const currentPrice = this.getSubscriptionPriceForInterval(subscription, currentInterval);
    const targetPrice = this.getPrice(plan);
    const currentMonthlyEquivalent = this.toMonthlyEquivalent(currentPrice, currentInterval);
    const targetMonthlyEquivalent = this.toMonthlyEquivalent(targetPrice, targetInterval);

    if (targetMonthlyEquivalent < currentMonthlyEquivalent) {
      return {
        changeLabel: 'Downgrade',
        timingLabel: 'Il cambio verrà applicato alla fine del periodo corrente.',
        currentPrice,
        targetPrice,
      };
    }

    if (targetMonthlyEquivalent > currentMonthlyEquivalent) {
      return {
        changeLabel: 'Upgrade',
        timingLabel: 'Il cambio verrà applicato subito con eventuale pro-rata.',
        currentPrice,
        targetPrice,
      };
    }

    return {
      changeLabel: 'Cambio laterale',
      timingLabel: 'Il cambio verrà applicato subito.',
      currentPrice,
      targetPrice,
    };
  }

  private getSubscriptionPriceForInterval(
    subscription: UserSubscription,
    interval: 'monthly' | 'yearly',
  ): number {
    return interval === 'yearly' ? subscription.yearlyPrice : subscription.monthlyPrice;
  }

  private inferBillingInterval(subscription: UserSubscription): 'monthly' | 'yearly' {
    const start = new Date(subscription.currentPeriodStart).getTime();
    const end = new Date(subscription.currentPeriodEnd).getTime();

    if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start) {
      return 'monthly';
    }

    const daysInPeriod = (end - start) / (1000 * 60 * 60 * 24);
    return daysInPeriod >= 180 ? 'yearly' : 'monthly';
  }

  private toMonthlyEquivalent(price: number, interval: 'monthly' | 'yearly'): number {
    return interval === 'yearly' ? price / 12 : price;
  }

  private formatCurrency(price: number): string {
    return this.currencyFormatter.format(price);
  }

  private getIntervalLabel(interval: 'monthly' | 'yearly'): string {
    return interval === 'yearly' ? 'anno' : 'mese';
  }
}
