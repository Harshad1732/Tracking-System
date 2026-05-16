import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

// Paths a platform admin in 'platform' mode is allowed to visit. Anything outside this
// list bounces them back to /platform/tenants so they can't accidentally land on a
// tenant business page (Dashboard / Workspace / Masters / Reports) while in platform
// mode — those pages would silently show whatever tenant their JWT happens to point at.
const PLATFORM_MODE_ALLOWLIST = ['/platform', '/plans', '/login', '/'];

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
    return false;
  }

  if (auth.isPlatformAdmin() && auth.viewMode() === 'platform') {
    const url = state.url.split('?')[0];
    const allowed = PLATFORM_MODE_ALLOWLIST.some(p =>
      url === p || url.startsWith(p + '/'));
    if (!allowed) {
      router.navigateByUrl('/platform/tenants');
      return false;
    }
  }
  return true;
};

export const roleGuard = (...roles: string[]): CanActivateFn => (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
    return false;
  }
  if (!roles.includes(auth.role() ?? '')) {
    router.navigate(['/dashboard']);
    return false;
  }
  return true;
};
