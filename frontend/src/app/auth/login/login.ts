import { Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { AuthService } from '../auth.service';
import { OAuthService } from '../oauth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink, ButtonModule, CardModule, InputTextModule, PasswordModule],
  templateUrl: './login.html',
  styleUrls: ['../auth-shell.scss']
})
export class Login implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly oauth = inject(OAuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  @ViewChild('googleBtn', { static: false }) googleBtn?: ElementRef<HTMLDivElement>;

  tenantSlug = '';
  email = '';
  password = '';
  submitting = signal(false);
  error = signal<string | null>(null);
  configured = this.oauth.configured;

  ngOnInit() {
    const t = this.route.snapshot.queryParamMap.get('t');
    if (t) this.tenantSlug = t;
    queueMicrotask(() => {
      if (this.googleBtn) {
        this.oauth.renderGoogleButton(this.googleBtn.nativeElement, (idToken) => {
          if (!this.requireTenant()) return;
          this.handleExternal(this.auth.google(this.tenantSlug, idToken));
        });
      }
    });
  }

  submit() {
    if (!this.tenantSlug || !this.email || !this.password) return;
    this.error.set(null);
    this.submitting.set(true);
    this.auth.login(this.tenantSlug, this.email, this.password).subscribe({
      next: () => this.goNext(),
      error: (e: HttpErrorResponse) => {
        this.error.set(e.error?.error ?? 'Login failed.');
        this.submitting.set(false);
      }
    });
  }

  async signInWithMicrosoft() {
    if (!this.requireTenant()) return;
    try {
      this.error.set(null);
      this.submitting.set(true);
      const idToken = await this.oauth.microsoftSignIn();
      if (!idToken) {
        this.error.set('Microsoft sign-in was cancelled.');
        this.submitting.set(false);
        return;
      }
      this.handleExternal(this.auth.microsoft(this.tenantSlug, idToken));
    } catch (e: any) {
      this.error.set(e?.message ?? 'Microsoft sign-in failed.');
      this.submitting.set(false);
    }
  }

  private requireTenant(): boolean {
    if (!this.tenantSlug.trim()) {
      this.error.set('Please enter your workspace slug before continuing.');
      return false;
    }
    return true;
  }

  private handleExternal(obs: ReturnType<AuthService['google']>) {
    obs.subscribe({
      next: () => this.goNext(),
      error: (e: HttpErrorResponse) => {
        this.error.set(e.error?.error ?? 'External sign-in failed.');
        this.submitting.set(false);
      }
    });
  }

  private goNext() {
    // Platform admins skip the per-tenant dashboard entirely and land on the
    // tenant-management page. Any explicit ?returnUrl= query still wins so deep links
    // (e.g. a password-reset email) work for them too.
    const explicitReturn = this.route.snapshot.queryParamMap.get('returnUrl');
    if (explicitReturn) {
      this.router.navigateByUrl(explicitReturn);
      return;
    }
    const home = this.auth.isPlatformAdmin() ? '/platform/tenants' : '/dashboard';
    this.router.navigateByUrl(home);
  }
}
