import { APP_INITIALIZER, Provider } from '@angular/core';
import { AuthService } from '../services/auth.service';

export function provideAuthInitializer(): Provider {
  return {
    provide: APP_INITIALIZER,
    useFactory: (authService: AuthService) => () => authService.initializeAuth(),
    deps: [AuthService],
    multi: true,
  };
}
