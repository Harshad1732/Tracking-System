import { Component, OnInit, computed, inject } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { ConfirmationService, MessageService } from 'primeng/api';
import { SubscriptionService } from './subscription.service';
import { PageHeaderComponent } from '../../shared/page-header/page-header';

@Component({
  selector: 'app-billing-page',
  imports: [DatePipe, DecimalPipe, RouterLink, ButtonModule, TagModule, SkeletonModule, PageHeaderComponent],
  templateUrl: './billing-page.html',
  styleUrl: './billing-page.scss'
})
export class BillingPage implements OnInit {
  protected readonly subService = inject(SubscriptionService);
  private readonly toast = inject(MessageService);
  private readonly confirm = inject(ConfirmationService);

  protected readonly sub = this.subService.current;
  protected readonly loading = this.subService.loading;

  ngOnInit(): void {
    void this.subService.load();
  }

  protected statusSeverity(status: string | undefined): 'info' | 'success' | 'warn' | 'danger' | 'secondary' {
    switch (status) {
      case 'Trial': return 'info';
      case 'Active': return 'success';
      case 'PastDue': return 'warn';
      case 'Canceled': return 'danger';
      case 'Expired': return 'danger';
      default: return 'secondary';
    }
  }

  protected pct(used: number, limit: number): number {
    if (limit <= 0) return 0;
    return Math.min(100, Math.round((used / limit) * 100));
  }

  protected pctTone(used: number, limit: number): 'ok' | 'warn' | 'danger' {
    const p = this.pct(used, limit);
    if (p >= 95) return 'danger';
    if (p >= 80) return 'warn';
    return 'ok';
  }

  protected priceLabel(): string {
    const p = this.sub()?.plan;
    if (!p) return '';
    if (p.monthlyPriceCents === 0) return 'Free';
    return `${p.currency === 'USD' ? '$' : ''}${p.monthlyPriceCents / 100} / month`;
  }

  protected formatLimit(n: number | undefined): string {
    if (n === undefined) return '—';
    if (n < 0) return 'Unlimited';
    return n.toLocaleString();
  }

  protected trialDaysLeft = computed(() => {
    const t = this.sub()?.trialEndsAtUtc;
    if (!t) return 0;
    const diff = new Date(t).getTime() - Date.now();
    return Math.max(0, Math.ceil(diff / (1000 * 60 * 60 * 24)));
  });

  protected async cancel(): Promise<void> {
    this.confirm.confirm({
      header: 'Cancel subscription?',
      message: 'Your workspace stays available but moves to read-only at the end of the current period.',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Cancel subscription',
      rejectLabel: 'Keep subscription',
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-text p-button-secondary',
      accept: async () => {
        try {
          await this.subService.cancel();
          this.toast.add({ severity: 'success', summary: 'Subscription canceled', detail: 'You can re-subscribe any time from the Plans page.', life: 3500 });
        } catch (err) {
          const msg = err instanceof HttpErrorResponse
            ? (err.error?.error ?? err.message ?? 'Could not cancel.')
            : 'Could not cancel.';
          this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
        }
      }
    });
  }
}
