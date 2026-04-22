import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';
import { CheckoutConfirmationResponse, CheckoutSessionResponse, CreateCheckoutRequest, CreateInvoiceRequest, InvoiceRequest, Plan, PortalSessionResponse, UserSubscription } from './billing.models';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(AUTH_CONFIG).apiUrl;
  private readonly plansUrl = `${this.baseUrl}/plans`;
  private readonly billingUrl = `${this.baseUrl}/billing`;

  getPlans(): Observable<Plan[]> {
    return this.http.get<Plan[]>(this.plansUrl);
  }

  createCheckoutSession(request: CreateCheckoutRequest): Observable<CheckoutSessionResponse> {
    return this.http.post<CheckoutSessionResponse>(`${this.billingUrl}/checkout`, request);
  }

  confirmCheckoutSession(sessionId: string): Observable<CheckoutConfirmationResponse> {
    return this.http.post<CheckoutConfirmationResponse>(`${this.billingUrl}/checkout/confirm`, { sessionId });
  }

  getMySubscription(): Observable<UserSubscription | null> {
    return this.http.get<UserSubscription | null>(`${this.billingUrl}/subscription`);
  }

  createPortalSession(returnUrl: string): Observable<PortalSessionResponse> {
    return this.http.post<PortalSessionResponse>(`${this.billingUrl}/portal`, { returnUrl });
  }

  createInvoiceRequest(request: CreateInvoiceRequest): Observable<string> {
    return this.http.post<string>(`${this.billingUrl}/invoice-request`, request);
  }

  getMyInvoiceRequests(): Observable<InvoiceRequest[]> {
    return this.http.get<InvoiceRequest[]>(`${this.billingUrl}/invoice-requests`);
  }
}
