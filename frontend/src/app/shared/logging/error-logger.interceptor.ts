import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';

// Endpoints whose request body we never send to the log server (auth/secrets) or that
// would create infinite loops (the log ingest endpoint itself).
const REDACT_BODY_PATHS = [
  '/auth/login',
  '/auth/register',
  '/auth/refresh',
  '/auth/reset-password',
  '/auth/forgot-password',
  '/users/me/reset-password'
];
const SKIP_LOGGING_PATHS = [
  '/logs/client'
];

const MAX_BODY = 4000;

export const errorLoggerInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      // Don't recursively log calls TO the log endpoint, and don't try to log if the
      // network is so broken we can't reach the API at all (we'd spin forever).
      const isSkipped = SKIP_LOGGING_PATHS.some(p => req.url.includes(p));
      if (!isSkipped) {
        void postClientLog(req, err, router);
      }
      return throwError(() => err);
    })
  );
};

async function postClientLog(req: { url: string; method: string; body: unknown },
                             err: HttpErrorResponse,
                             router: Router): Promise<void> {
  try {
    const shouldRedact = REDACT_BODY_PATHS.some(p => req.url.includes(p));
    const requestBody = shouldRedact
      ? '[redacted]'
      : truncate(safeStringify(req.body), MAX_BODY);

    const responseBody = truncate(
      typeof err.error === 'string' ? err.error : safeStringify(err.error),
      MAX_BODY);

    const payload = {
      level: err.status >= 500 ? 'Error' : 'Warning',
      message: err.message || `HTTP ${err.status} ${req.method} ${req.url}`,
      method: req.method,
      path: stripOrigin(req.url),
      statusCode: err.status,
      exceptionType: err.name || 'HttpErrorResponse',
      stackTrace: null,
      requestBody,
      responseBody,
      clientContext: safeStringify({
        route: router.url,
        ua: navigator.userAgent,
        time: new Date().toISOString()
      })
    };

    // Plain fetch (not Angular HttpClient) so this call doesn't re-enter this interceptor.
    await fetch(`${environment.apiBaseUrl}/logs/client`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      keepalive: true
    });
  } catch {
    // Logging must never throw — swallow.
  }
}

function safeStringify(value: unknown): string {
  if (value === undefined || value === null) return '';
  if (typeof value === 'string') return value;
  try { return JSON.stringify(value); } catch { return String(value); }
}

function truncate(value: string, max: number): string {
  if (!value) return value;
  return value.length <= max ? value : value.slice(0, max) + '…[truncated]';
}

function stripOrigin(url: string): string {
  try {
    const u = new URL(url, environment.apiBaseUrl);
    return u.pathname + u.search;
  } catch {
    return url;
  }
}
