import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { PlatformPlan, PlatformPlansService } from './platform-plans.service';
import { PageHeaderComponent } from '../../shared/page-header/page-header';
import { SearchInputComponent } from '../../shared/search-input/search-input';
import { SkeletonTableComponent } from '../../shared/skeleton-table/skeleton-table';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state';

/**
 * Read-only Plans listing for platform admins. Layer 1 of the Plans-management rollout —
 * see what's seeded, confirm the IsDefaultOnSignup flag points at the right plan, spot
 * misconfigured pricing or retention. Editing comes in Layer 3 once the safety guards
 * (immutable Code, soft-delete, impact preview) ship.
 */
@Component({
  selector: 'app-platform-plans-page',
  standalone: true,
  imports: [
    PageHeaderComponent, SearchInputComponent, SkeletonTableComponent, EmptyStateComponent,
    ButtonModule, TableModule, TagModule, TooltipModule
  ],
  templateUrl: './platform-plans-page.html',
  styleUrl: './platform-plans-page.scss'
})
export class PlatformPlansPage implements OnInit {
  protected readonly store = inject(PlatformPlansService);
  private readonly toast = inject(MessageService);

  protected readonly search = signal('');

  protected readonly filtered = computed<PlatformPlan[]>(() => {
    const q = this.search().trim().toLowerCase();
    const list = this.store.plans();
    if (!q) return list;
    return list.filter(p =>
      p.code.toLowerCase().includes(q) ||
      p.name.toLowerCase().includes(q) ||
      (p.description ?? '').toLowerCase().includes(q));
  });

  ngOnInit(): void {
    void this.store.list().catch(err => this.toast.add({
      severity: 'error', summary: 'Failed', detail: 'Could not load plans.', life: 3500
    }));
  }

  /** Format a price in cents to "$X.XX/mo". Free plans render as "Free". */
  protected priceLabel(p: PlatformPlan): string {
    if (p.monthlyPriceCents === 0) return 'Free';
    const dollars = (p.monthlyPriceCents / 100).toFixed(2);
    const symbol = p.currency === 'USD' ? '$' : p.currency + ' ';
    return `${symbol}${dollars}/mo`;
  }

  protected limitLabel(n: number): string {
    if (n < 0) return 'Unlimited';
    if (n >= 1000) return (n / 1000).toFixed(n % 1000 === 0 ? 0 : 1) + 'k';
    return n.toString();
  }
}
