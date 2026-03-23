import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';
import { SystemHealth } from './system-health.models';

@Injectable({ providedIn: 'root' })
export class SystemHealthService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${inject(AUTH_CONFIG).apiUrl}/admin/system-health`;

  getSystemHealth(): Observable<SystemHealth> {
    return this.http.get<SystemHealth>(this.apiUrl);
  }
}
