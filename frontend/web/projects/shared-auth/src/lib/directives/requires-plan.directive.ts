import { Directive, effect, inject, input, TemplateRef, ViewContainerRef } from '@angular/core';
import { SubscriptionService } from '../services/subscription.service';

@Directive({
  selector: '[requiresPlan]',
})
export class RequiresPlanDirective {
  private readonly subscriptionService = inject(SubscriptionService);
  private readonly templateRef = inject(TemplateRef);
  private readonly viewContainer = inject(ViewContainerRef);

  readonly requiresPlan = input.required<string | string[]>();

  private hasView = false;

  constructor() {
    effect(() => {
      const plan = this.requiresPlan();
      const allowed = Array.isArray(plan)
        ? this.subscriptionService.hasAnyPlan(plan)
        : this.subscriptionService.hasPlan(plan);

      if (allowed && !this.hasView) {
        this.viewContainer.createEmbeddedView(this.templateRef);
        this.hasView = true;
      } else if (!allowed && this.hasView) {
        this.viewContainer.clear();
        this.hasView = false;
      }
    });
  }
}
