import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RequiresPlanDirective } from './requires-plan.directive';
import { SubscriptionService } from '../services/subscription.service';

@Component({
  imports: [RequiresPlanDirective],
  template: `<div *requiresPlan="'Pro'"><span class="protected">Content</span></div>`,
})
class TestComponent {}

describe('RequiresPlanDirective', () => {
  function setup(currentPlan: string | null) {
    const subscriptionService = {
      hasPlan: (plan: string) => currentPlan === null || currentPlan === plan,
      hasAnyPlan: (plans: string[]) => currentPlan === null || plans.includes(currentPlan),
    };

    TestBed.configureTestingModule({
      imports: [TestComponent],
      providers: [{ provide: SubscriptionService, useValue: subscriptionService }],
    });

    const fixture = TestBed.createComponent(TestComponent);
    fixture.detectChanges();

    return { fixture };
  }

  it('should show content when user has the required plan', () => {
    const { fixture } = setup('Pro');
    const el = fixture.nativeElement.querySelector('.protected');
    expect(el).toBeTruthy();
    expect(el.textContent).toBe('Content');
  });

  it('should hide content when user lacks the required plan', () => {
    const { fixture } = setup('Basic');
    const el = fixture.nativeElement.querySelector('.protected');
    expect(el).toBeNull();
  });

  it('should show content when subscription is null (payments module disabled)', () => {
    const { fixture } = setup(null);
    const el = fixture.nativeElement.querySelector('.protected');
    expect(el).toBeTruthy();
    expect(el.textContent).toBe('Content');
  });
});
