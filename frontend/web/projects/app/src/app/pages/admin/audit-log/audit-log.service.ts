import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';
import { PagedResult } from '../users/models/user.models';
import { AuditLogEntry, GetAuditLogParams } from './audit-log.models';

@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${inject(AUTH_CONFIG).apiUrl}/admin/audit-log`;

  getEntries(params: GetAuditLogParams = {}): Observable<PagedResult<AuditLogEntry>> {
    let httpParams = new HttpParams();
    if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.actionFilter) httpParams = httpParams.set('actionFilter', params.actionFilter);
    if (params.userId) httpParams = httpParams.set('userId', params.userId);
    if (params.dateFrom) httpParams = httpParams.set('dateFrom', params.dateFrom);
    if (params.dateTo) httpParams = httpParams.set('dateTo', params.dateTo);
    if (params.searchTerm) httpParams = httpParams.set('searchTerm', params.searchTerm);
    if (params.sortDescending !== undefined) httpParams = httpParams.set('sortDescending', params.sortDescending);
    return this.http.get<PagedResult<AuditLogEntry>>(this.apiUrl, { params: httpParams });
  }

  getEntryById(id: string): Observable<AuditLogEntry> {
    return this.http.get<AuditLogEntry>(`${this.apiUrl}/${id}`);
  }

  exportCsv(params: GetAuditLogParams = {}): Observable<Blob> {
    let httpParams = new HttpParams();
    if (params.actionFilter) httpParams = httpParams.set('actionFilter', params.actionFilter);
    if (params.userId) httpParams = httpParams.set('userId', params.userId);
    if (params.dateFrom) httpParams = httpParams.set('dateFrom', params.dateFrom);
    if (params.dateTo) httpParams = httpParams.set('dateTo', params.dateTo);
    if (params.searchTerm) httpParams = httpParams.set('searchTerm', params.searchTerm);
    if (params.sortDescending !== undefined) httpParams = httpParams.set('sortDescending', params.sortDescending);
    return this.http.get(`${this.apiUrl}/export`, { params: httpParams, responseType: 'blob' });
  }
}
