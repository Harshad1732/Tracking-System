import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-forgot-password',
  imports: [FormsModule, RouterLink, ButtonModule, CardModule, InputTextModule],
  templateUrl: './forgot-password.html',
  styleUrls: ['../auth-shell.scss']
})
export class ForgotPassword {
  private readonly auth = inject(AuthService);

  tenantSlug = '';
  email = '';
  submitting = signal(false);
  message = signal<string | null>(null);
  error = signal<string | null>(null);

  submit() {
    if (!this.tenantSlug || !this.email) return;
    this.error.set(null);
    this.message.set(null);
    this.submitting.set(true);
    this.auth.forgotPassword(this.tenantSlug, this.email).subscribe({
      next: (r) => {
        this.message.set(r.message);
        this.submitting.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.error.set(e.error?.error ?? 'Something went wrong.');
        this.submitting.set(false);
      }
    });
  }
}
