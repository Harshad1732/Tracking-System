import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ToolbarModule } from 'primeng/toolbar';
import { AvatarModule } from 'primeng/avatar';
import { TagModule } from 'primeng/tag';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-dashboard',
  imports: [ButtonModule, CardModule, ToolbarModule, AvatarModule, TagModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected initials(): string {
    const u = this.auth.user();
    if (!u) return '?';
    const base = u.fullName?.trim() || u.email;
    return base.slice(0, 1).toUpperCase();
  }

  logout() {
    this.auth.logout().subscribe({
      next: () => this.router.navigateByUrl('/'),
      error: () => this.router.navigateByUrl('/')
    });
  }
}
