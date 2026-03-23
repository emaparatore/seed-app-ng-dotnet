import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';
import {
  AdminRole,
  AdminUser,
  AdminUserDetail,
  AssignRolesRequest,
  CreateUserRequest,
  GetUsersParams,
  PagedResult,
  ToggleStatusRequest,
  UpdateUserRequest,
} from './models/user.models';

@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${inject(AUTH_CONFIG).apiUrl}/admin/users`;
  private readonly rolesApiUrl = `${inject(AUTH_CONFIG).apiUrl}/admin/roles`;

  getUsers(params: GetUsersParams = {}): Observable<PagedResult<AdminUser>> {
    let httpParams = new HttpParams();
    if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.searchTerm) httpParams = httpParams.set('searchTerm', params.searchTerm);
    if (params.roleFilter) httpParams = httpParams.set('roleFilter', params.roleFilter);
    if (params.statusFilter !== undefined && params.statusFilter !== null)
      httpParams = httpParams.set('statusFilter', params.statusFilter);
    if (params.dateFrom) httpParams = httpParams.set('dateFrom', params.dateFrom);
    if (params.dateTo) httpParams = httpParams.set('dateTo', params.dateTo);
    if (params.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDescending) httpParams = httpParams.set('sortDescending', params.sortDescending);
    return this.http.get<PagedResult<AdminUser>>(this.apiUrl, { params: httpParams });
  }

  getUserById(id: string): Observable<AdminUserDetail> {
    return this.http.get<AdminUserDetail>(`${this.apiUrl}/${id}`);
  }

  createUser(request: CreateUserRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.apiUrl, request);
  }

  updateUser(id: string, request: UpdateUserRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, request);
  }

  deleteUser(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  toggleUserStatus(id: string, isActive: boolean): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/status`, { isActive } as ToggleStatusRequest);
  }

  assignRoles(id: string, roleNames: string[]): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/roles`, { roleNames } as AssignRolesRequest);
  }

  forcePasswordChange(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/force-password-change`, {});
  }

  resetPassword(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/reset-password`, {});
  }

  getRoles(): Observable<AdminRole[]> {
    return this.http.get<AdminRole[]>(this.rolesApiUrl);
  }
}
