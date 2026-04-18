export interface UserSubscription {
  id: string;
  planName: string;
  planDescription: string | null;
  status: string;
  monthlyPrice: number;
  yearlyPrice: number;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  trialEnd: string | null;
  canceledAt: string | null;
  isFreeTier: boolean;
  features: PlanFeature[];
  scheduledPlanName: string | null;
  scheduledChangeDate: string | null;
}

export interface PortalSessionResponse {
  portalUrl: string;
}

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

export interface InvoiceRequest {
  id: string;
  customerType: string;
  fullName: string;
  companyName: string | null;
  address: string;
  city: string;
  postalCode: string;
  country: string;
  fiscalCode: string | null;
  vatNumber: string | null;
  sdiCode: string | null;
  pecEmail: string | null;
  stripePaymentIntentId: string | null;
  status: string;
  createdAt: string;
  processedAt: string | null;
}

export interface CreateInvoiceRequest {
  customerType: 'Individual' | 'Company';
  fullName: string;
  companyName?: string;
  address: string;
  city: string;
  postalCode: string;
  country: string;
  fiscalCode?: string;
  vatNumber?: string;
  sdiCode?: string;
  pecEmail?: string;
  stripePaymentIntentId?: string;
}
