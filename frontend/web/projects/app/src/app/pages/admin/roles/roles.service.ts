import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';
import { AdminRole, AdminRoleDetail, CreateRoleRequest, Permission, UpdateRoleRequest } from './models/role.models';

@Injectable({ providedIn: 'root' })
export class AdminRolesService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${inject(AUTH_CONFIG).apiUrl}/admin/roles`;

  getRoles(): Observable<AdminRole[]> {
    return this.http.get<AdminRole[]>(this.apiUrl);
  }

  getRoleById(id: string): Observable<AdminRoleDetail> {
    return this.http.get<AdminRoleDetail>(`${this.apiUrl}/${id}`);
  }

  createRole(request: CreateRoleRequest): Observable<string> {
    return this.http.post<string>(this.apiUrl, request);
  }

  updateRole(id: string, request: UpdateRoleRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.apiUrl}/${id}`, request);
  }

  deleteRole(id: string): Observable<boolean> {
    return this.http.delete<boolean>(`${this.apiUrl}/${id}`);
  }

  getPermissions(): Observable<Permission[]> {
    return this.http.get<Permission[]>(`${this.apiUrl}/permissions`);
  }
}
