import { PlanFeature } from '../../pricing/billing.models';

export type { PlanFeature };

export interface AdminPlan {
  id: string;
  name: string;
  description: string | null;
  monthlyPrice: number;
  yearlyPrice: number;
  stripePriceIdMonthly: string | null;
  stripePriceIdYearly: string | null;
  stripeProductId: string | null;
  trialDays: number;
  isFreeTier: boolean;
  isDefault: boolean;
  isPopular: boolean;
  status: string;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
  subscriberCount: number;
  features: PlanFeature[];
}

export interface CreatePlanFeatureRequest {
  key: string;
  description: string;
  limitValue: string | null;
  sortOrder: number;
}

export interface CreatePlanRequest {
  name: string;
  description: string | null;
  monthlyPrice: number;
  yearlyPrice: number;
  trialDays: number;
  isFreeTier: boolean;
  isDefault: boolean;
  isPopular: boolean;
  sortOrder: number;
  features: CreatePlanFeatureRequest[];
}
