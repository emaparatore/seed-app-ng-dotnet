import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';
import { SystemSetting, UpdateSettingItem } from './settings.models';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${inject(AUTH_CONFIG).apiUrl}/admin/settings`;

  getSettings(): Observable<SystemSetting[]> {
    return this.http.get<SystemSetting[]>(this.apiUrl);
  }

  updateSettings(items: UpdateSettingItem[]): Observable<void> {
    return this.http.put<void>(this.apiUrl, { items });
  }
}
