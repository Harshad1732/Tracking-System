import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface DashboardFloor {
  id: string;
  code: string;
  name: string;
  sequenceNo: number;
  isStorage: boolean;
  count: number;
}

export interface DashboardStats {
  total: number;
  active: number;
  byStatus: Record<string, number>;
  byShopfloor: DashboardFloor[];
  movementsToday: number;
  sheetsAddedToday: number;
}

const EMPTY: DashboardStats = {
  total: 0, active: 0,
  byStatus: { Pending: 0, InProcess: 0, Completed: 0, Hold: 0, Rejected: 0, Delivered: 0 },
  byShopfloor: [],
  movementsToday: 0,
  sheetsAddedToday: 0
};

@Injectable({ providedIn: 'root' })
export class DashboardStatsService {
  private readonly http = inject(HttpClient);
  private readonly _stats = signal<DashboardStats>(EMPTY);
  private readonly _loading = signal(false);

  readonly stats = this._stats.asReadonly();
  readonly loading = this._loading.asReadonly();

  readonly storage = computed(() =>
    this._stats().byShopfloor.find(f => f.isStorage) ?? null
  );

  async load(): Promise<void> {
    this._loading.set(true);
    try {
      const data = await firstValueFrom(
        this.http.get<DashboardStats>(`${environment.apiBaseUrl}/reports/dashboard-stats`)
      );
      this._stats.set(data);
    } finally {
      this._loading.set(false);
    }
  }
}
