import { Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { AuthService } from '../auth.service';
import { OAuthService } from '../oauth.service';

@Component({
  selector: 'app-signup',
  imports: [FormsModule, RouterLink, ButtonModule, CardModule, InputTextModule, PasswordModule],
  templateUrl: './signup.html',
  styleUrls: ['../auth-shell.scss']
})
export class Signup implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly oauth = inject(OAuthService);
  private readonly router = inject(Router);

  @ViewChild('googleBtn', { static: false }) googleBtn?: ElementRef<HTMLDivElement>;

  tenantName = '';
  fullName = '';
  email = '';
  password = '';
  submitting = signal(false);
  error = signal<string | null>(null);
  configured = this.oauth.configured;

  ngOnInit() {
    queueMicrotask(() => {
      if (this.googleBtn) {
        this.oauth.renderGoogleButton(this.googleBtn.nativeElement, (idToken) => {
          this.error.set('Google sign-up needs an existing workspace. Use the Login page or create a workspace below first.');
        });
      }
    });
  }

  submit() {
    if (!this.tenantName || !this.email || !this.password) return;
    this.error.set(null);
    this.submitting.set(true);
    this.auth.register({
      email: this.email,
      password: this.password,
      fullName: this.fullName || undefined,
      tenantName: this.tenantName
    }).subscribe({
      next: () => this.router.navigateByUrl('/dashboard'),
      error: (e: HttpErrorResponse) => {
        this.error.set(e.error?.error ?? 'Registration failed.');
        this.submitting.set(false);
      }
    });
  }
}
