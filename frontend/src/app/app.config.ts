import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { providePrimeNG } from 'primeng/config';
import { MessageService, ConfirmationService } from 'primeng/api';
import Aura from '@primeuix/themes/aura';

import { routes } from './app.routes';
import { authInterceptor } from './auth/auth.interceptor';
import { errorLoggerInterceptor } from './shared/logging/error-logger.interceptor';
import { provideDummyAuthBootstrap } from './auth/dummy-auth.bootstrap';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // Order matters: errorLoggerInterceptor wraps everything so it sees responses AFTER
    // authInterceptor has resolved 401-retries. Adding it last means it sits outermost
    // in the request pipeline and only fires for terminal failures.
    provideHttpClient(withInterceptors([authInterceptor, errorLoggerInterceptor])),
    provideAnimationsAsync(),
    provideDummyAuthBootstrap(),
    MessageService,
    ConfirmationService,
    providePrimeNG({
      theme: {
        preset: Aura,
        options: {
          darkModeSelector: '.app-dark'
        }
      }
    })
  ]
};
