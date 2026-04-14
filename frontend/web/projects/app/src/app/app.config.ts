import { APP_INITIALIZER, ApplicationConfig, provideBrowserGlobalErrorListeners, isDevMode } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideServiceWorker } from '@angular/service-worker';
import { AUTH_CONFIG, authInterceptor, provideAuthInitializer } from 'shared-auth';
import { ConfigService } from './services/config.service';

import { routes } from './app.routes';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideClientHydration(withEventReplay()),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    provideAnimationsAsync(),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
    { provide: AUTH_CONFIG, useValue: { apiUrl: environment.apiUrl } },
    provideAuthInitializer(),
    {
      provide: APP_INITIALIZER,
      useFactory: (configService: ConfigService) => () => configService.loadConfig(),
      deps: [ConfigService],
      multi: true,
    },
  ],
};
