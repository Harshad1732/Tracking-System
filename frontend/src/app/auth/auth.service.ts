import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse, TenantDto, UserDto } from './auth.types';

const STORAGE_KEY = 'tracker.auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiBaseUrl;

  private readonly authState = signal<AuthResponse | null>(this.load());
  readonly user = computed<UserDto | null>(() => this.authState()?.user ?? null);
  readonly tenant = computed<TenantDto | null>(() => this.authState()?.tenant ?? null);
  readonly isAuthenticated = computed(() => this.authState() !== null);
  readonly role = computed(() => this.authState()?.user.role ?? null);

  register(payload: { email: string; password: string; fullName?: string; tenantName: string }):
    Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/register`, payload)
      .pipe(tap(r => this.setAuth(r)));
  }

  login(tenantSlug: string, email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/login`, { tenantSlug, email, password })
      .pipe(tap(r => this.setAuth(r)));
  }

  refresh(): Observable<AuthResponse> {
    const refreshToken = this.authState()?.refreshToken;
    return this.http.post<AuthResponse>(`${this.api}/auth/refresh`, { refreshToken })
      .pipe(tap(r => this.setAuth(r)));
  }

  logout(): Observable<void> {
    const refreshToken = this.authState()?.refreshToken;
    this.clear();
    return this.http.post<void>(`${this.api}/auth/logout`, { refreshToken });
  }

  forgotPassword(tenantSlug: string, email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/auth/forgot-password`, { tenantSlug, email });
  }

  resetPassword(token: string, newPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/auth/reset-password`, { token, newPassword });
  }

  google(tenantSlug: string, idToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/google`, { tenantSlug, idToken })
      .pipe(tap(r => this.setAuth(r)));
  }

  microsoft(tenantSlug: string, idToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/microsoft`, { tenantSlug, idToken })
      .pipe(tap(r => this.setAuth(r)));
  }

  accessToken(): string | null { return this.authState()?.accessToken ?? null; }
  refreshToken(): string | null { return this.authState()?.refreshToken ?? null; }

  setAuth(auth: AuthResponse): void {
    this.authState.set(auth);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(auth));
  }

  clear(): void {
    this.authState.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private load(): AuthResponse | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    try { return JSON.parse(raw) as AuthResponse; } catch { return null; }
  }
}
