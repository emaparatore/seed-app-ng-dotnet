import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';
import { AdminSubscription, AdminSubscriptionDetail, GetSubscriptionsParams, PagedResult, SubscriptionMetrics } from './admin-subscriptions.models';

@Injectable({ providedIn: 'root' })
export class AdminSubscriptionsService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${inject(AUTH_CONFIG).apiUrl}/admin/subscriptions`;

  getMetrics(): Observable<SubscriptionMetrics> {
    return this.http.get<SubscriptionMetrics>(`${this.apiUrl}/metrics`);
  }

  getSubscriptions(params: GetSubscriptionsParams): Observable<PagedResult<AdminSubscription>> {
    let httpParams = new HttpParams();
    if (params.pageNumber != null) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize != null) httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.planId) httpParams = httpParams.set('planId', params.planId);
    if (params.status) httpParams = httpParams.set('status', params.status);
    return this.http.get<PagedResult<AdminSubscription>>(this.apiUrl, { params: httpParams });
  }

  getSubscriptionById(id: string): Observable<AdminSubscriptionDetail> {
    return this.http.get<AdminSubscriptionDetail>(`${this.apiUrl}/${id}`);
  }
}
