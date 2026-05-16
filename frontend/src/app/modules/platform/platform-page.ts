import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { TooltipModule } from 'primeng/tooltip';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ConfirmationService, MessageService } from 'primeng/api';
import { PlatformService } from './platform.service';
import { PlatformTenant } from './platform.types';
import { AuthService } from '../../auth/auth.service';
import { PageHeaderComponent } from '../../shared/page-header/page-header';
import { SearchInputComponent } from '../../shared/search-input/search-input';
import { SkeletonTableComponent } from '../../shared/skeleton-table/skeleton-table';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state';

@Component({
  selector: 'app-platform-page',
  imports: [
    DatePipe,
    PageHeaderComponent, SearchInputComponent, SkeletonTableComponent, EmptyStateComponent,
    ButtonModule, TableModule, TagModule, TooltipModule,
    ToggleSwitchModule
  ],
  templateUrl: './platform-page.html',
  styleUrl: './platform-page.scss'
})
export class PlatformPage implements OnInit {
  protected readonly store = inject(PlatformService);
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(MessageService);
  private readonly confirm = inject(ConfirmationService);

  protected readonly search = signal('');
  protected readonly skeletonRows = Array.from({ length: 4 });
  protected readonly switching = signal<string | null>(null);

  protected readonly filtered = computed<PlatformTenant[]>(() => {
    const q = this.search().trim().toLowerCase();
    const list = this.store.tenants();
    if (!q) return list;
    return list.filter(t =>
      t.name.toLowerCase().includes(q) ||
      t.slug.toLowerCase().includes(q));
  });

  protected readonly currentTenantId = computed(() => this.auth.tenant()?.id);

  ngOnInit(): void {
    void this.store.list().catch(err => this.toastError(err, 'Could not load tenants.'));
  }

  protected async switchTo(t: PlatformTenant): Promise<void> {
    if (t.id === this.currentTenantId()) {
      this.toast.add({ severity: 'info', summary: 'Already here', detail: `You're already in ${t.name}.`, life: 2500 });
      return;
    }
    this.switching.set(t.id);
    try {
      const resp = await this.store.switch(t.id);
      this.auth.setAuth(resp);
      // Step out of platform mode — sidebar will now show the regular tenant nav.
      // The user can return to platform mode via the banner at the top of every page.
      this.auth.setViewMode('tenant');
      this.toast.add({
        severity: 'success',
        summary: 'Switched workspace',
        detail: `Now viewing ${t.name}.`,
        life: 2500
      });
      this.router.navigateByUrl('/dashboard');
    } catch (err) {
      this.toastError(err, 'Could not switch tenant.');
    } finally {
      this.switching.set(null);
    }
  }

  protected toggleActive(t: PlatformTenant): void {
    const next = !t.isActive;
    this.confirm.confirm({
      header: next ? 'Re-activate tenant?' : 'Disable tenant?',
      message: next
        ? `“${t.name}” users will be able to sign in again.`
        : `“${t.name}” users will be blocked from signing in until you re-activate.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: next ? 'Activate' : 'Disable',
      rejectLabel: 'Cancel',
      acceptButtonStyleClass: next ? 'p-button-success' : 'p-button-danger',
      rejectButtonStyleClass: 'p-button-text p-button-secondary',
      accept: async () => {
        try {
          await this.store.setActive(t.id, next);
          await this.store.list();
          this.toast.add({
            severity: 'success',
            summary: next ? 'Tenant activated' : 'Tenant disabled',
            detail: t.name,
            life: 2500
          });
        } catch (err) {
          this.toastError(err, 'Could not update tenant status.');
        }
      }
    });
  }

  protected clearSearch(): void { this.search.set(''); }

  protected planSeverity(code: string | null): 'success' | 'info' | 'warn' | 'secondary' {
    switch (code) {
      case 'pro':        return 'success';
      case 'enterprise': return 'success';
      case 'starter':    return 'info';
      case 'free':       return 'warn';
      default:           return 'secondary';
    }
  }

  protected statusSeverity(status: string | null): 'success' | 'info' | 'warn' | 'secondary' | 'danger' {
    switch (status) {
      case 'Active':   return 'success';
      case 'Trial':    return 'info';
      case 'Past Due': return 'warn';
      case 'Canceled': return 'danger';
      default:         return 'secondary';
    }
  }

  private toastError(err: unknown, fallback: string): void {
    const msg = err instanceof HttpErrorResponse
      ? (err.error?.error ?? err.message ?? fallback)
      : fallback;
    this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
  }
}
