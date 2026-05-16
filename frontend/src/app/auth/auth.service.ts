import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { ACTIONS, AuthResponse, hasPermission, TenantDto, UserDto } from './auth.types';

const STORAGE_KEY = 'tracker.auth';
const VIEW_MODE_KEY = 'tracker.viewMode';

export type ViewMode = 'platform' | 'tenant';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiBaseUrl;

  private readonly authState = signal<AuthResponse | null>(this.load());
  readonly user = computed<UserDto | null>(() => this.authState()?.user ?? null);
  readonly tenant = computed<TenantDto | null>(() => this.authState()?.tenant ?? null);
  readonly isAuthenticated = computed(() => this.authState() !== null);

  /** First role name on the user, kept for badges/labels. UI gating should use `has()`. */
  readonly role = computed(() => this.user()?.roles?.[0] ?? null);
  readonly roles = computed(() => this.user()?.roles ?? []);

  readonly isSystemAdmin = computed(() => this.user()?.isSystemAdmin === true);
  readonly isPlatformAdmin = computed(() => this.user()?.isPlatformAdmin === true);
  readonly currentPlantId  = computed(() => this.user()?.currentPlantId ?? null);
  readonly lockedPlantId   = computed(() => this.user()?.lockedPlantId ?? null);
  readonly isPlantLocked   = computed(() => this.lockedPlantId() !== null);

  /** Per-resource permission check. Use this for new code. */
  has(resource: string, action: string): boolean {
    const u = this.user();
    if (!u) return false;
    if (u.isSystemAdmin || u.isPlatformAdmin) return true;
    return hasPermission(u.permissions, resource, action);
  }

  /**
   * Back-compat shortcuts. Return true if the user has the action on ANY resource.
   * Existing templates use these for generic button gating; the server enforces the
   * real per-resource check. New code should call `has(resource, action)` instead.
   */
  readonly canAdd        = computed(() => this.anyResourceAction(ACTIONS.Add));
  readonly canEdit       = computed(() => this.anyResourceAction(ACTIONS.Edit));
  readonly canDelete     = computed(() => this.anyResourceAction(ACTIONS.Delete));
  readonly canViewReports = computed(() =>
    this.isSystemAdmin() || this.isPlatformAdmin() ||
    this.user()?.permissions.some(p => p.resource === 'Reports' && p.action === ACTIONS.View) === true);

  private anyResourceAction(action: string): boolean {
    const u = this.user();
    if (!u) return false;
    if (u.isSystemAdmin || u.isPlatformAdmin) return true;
    return u.permissions.some(p => p.action === action);
  }

  private readonly viewModeSignal = signal<ViewMode>(this.loadViewMode());
  readonly viewMode = this.viewModeSignal.asReadonly();

  setViewMode(mode: ViewMode): void {
    this.viewModeSignal.set(mode);
    try { localStorage.setItem(VIEW_MODE_KEY, mode); } catch { /* ignore */ }
  }

  register(payload: { email: string; password: string; fullName?: string; tenantName: string }):
    Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/register`, payload)
      .pipe(tap(r => this.acceptFreshAuth(r)));
  }

  login(tenantSlug: string, email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/login`, { tenantSlug, email, password })
      .pipe(tap(r => this.acceptFreshAuth(r)));
  }

  refresh(): Observable<AuthResponse> {
    const refreshToken = this.authState()?.refreshToken;
    return this.http.post<AuthResponse>(`${this.api}/auth/refresh`, { refreshToken })
      .pipe(tap(r => this.setAuth(r)));
  }

  private acceptFreshAuth(r: AuthResponse): void {
    this.setAuth(r);
    this.setViewMode(r.user.isPlatformAdmin ? 'platform' : 'tenant');
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
      .pipe(tap(r => this.acceptFreshAuth(r)));
  }

  microsoft(tenantSlug: string, idToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/microsoft`, { tenantSlug, idToken })
      .pipe(tap(r => this.acceptFreshAuth(r)));
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
    this.setViewMode('tenant');
  }

  private loadViewMode(): ViewMode {
    try {
      const v = localStorage.getItem(VIEW_MODE_KEY);
      if (v === 'platform' || v === 'tenant') return v;
    } catch { /* ignore */ }
    return 'tenant';
  }

  private load(): AuthResponse | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    try {
      const parsed = JSON.parse(raw) as AuthResponse;
      // Defensive: old payloads (saved before the matrix rewrite) carried a different
      // shape. Force a refresh by clearing — interceptor will re-issue via /refresh.
      if (parsed?.user && !Array.isArray((parsed.user as unknown as { permissions?: unknown }).permissions)) {
        return null;
      }
      return parsed;
    } catch { return null; }
  }

  seedFakeAuth(): void {
    this.setAuth(this.buildDummy());
  }

  private buildDummy(): AuthResponse {
    const inOneDay = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString();
    return {
      accessToken: 'dev-bypass-token',
      refreshToken: 'dev-bypass-refresh',
      accessTokenExpiresAtUtc: inOneDay,
      user: {
        id: '00000000-0000-0000-0000-000000000001',
        email: 'admin@tracker.local',
        fullName: 'Tracker Admin',
        roles: ['Admin'],
        isSystemAdmin: true,
        isPlatformAdmin: true,
        permissions: [],
        lockedPlantId: null,
        currentPlantId: '00000000-0000-0000-0000-000000000002'
      },
      tenant: {
        id: '00000000-0000-0000-0000-000000000001',
        name: 'Demo Workspace',
        slug: 'demo'
      }
    };
  }
}
