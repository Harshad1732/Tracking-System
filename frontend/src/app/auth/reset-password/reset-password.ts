import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { PasswordModule } from 'primeng/password';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-reset-password',
  imports: [FormsModule, RouterLink, ButtonModule, CardModule, PasswordModule],
  templateUrl: './reset-password.html',
  styleUrls: ['../auth-shell.scss']
})
export class ResetPassword implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  token = '';
  password = '';
  submitting = signal(false);
  message = signal<string | null>(null);
  error = signal<string | null>(null);

  ngOnInit() {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!this.token) this.error.set('Missing reset token. Use the link from your email.');
  }

  submit() {
    if (!this.token || !this.password) return;
    this.error.set(null);
    this.message.set(null);
    this.submitting.set(true);
    this.auth.resetPassword(this.token, this.password).subscribe({
      next: (r) => {
        this.message.set(`${r.message} You can now log in.`);
        this.submitting.set(false);
        setTimeout(() => this.router.navigateByUrl('/login'), 1500);
      },
      error: (e: HttpErrorResponse) => {
        this.error.set(e.error?.error ?? 'Reset failed.');
        this.submitting.set(false);
      }
    });
  }
}
