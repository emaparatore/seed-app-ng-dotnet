import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';
import { AdminInvoiceRequest, PagedResult } from './admin-invoice-requests.models';

export interface GetInvoiceRequestsParams {
  pageNumber?: number;
  pageSize?: number;
  status?: string;
}

@Injectable({ providedIn: 'root' })
export class AdminInvoiceRequestsService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${inject(AUTH_CONFIG).apiUrl}/admin/invoice-requests`;

  getInvoiceRequests(params: GetInvoiceRequestsParams): Observable<PagedResult<AdminInvoiceRequest>> {
    let httpParams = new HttpParams();
    if (params.pageNumber != null) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize != null) httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.status) httpParams = httpParams.set('status', params.status);
    return this.http.get<PagedResult<AdminInvoiceRequest>>(this.apiUrl, { params: httpParams });
  }

  updateInvoiceRequestStatus(id: string, newStatus: string): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/status`, { newStatus });
  }
}
