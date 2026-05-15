import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const SKIP_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/forgot-password',
                    '/auth/reset-password', '/auth/google', '/auth/microsoft'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken();
  const authed = token ? withAuth(req, token) : req;

  return next(authed).pipe(
    catchError((err: HttpErrorResponse) => {
      const skip = SKIP_PATHS.some(p => req.url.includes(p));
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
