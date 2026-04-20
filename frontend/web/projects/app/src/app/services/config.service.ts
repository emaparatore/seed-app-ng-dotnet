import { Injectable, TransferState, inject, makeStateKey, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AUTH_CONFIG } from 'shared-auth';

interface AppConfig {
  paymentsEnabled: boolean;
}

const CONFIG_STATE_KEY = makeStateKey<AppConfig>('app.config');
const MAX_ATTEMPTS = 5;
const RETRY_DELAY_MS = 1000;

@Injectable({ providedIn: 'root' })
export class ConfigService {
  private readonly http = inject(HttpClient);
  private readonly transferState = inject(TransferState);
  private readonly configUrl = `${inject(AUTH_CONFIG).apiUrl}/config`;
  readonly paymentsEnabled = signal(false);

  async loadConfig(): Promise<void> {
    const transferred = this.transferState.get(CONFIG_STATE_KEY, null);
    if (transferred) {
      this.paymentsEnabled.set(transferred.paymentsEnabled);
      this.transferState.remove(CONFIG_STATE_KEY);
      return;
    }

    for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
      try {
        const config = await firstValueFrom(this.http.get<AppConfig>(this.configUrl));
        this.paymentsEnabled.set(config.paymentsEnabled);
        this.transferState.set(CONFIG_STATE_KEY, config);
        return;
      } catch {
        if (attempt === MAX_ATTEMPTS) return;
        await new Promise((r) => setTimeout(r, RETRY_DELAY_MS * attempt));
      }
    }
  }
}
