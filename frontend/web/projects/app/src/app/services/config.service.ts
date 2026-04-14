import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';

interface AppConfig {
  paymentsEnabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class ConfigService {
  private readonly http = inject(HttpClient);
  private readonly configUrl = `${inject(AUTH_CONFIG).apiUrl}/config`;
  readonly paymentsEnabled = signal(false);

  async loadConfig(): Promise<void> {
    try {
      const config = await firstValueFrom(this.http.get<AppConfig>(this.configUrl));
      this.paymentsEnabled.set(config.paymentsEnabled);
    } catch {
      // Config endpoint unavailable — keep default (payments disabled)
    }
  }
}
