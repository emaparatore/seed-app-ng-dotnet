export interface CheckoutSessionResponse {
  checkoutUrl: string;
}

export interface CreateCheckoutRequest {
  planId: string;
  billingInterval: 'Monthly' | 'Yearly';
  successUrl: string;
  cancelUrl: string;
}

export interface PlanFeature {
  id: string;
  key: string;
  description: string;
  limitValue: string | null;
  sortOrder: number;
}

export interface Plan {
  id: string;
  name: string;
  description: string | null;
  monthlyPrice: number;
  yearlyPrice: number;
  trialDays: number;
  isFreeTier: boolean;
  isDefault: boolean;
  isPopular: boolean;
  sortOrder: number;
  features: PlanFeature[];
}
