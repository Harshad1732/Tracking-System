import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { MessageService } from 'primeng/api';
import { SheetsService } from '../tracking/sheets.service';
import { ShopfloorsService } from '../masters/shopfloors.service';
import { AuthService } from '../../auth/auth.service';
import { GlassSheet, Shopfloor } from '../masters/master.types';
import { environment } from '../../../environments/environment';
import { downloadAuthorized } from '../../shared/downloads';

interface FloorGroup {
  floor: Shopfloor;
  sheets: GlassSheet[];
}

export type DailyReportScope = 'production' | 'storage';

@Component({
  selector: 'app-daily-report-page',
  imports: [DatePipe, ButtonModule, TagModule, SkeletonModule],
  templateUrl: './daily-report-page.html',
  styleUrl: './daily-report-page.scss'
})
export class DailyReportPage implements OnInit, OnDestroy {
  protected readonly sheets = inject(SheetsService);
  protected readonly shopfloors = inject(ShopfloorsService);
  protected readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(MessageService);

  protected readonly scope = signal<DailyReportScope>('production');
  protected readonly now = signal<Date>(new Date());
  protected readonly exporting = signal(false);
  protected readonly skeletonRows = Array.from({ length: 4 });

  protected readonly loading = computed(() => this.sheets.loading() || this.shopfloors.loading());

  protected readonly title = computed(() =>
    this.scope() === 'storage' ? 'Storage Daily Report' : 'Daily Shopfloor Report');

  protected readonly subtitle = computed(() =>
    this.scope() === 'storage'
      ? 'Sheets currently in Storage — waiting to be sent to a production floor.'
      : 'Sheets currently on production floors — excludes Storage and delivered orders.');

  protected readonly summaryLabel = computed(() =>
    this.scope() === 'storage' ? 'Sheets in storage' : 'Sheets on shopfloor');

  protected readonly summaryHint = computed(() =>
    this.scope() === 'storage' ? 'Excludes delivered' : 'Excludes storage and delivered');

  protected readonly activeFloors = computed(() =>
    this.shopfloors.items()
      .filter(s => s.isActive && (this.scope() === 'storage' ? s.isStorage : !s.isStorage))
      .sort((a, b) => a.sequenceNo - b.sequenceNo));

  protected readonly groups = computed<FloorGroup[]>(() => {
    const allowedFloorIds = new Set(this.activeFloors().map(f => f.id));
    const byFloor = new Map<string, GlassSheet[]>();
    for (const s of this.sheets.items()) {
      if (s.status === 'Delivered') continue;
      if (!allowedFloorIds.has(s.currentShopfloorId)) continue;
      const list = byFloor.get(s.currentShopfloorId) ?? [];
      list.push(s);
      byFloor.set(s.currentShopfloorId, list);
    }
    return this.activeFloors().map(f => ({
      floor: f,
      sheets: (byFloor.get(f.id) ?? []).sort((a, b) =>
        a.sheetNo.localeCompare(b.sheetNo, undefined, { numeric: true }))
    }));
  });

  protected readonly totalSheets = computed(() =>
    this.groups().reduce((sum, g) => sum + g.sheets.length, 0));

  private routeSub?: Subscription;

  ngOnInit(): void {
    this.routeSub = this.route.data.subscribe(d => {
      this.scope.set((d['scope'] as DailyReportScope) ?? 'production');
    });
    void this.reload();
  }

  ngOnDestroy(): void { this.routeSub?.unsubscribe(); }

  protected async reload(): Promise<void> {
    this.now.set(new Date());
    try {
      await Promise.all([this.sheets.list(), this.shopfloors.list()]);
    } catch (err) {
      this.toastError(err, 'Could not load report data.');
    }
  }

  protected print(): void {
    this.now.set(new Date());
    setTimeout(() => window.print(), 60);
  }

  protected async exportCsv(): Promise<void> {
    this.exporting.set(true);
    try {
      const params: Record<string, string | undefined> = {
        excludeStatus: 'Delivered',
        fileName: this.scope() === 'storage' ? 'storage-daily' : 'production-daily',
        isStorage: this.scope() === 'storage' ? 'true' : 'false'
      };
      await downloadAuthorized(
        this.http,
        `${environment.apiBaseUrl}/reports/export/sheets.csv`,
        params,
        `${params['fileName']}.csv`
      );
      this.toast.add({ severity: 'success', summary: 'Export ready', detail: 'CSV downloaded.', life: 2500 });
    } catch (err) {
      this.toastError(err, 'Could not export.');
    } finally {
      this.exporting.set(false);
    }
  }

  protected daysSince(iso: string): number {
    const diff = this.now().getTime() - new Date(iso).getTime();
    return Math.max(0, Math.floor(diff / (1000 * 60 * 60 * 24)));
  }

  protected statusSeverity(status: string | null): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (status) {
      case 'Completed': return 'success';
      case 'Delivered': return 'info';
      case 'InProcess': return 'info';
      case 'Hold': return 'warn';
      case 'Rejected': return 'danger';
      default: return 'secondary';
    }
  }

  private toastError(err: unknown, fallback: string): void {
    const msg = err instanceof HttpErrorResponse
      ? (err.error?.error ?? err.message ?? fallback)
      : fallback;
    this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
  }
}
