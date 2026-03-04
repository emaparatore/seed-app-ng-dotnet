import { InjectionToken } from '@angular/core';

export interface AuthConfig {
  apiUrl: string;
}

export const AUTH_CONFIG = new InjectionToken<AuthConfig>('AUTH_CONFIG');
