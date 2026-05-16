import { inject, provideAppInitializer } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

const SEEDED_SLUG = 'demo';
const SEEDED_EMAIL = 'admin@tracker.local';
const SEEDED_PASSWORD = 'Admin#12345';
const LOGIN_TIMEOUT_MS = 3000;
const FAKE_TOKEN = 'dev-bypass-token';

export const provideDummyAuthBootstrap = () =>
  provideAppInitializer(async () => {
    if (!environment.useDummyAuth) return;
    const auth = inject(AuthService);

    // Self-heal stale fake tokens from earlier sessions so we always try to
    // pick up a real JWT when the backend is reachable.
    if (auth.accessToken() === FAKE_TOKEN) {
      auth.clear();
    }
    if (auth.isAuthenticated()) return;

    try {
      await Promise.race([
        firstValueFrom(auth.login(SEEDED_SLUG, SEEDED_EMAIL, SEEDED_PASSWORD)),
        new Promise<never>((_, reject) =>
          setTimeout(() => reject(new Error('login-timeout')), LOGIN_TIMEOUT_MS)
        )
      ]);
    } catch {
      auth.seedFakeAuth();
    }
  });
