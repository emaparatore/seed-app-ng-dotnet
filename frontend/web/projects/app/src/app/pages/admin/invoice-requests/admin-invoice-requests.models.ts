import { PagedResult } from '../users/models/user.models';

export type { PagedResult };

export interface AdminInvoiceRequest {
  id: string;
  userEmail: string;
  userFullName: string;
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
