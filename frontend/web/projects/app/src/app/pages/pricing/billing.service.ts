import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';
import { CheckoutSessionResponse, CreateCheckoutRequest, Plan } from './billing.models';

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
}
