import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';
import { Plan } from './billing.models';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${inject(AUTH_CONFIG).apiUrl}/plans`;

  getPlans(): Observable<Plan[]> {
    return this.http.get<Plan[]>(this.apiUrl);
  }
}
