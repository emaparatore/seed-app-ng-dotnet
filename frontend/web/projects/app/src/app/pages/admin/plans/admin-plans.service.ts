import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';
import { AdminPlan, CreatePlanRequest } from './admin-plans.models';

@Injectable({ providedIn: 'root' })
export class AdminPlansService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${inject(AUTH_CONFIG).apiUrl}/admin/plans`;

  getPlans(): Observable<AdminPlan[]> {
    return this.http.get<AdminPlan[]>(this.apiUrl);
  }

  getPlanById(id: string): Observable<AdminPlan> {
    return this.http.get<AdminPlan>(`${this.apiUrl}/${id}`);
  }

  createPlan(request: CreatePlanRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.apiUrl, request);
  }

  updatePlan(id: string, request: CreatePlanRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, request);
  }

  archivePlan(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/archive`, {});
  }
}
