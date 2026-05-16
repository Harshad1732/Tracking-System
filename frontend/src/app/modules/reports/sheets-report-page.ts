import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';
import { downloadAuthorized } from '../../shared/downloads';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { SkeletonModule } from 'primeng/skeleton';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { SheetsService } from '../tracking/sheets.service';
import { ShopfloorsService } from '../masters/shopfloors.service';
import { CustomersService } from '../masters/customers.service';
import { GlassSheet } from '../masters/master.types';

export interface SheetsReportConfig {
  title: string;
  description: string;
  icon: string;
  crumbLabel: string;
  apiStatus?: string;
  apiIsStorage?: boolean;
  excludeStatuses?: string[];
}

@Component({
  selector: 'app-sheets-report-page',
  imports: [
    DatePipe, FormsModule, RouterLink,
    ButtonModule, TableModule, SelectModule, TagModule, TooltipModule,
    SkeletonModule, IconFieldModule, InputIconModule, InputTextModule
  ],
  templateUrl: './sheets-report-page.html',
  styleUrl: './sheets-report-page.scss'
})
export class SheetsReportPage implements OnInit, OnDestroy {
  protected readonly sheets = inject(SheetsService);
  protected readonly shopfloors = inject(ShopfloorsService);
  protected readonly customers = inject(CustomersService);
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(MessageService);

  protected readonly exporting = signal(false);

  protected readonly config = signal<SheetsReportConfig>({
    title: 'Report', description: '', icon: 'pi pi-th-large', crumbLabel: 'Report'
  });

  protected readonly search = signal('');
  protected readonly shopfloorFilter = signal<string | null>(null);
  protected readonly customerFilter = signal<string | null>(null);
  protected readonly skeletonRows = Array.from({ length: 6 });

  protected readonly shopfloorOptions = computed(() => [
    { label: 'All shopfloors', value: null },
    ...this.shopfloors.items().map(s => ({ label: s.name, value: s.id }))
  ]);

  protected readonly customerOptions = computed(() => [
    { label: 'All customers', value: null },
    ...this.customers.items().map(c => ({ label: c.name, value: c.id }))
  ]);

  protected readonly filtered = computed<GlassSheet[]>(() => {
    const exclude = new Set(this.config().excludeStatuses ?? []);
    const sfId = this.shopfloorFilter();
    const cId = this.customerFilter();
    const q = this.search().trim().toLowerCase();
    return this.sheets.items().filter(s => {
      if (exclude.has(s.status)) return false;
      if (sfId && s.currentShopfloorId !== sfId) return false;
      if (cId && s.customerId !== cId) return false;
      if (q) {
        const hay = [s.sheetNo, s.orderNo, s.customerName, s.glassType, s.currentShopfloorName]
          .filter(Boolean).map(v => v!.toLowerCase());
        if (!hay.some(h => h.includes(q))) return false;
      }
      return true;
    });
  });

  protected readonly displayCount = computed(() => this.filtered().length);
  protected readonly totalCount = computed(() => this.sheets.items().length);

  private routeSub?: Subscription;

  ngOnInit(): void {
    this.routeSub = this.route.data.subscribe(data => {
      const cfg = (data as { report?: SheetsReportConfig }).report;
      if (cfg) {
        this.config.set(cfg);
        this.reload();
      }
    });
    void this.shopfloors.list().catch(() => {});
    void this.customers.list().catch(() => {});
  }

  ngOnDestroy(): void { this.routeSub?.unsubscribe(); }

  protected reload(): void {
    const cfg = this.config();
    void this.sheets.list({
      status: cfg.apiStatus,
      isStorage: cfg.apiIsStorage
    }).catch(err => this.toastError(err, 'Could not load report.'));
  }

  protected clearFilters(): void {
    this.search.set('');
    this.shopfloorFilter.set(null);
    this.customerFilter.set(null);
  }

  protected async exportCsv(): Promise<void> {
    const cfg = this.config();
    this.exporting.set(true);
    try {
      await downloadAuthorized(this.http, `${environment.apiBaseUrl}/reports/export/sheets.csv`, {
        status: cfg.apiStatus,
        excludeStatus: cfg.excludeStatuses?.[0],
        isStorage: cfg.apiIsStorage !== undefined ? String(cfg.apiIsStorage) : undefined,
        shopfloorId: this.shopfloorFilter() ?? undefined,
        customerId: this.customerFilter() ?? undefined,
        fileName: cfg.crumbLabel.toLowerCase().replace(/\s+/g, '-')
      }, 'sheets.csv');
      this.toast.add({ severity: 'success', summary: 'Export ready', detail: 'CSV downloaded.', life: 2500 });
    } catch (err) {
      this.toastError(err, 'Could not export.');
    } finally {
      this.exporting.set(false);
    }
  }

  protected daysSince(iso: string): number {
    const diff = Date.now() - new Date(iso).getTime();
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
