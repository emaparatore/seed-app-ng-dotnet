import { PagedResult } from '../users/models/user.models';

export type { PagedResult };

export interface SubscriptionMetrics {
  mrr: number;
  activeCount: number;
  trialingCount: number;
  churnRate: number;
}

export interface AdminSubscription {
  id: string;
  userEmail: string;
  planName: string;
  status: string;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  trialEnd: string | null;
  canceledAt: string | null;
  createdAt: string;
}

export interface AdminSubscriptionDetail extends AdminSubscription {
  userId: string;
  userFullName: string;
  planId: string;
  monthlyPrice: number;
  yearlyPrice: number;
  stripeSubscriptionId: string | null;
  stripeCustomerId: string | null;
  updatedAt: string;
}

export interface GetSubscriptionsParams {
  pageNumber?: number;
  pageSize?: number;
  planId?: string;
  status?: string;
}
