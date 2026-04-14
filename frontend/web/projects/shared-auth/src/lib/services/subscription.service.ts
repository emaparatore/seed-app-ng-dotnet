import { Injectable, inject, computed } from '@angular/core';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class SubscriptionService {
  private readonly authService = inject(AuthService);

  readonly currentPlan = computed(() => this.authService.subscription()?.currentPlan ?? null);
  readonly planFeatures = computed(() => this.authService.subscription()?.planFeatures ?? []);
  readonly subscriptionStatus = computed(() => this.authService.subscription()?.subscriptionStatus ?? null);
  readonly trialEndsAt = computed(() => this.authService.subscription()?.trialEndsAt ?? null);

  hasPlan(planName: string): boolean {
    const subscription = this.authService.subscription();
    if (subscription === null) return true;
    return subscription.currentPlan === planName;
  }

  hasAnyPlan(planNames: string[]): boolean {
    const subscription = this.authService.subscription();
    if (subscription === null) return true;
    return planNames.includes(subscription.currentPlan);
  }

  hasFeature(featureKey: string): boolean {
    const subscription = this.authService.subscription();
    if (subscription === null) return true;
    return subscription.planFeatures.includes(featureKey);
  }
}
