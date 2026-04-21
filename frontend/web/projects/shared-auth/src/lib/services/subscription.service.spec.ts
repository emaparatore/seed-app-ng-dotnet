import { TestBed } from '@angular/core/testing';
import { SubscriptionService } from './subscription.service';
import { AuthService } from './auth.service';
import { SubscriptionInfo } from '../models/auth.models';

const proPlan: SubscriptionInfo = {
  currentPlan: 'Pro',
  planFeatures: ['feature-a', 'feature-b'],
  subscriptionStatus: 'Active',
  trialEndsAt: null,
};

describe('SubscriptionService', () => {
  function setup(subscription: SubscriptionInfo | null) {
    const authService = { subscription: () => subscription };

    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: authService }],
    });

    return TestBed.inject(SubscriptionService);
  }

  describe('hasPlan', () => {
    it('should return true when subscription is null (payments module disabled)', () => {
      const service = setup(null);
      expect(service.hasPlan('Pro')).toBe(true);
    });

    it('should return true when plan matches', () => {
      const service = setup(proPlan);
      expect(service.hasPlan('Pro')).toBe(true);
    });

    it('should return false when plan does not match', () => {
      const service = setup(proPlan);
      expect(service.hasPlan('Enterprise')).toBe(false);
    });
  });

  describe('hasAnyPlan', () => {
    it('should return true when subscription is null (payments module disabled)', () => {
      const service = setup(null);
      expect(service.hasAnyPlan(['Pro', 'Enterprise'])).toBe(true);
    });

    it('should return true when current plan is in the list', () => {
      const service = setup(proPlan);
      expect(service.hasAnyPlan(['Basic', 'Pro'])).toBe(true);
    });

    it('should return false when current plan is not in the list', () => {
      const service = setup(proPlan);
      expect(service.hasAnyPlan(['Basic', 'Enterprise'])).toBe(false);
    });
  });

  describe('hasFeature', () => {
    it('should return true when subscription is null (payments module disabled)', () => {
      const service = setup(null);
      expect(service.hasFeature('feature-a')).toBe(true);
    });

    it('should return true when feature is present', () => {
      const service = setup(proPlan);
      expect(service.hasFeature('feature-a')).toBe(true);
    });

    it('should return false when feature is absent', () => {
      const service = setup(proPlan);
      expect(service.hasFeature('feature-c')).toBe(false);
    });
  });
});
