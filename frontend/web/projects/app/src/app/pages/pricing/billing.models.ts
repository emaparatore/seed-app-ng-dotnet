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

export interface CheckoutConfirmationResponse {
  confirmed: boolean;
  status: string;
}

export interface SyncSubscriptionResponse {
  synced: boolean;
  status: string;
  planName: string | null;
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
  userSubscriptionId: string | null;
  stripeInvoiceId: string | null;
  currency: string | null;
  amountSubtotal: number | null;
  amountTax: number | null;
  amountTotal: number | null;
  amountPaid: number | null;
  isProrationApplied: boolean | null;
  prorationAmount: number | null;
  billingReason: string | null;
  serviceName: string | null;
  servicePeriodStart: string | null;
  servicePeriodEnd: string | null;
  invoicePeriodStart: string | null;
  invoicePeriodEnd: string | null;
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
  userSubscriptionId: string;
  stripePaymentIntentId?: string;
}
