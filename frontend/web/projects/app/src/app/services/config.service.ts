import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';

interface AppConfig {
  paymentsEnabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class ConfigService {
  private readonly http = inject(HttpClient);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly configUrl = `${inject(AUTH_CONFIG).apiUrl}/config`;
  readonly paymentsEnabled = signal(false);

  async loadConfig(): Promise<void> {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    try {
      const config = await firstValueFrom(this.http.get<AppConfig>(this.configUrl));
      this.paymentsEnabled.set(config.paymentsEnabled);
    } catch {
      // Config endpoint unavailable — keep default (payments disabled)
    }
  }
}
