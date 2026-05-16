import { Component, OnInit, computed, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { SkeletonModule } from 'primeng/skeleton';
import { TooltipModule } from 'primeng/tooltip';
import { TagModule } from 'primeng/tag';
import { AuthService } from '../auth/auth.service';
import { DashboardStatsService } from '../modules/reports/dashboard-stats.service';
import { floorTone } from '../modules/masters/floor-tones';

interface DonutSlice {
  key: string;
  label: string;
  value: number;
  pct: number;
  tone: 'pending' | 'process' | 'hold' | 'rejected' | 'completed' | 'delivered';
  dasharray: string;
  dashoffset: number;
}

interface AlertRow {
  key: string;
  label: string;
  count: number;
  icon: string;
  tone: 'warn' | 'danger' | 'info' | 'neutral' | 'success';
  link?: string[];
  queryParams?: Record<string, string>;
}

const STATUS_TONES: Record<string, DonutSlice['tone']> = {
  Pending:   'pending',
  InProcess: 'process',
  Hold:      'hold',
  Rejected:  'rejected',
  Completed: 'completed',
  Delivered: 'delivered'
};

// Donut geometry — keep small so it renders cleanly in any column width
const RING_R = 70;
const RING_CIRC = 2 * Math.PI * RING_R; // ≈ 439.82

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, RouterLink, ButtonModule, SkeletonModule, TooltipModule, TagModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {
  protected readonly auth = inject(AuthService);
  protected readonly statsService = inject(DashboardStatsService);

  ngOnInit(): void {
    void this.statsService.load();
  }

  protected refresh(): void {
    void this.statsService.load();
  }

  protected readonly stats = this.statsService.stats;
  protected readonly loading = this.statsService.loading;
  protected readonly today = new Date();

  protected initials(): string {
    const u = this.auth.user();
    if (!u) return '?';
    const base = u.fullName?.trim() || u.email;
    const parts = base.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
    return base.slice(0, 2).toUpperCase();
  }

  protected firstName(): string {
    const u = this.auth.user();
    if (!u) return '';
    const base = u.fullName?.trim();
    if (base) return base.split(/\s+/)[0];
    return u.email.split('@')[0];
  }

  protected readonly kpis = computed(() => {
    const s = this.stats();
    return [
      { key: 'total',     label: 'Total Sheets', value: s.total,                                  tone: 'primary',   icon: 'pi pi-box',          hint: `${s.sheetsAddedToday} added today` },
      { key: 'process',   label: 'On Shopfloor', value: s.active - (s.byStatus['Completed'] ?? 0),tone: 'process',   icon: 'pi pi-spinner',      hint: 'Currently being processed' },
      { key: 'completed', label: 'Completed',    value: s.byStatus['Completed'] ?? 0,             tone: 'completed', icon: 'pi pi-check-circle', hint: 'Ready for dispatch' },
      { key: 'delivered', label: 'Delivered',    value: s.byStatus['Delivered'] ?? 0,             tone: 'delivered', icon: 'pi pi-truck',        hint: `${s.movementsToday} movements today` }
    ] as const;
  });

  protected readonly activeFloors = computed(() => this.stats().byShopfloor.map(f => ({
    ...f,
    tone: floorTone(f)
  })));

  protected readonly liveSheets = computed(() => {
    const s = this.stats();
    return s.active - (s.byStatus['Completed'] ?? 0);
  });

  protected readonly donutSlices = computed<DonutSlice[]>(() => {
    const status = this.stats().byStatus;
    const order: { key: string; label: string }[] = [
      { key: 'InProcess', label: 'In Process' },
      { key: 'Pending',   label: 'Pending' },
      { key: 'Completed', label: 'Completed' },
      { key: 'Delivered', label: 'Delivered' },
      { key: 'Hold',      label: 'On Hold' },
      { key: 'Rejected',  label: 'Rejected' }
    ];
    const values = order.map(o => ({ ...o, value: status[o.key] ?? 0 }));
    const total = values.reduce((sum, v) => sum + v.value, 0);
    if (total === 0) return [];

    let cursor = 0;
    return values
      .filter(v => v.value > 0)
      .map(v => {
        const pct = v.value / total;
        const len = pct * RING_CIRC;
        const slice: DonutSlice = {
          key: v.key,
          label: v.label,
          value: v.value,
          pct: pct * 100,
          tone: STATUS_TONES[v.key],
          dasharray: `${len} ${RING_CIRC - len}`,
          dashoffset: -cursor
        };
        cursor += len;
        return slice;
      });
  });

  protected readonly donutTotal = computed(() =>
    Object.values(this.stats().byStatus).reduce((sum, n) => sum + n, 0)
  );

  protected readonly alerts = computed<AlertRow[]>(() => {
    const s = this.stats();
    return [
      { key: 'hold',      label: 'On hold',           count: s.byStatus['Hold'] ?? 0,      icon: 'pi pi-pause-circle',     tone: 'warn',    link: ['/reports/shopfloor'] },
      { key: 'rejected',  label: 'Rejected',          count: s.byStatus['Rejected'] ?? 0,  icon: 'pi pi-times-circle',     tone: 'danger',  link: ['/reports/shopfloor'] },
      { key: 'completed', label: 'Ready for dispatch',count: s.byStatus['Completed'] ?? 0, icon: 'pi pi-check-circle',     tone: 'success', link: ['/reports/shopfloor'] },
      { key: 'pending',   label: 'Pending start',     count: s.byStatus['Pending'] ?? 0,   icon: 'pi pi-hourglass',        tone: 'info',    link: ['/reports/storage'] },
      { key: 'movements', label: 'Movements today',   count: s.movementsToday,             icon: 'pi pi-arrow-right',      tone: 'neutral', link: ['/reports/daily'] },
      { key: 'added',     label: 'Added today',       count: s.sheetsAddedToday,           icon: 'pi pi-plus-circle',      tone: 'neutral', link: ['/reports/storage'] }
    ];
  });

  protected readonly ringRadius = RING_R;
  protected readonly ringCirc = RING_CIRC;
}
