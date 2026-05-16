import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { MessageService } from 'primeng/api';
import { AuthService } from './auth.service';

const SKIP_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/forgot-password',
                    '/auth/reset-password', '/auth/google', '/auth/microsoft'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const toast = inject(MessageService, { optional: true });
  const router = inject(Router);
  const token = auth.accessToken();
  const authed = token ? withAuth(req, token) : req;

  return next(authed).pipe(
    catchError((err: HttpErrorResponse) => {
      const skip = SKIP_PATHS.some(p => req.url.includes(p));

      // 402 Payment Required = plan limit hit. Show upgrade prompt.
      if (err.status === 402) {
        const msg = err.error?.error ?? 'You have reached your plan limit. Upgrade to add more.';
        toast?.add({
          severity: 'warn',
          summary: 'Plan limit reached',
          detail: msg + ' Tap to see plans.',
          life: 6000,
          sticky: false
        });
        // navigate after a short delay so the user sees the toast
        setTimeout(() => router.navigateByUrl('/billing'), 400);
        return throwError(() => err);
      }

      if (err.status !== 401 || skip || !auth.refreshToken()) {
        return throwError(() => err);
      }
      return auth.refresh().pipe(
        switchMap(r => next(withAuth(req, r.accessToken))),
        catchError(refreshErr => {
          auth.clear();
          return throwError(() => refreshErr);
        })
      );
    })
  );
};

function withAuth(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}
