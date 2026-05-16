import { Component, OnInit, computed, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { SkeletonModule } from 'primeng/skeleton';
import { TooltipModule } from 'primeng/tooltip';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';
import { ShopfloorsService } from '../masters/shopfloors.service';
import { DashboardStatsService } from '../reports/dashboard-stats.service';
import { floorTone, FloorTone } from '../masters/floor-tones';
import { Shopfloor } from '../masters/master.types';

interface FlowStop {
  floor: Shopfloor;
  count: number;
  pct: number;
  tone: FloorTone;
}

@Component({
  selector: 'app-shopfloor-workspace',
  imports: [DecimalPipe, RouterLink, ButtonModule, SkeletonModule, TooltipModule, TagModule],
  templateUrl: './shopfloor-workspace.html',
  styleUrl: './shopfloor-workspace.scss'
})
export class ShopfloorWorkspacePage implements OnInit {
  protected readonly shopfloors = inject(ShopfloorsService);
  protected readonly statsService = inject(DashboardStatsService);
  private readonly toast = inject(MessageService);

  protected readonly loading = computed(() =>
    this.shopfloors.loading() || this.statsService.loading()
  );

  protected readonly storage = computed<Shopfloor | null>(() => this.shopfloors.storage());

  protected readonly flow = computed<FlowStop[]>(() => {
    const floors = this.shopfloors.items()
      .filter(s => s.isActive)
      .sort((a, b) => a.sequenceNo - b.sequenceNo);
    const total = floors.reduce((sum, f) => sum + f.sheetCount, 0);
    return floors.map(f => ({
      floor: f,
      count: f.sheetCount,
      pct: total > 0 ? (f.sheetCount / total) * 100 : 0,
      tone: floorTone(f)
    }));
  });

  protected readonly flowTotal = computed(() =>
    this.shopfloors.items().reduce((sum, f) => sum + f.sheetCount, 0)
  );

  protected readonly holdCount = computed(() =>
    this.statsService.stats().byStatus['Hold'] ?? 0
  );

  ngOnInit(): void {
    void this.refresh();
  }

  protected async refresh(): Promise<void> {
    try {
      await Promise.all([this.shopfloors.list(), this.statsService.load()]);
    } catch (err) {
      const msg = err instanceof HttpErrorResponse
        ? (err.error?.error ?? err.message ?? 'Could not load workspace.')
        : 'Could not load workspace.';
      this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
    }
  }
}
