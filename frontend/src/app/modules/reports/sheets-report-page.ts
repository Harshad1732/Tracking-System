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
import { InputNumberModule } from 'primeng/inputnumber';
import { TextareaModule } from 'primeng/textarea';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { SheetsService } from '../tracking/sheets.service';
import { ShopfloorsService } from '../masters/shopfloors.service';
import { CustomersService } from '../masters/customers.service';
import { GlassSheet } from '../masters/master.types';
import { AuthService } from '../../auth/auth.service';
import { PageHeaderComponent } from '../../shared/page-header/page-header';
import { SearchInputComponent } from '../../shared/search-input/search-input';
import { SkeletonTableComponent } from '../../shared/skeleton-table/skeleton-table';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state';
import { SheetStatusesService } from '../../shared/sheet-statuses.service';

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
    PageHeaderComponent, SearchInputComponent, SkeletonTableComponent, EmptyStateComponent,
    ButtonModule, TableModule, SelectModule, TagModule, TooltipModule,
    SkeletonModule, InputTextModule,
    InputNumberModule, TextareaModule, DialogModule
  ],
  templateUrl: './sheets-report-page.html',
  styleUrl: './sheets-report-page.scss'
})
export class SheetsReportPage implements OnInit, OnDestroy {
  protected readonly sheets = inject(SheetsService);
  protected readonly shopfloors = inject(ShopfloorsService);
  protected readonly customers = inject(CustomersService);
  protected readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(MessageService);

  protected readonly exporting = signal(false);

  // Replacement dialog state
  protected readonly replaceOpen = signal(false);
  protected readonly replaceTarget = signal<GlassSheet | null>(null);
  protected readonly replaceForm = signal({ sheetNo: '', reason: '', quantity: null as number | null });
  protected readonly replaceSaving = signal(false);

  /** Whether a sheet can have a replacement issued. Driven by the SheetStatus.IsReplaceable
   *  flag in the DB catalog, NOT by a hardcoded {Hold, Rejected} set. */
  private readonly sheetStatuses = inject(SheetStatusesService);
  protected canReplace(s: GlassSheet): boolean {
    return this.sheetStatuses.canReplace(s.status) && this.auth.canAdd();
  }

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
    void this.sheetStatuses.load().catch(() => {});
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

  protected openReplace(s: GlassSheet): void {
    this.replaceTarget.set(s);
    this.replaceForm.set({ sheetNo: '', reason: '', quantity: s.quantity });
    this.replaceOpen.set(true);
  }

  protected closeReplace(): void {
    this.replaceOpen.set(false);
    this.replaceTarget.set(null);
  }

  protected async submitReplace(): Promise<void> {
    const target = this.replaceTarget();
    const form = this.replaceForm();
    if (!target || !form.reason.trim()) return;

    this.replaceSaving.set(true);
    try {
      const created = await this.sheets.replace(target.id, {
        sheetNo: form.sheetNo.trim() || null,
        reason: form.reason.trim(),
        quantity: form.quantity ?? null
      });
      this.toast.add({
        severity: 'success',
        summary: 'Replacement created',
        detail: `${created.sheetNo} added to Storage. The original (${target.sheetNo}) stays on the current floor.`,
        life: 4000
      });
      this.replaceOpen.set(false);
      this.replaceTarget.set(null);
      // The new sheet starts in Storage, so this report (status=Hold/Rejected) is
      // unchanged — no reload needed. The original still shows here with its history
      // now annotated.
    } catch (err) {
      this.toastError(err, 'Could not create replacement.');
    } finally {
      this.replaceSaving.set(false);
    }
  }

  private toastError(err: unknown, fallback: string): void {
    const msg = err instanceof HttpErrorResponse
      ? (err.error?.error ?? err.message ?? fallback)
      : fallback;
    this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
  }
}
