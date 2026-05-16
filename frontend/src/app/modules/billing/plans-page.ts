import { Component, OnInit, computed, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { MessageService } from 'primeng/api';
import { AuthService } from '../../auth/auth.service';
import { PlansService } from './plans.service';
import { SubscriptionService } from './subscription.service';

@Component({
  selector: 'app-plans-page',
  imports: [RouterLink, ButtonModule, TagModule, SkeletonModule],
  templateUrl: './plans-page.html',
  styleUrl: './plans-page.scss'
})
export class PlansPage implements OnInit {
  protected readonly plansService = inject(PlansService);
  protected readonly subService = inject(SubscriptionService);
  protected readonly auth = inject(AuthService);
  private readonly toast = inject(MessageService);
  private readonly router = inject(Router);

  protected readonly upgrading = inject(SubscriptionService).saving;

  protected readonly plans = this.plansService.items;
  protected readonly loading = this.plansService.loading;
  protected readonly currentPlanCode = computed(() => this.subService.current()?.plan.code ?? null);

  ngOnInit(): void {
    void this.plansService.list();
    if (this.auth.isAuthenticated()) void this.subService.load();
  }

  protected priceLabel(p: { monthlyPriceCents: number; currency: string }): string {
    if (p.monthlyPriceCents === 0) return 'Free';
    const dollars = p.monthlyPriceCents / 100;
    return `${p.currency === 'USD' ? '$' : ''}${dollars}`;
  }

  protected formatLimit(n: number): string {
    if (n < 0) return 'Unlimited';
    return n.toLocaleString();
  }

  protected isCurrent(code: string): boolean {
    return this.currentPlanCode() === code;
  }

  protected async select(code: string): Promise<void> {
    if (!this.auth.isAuthenticated()) {
      this.router.navigateByUrl(`/signup?plan=${code}`);
      return;
    }
    if (this.isCurrent(code)) return;
    try {
      await this.subService.upgrade(code);
      this.toast.add({ severity: 'success', summary: 'Plan updated', detail: 'Your new plan is active.', life: 3000 });
      this.router.navigateByUrl('/billing');
    } catch (err) {
      const msg = err instanceof HttpErrorResponse
        ? (err.error?.error ?? err.message ?? 'Could not change plan.')
        : 'Could not change plan.';
      this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
    }
  }
}
