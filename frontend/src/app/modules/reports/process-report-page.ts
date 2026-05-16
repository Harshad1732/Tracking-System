import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { environment } from '../../../environments/environment';
import { downloadAuthorized } from '../../shared/downloads';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { SkeletonModule } from 'primeng/skeleton';
import { MessageService } from 'primeng/api';
import { ShopfloorsService } from '../masters/shopfloors.service';
import { PageHeaderComponent } from '../../shared/page-header/page-header';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state';

@Component({
  selector: 'app-process-report-page',
  imports: [
    DecimalPipe, RouterLink, ButtonModule, TagModule, TooltipModule, SkeletonModule,
    PageHeaderComponent, EmptyStateComponent
  ],
  templateUrl: './process-report-page.html',
  styleUrl: './process-report-page.scss'
})
export class ProcessReportPage implements OnInit {
  protected readonly shopfloors = inject(ShopfloorsService);
  private readonly http = inject(HttpClient);
  private readonly toast = inject(MessageService);

  protected readonly skeletonRows = Array.from({ length: 5 });
  protected readonly exporting = signal(false);

  protected readonly rows = computed(() => {
    const list = this.shopfloors.items()
      .filter(s => s.isActive)
      .sort((a, b) => a.sequenceNo - b.sequenceNo);
    const total = list.reduce((sum, s) => sum + s.sheetCount, 0);
    return list.map(s => ({
      ...s,
      pct: total > 0 ? (s.sheetCount / total) * 100 : 0,
      pctOfMax: this.maxCount() > 0 ? (s.sheetCount / this.maxCount()) * 100 : 0
    }));
  });

  protected readonly maxCount = computed(() =>
    Math.max(0, ...this.shopfloors.items().map(s => s.sheetCount))
  );

  protected readonly totalSheets = computed(() =>
    this.shopfloors.items().reduce((sum, s) => sum + s.sheetCount, 0)
  );

  ngOnInit(): void {
    void this.reload();
  }

  protected async reload(): Promise<void> {
    try {
      await this.shopfloors.list();
    } catch (err) {
      this.toastError(err, 'Could not load report.');
    }
  }

  protected async exportCsv(): Promise<void> {
    this.exporting.set(true);
    try {
      await downloadAuthorized(this.http, `${environment.apiBaseUrl}/reports/export/process.csv`, {}, 'shopfloor-counts.csv');
      this.toast.add({ severity: 'success', summary: 'Export ready', detail: 'CSV downloaded.', life: 2500 });
    } catch (err) {
      this.toastError(err, 'Could not export.');
    } finally {
      this.exporting.set(false);
    }
  }

  private toastError(err: unknown, fallback: string): void {
    const msg = err instanceof HttpErrorResponse
      ? (err.error?.error ?? err.message ?? fallback)
      : fallback;
    this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
  }
}
