import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Full plan row including internal flags. Matches backend PlanAdminDto. */
export interface PlatformPlan {
  id: string;
  code: string;
  name: string;
  description: string | null;
  monthlyPriceCents: number;
  currency: string;
  maxSheets: number;
  maxUsers: number;
  maxShopfloors: number;
  retentionDays: number;
  trialDays: number;
  billingIntervalMonths: number;
  isDefaultOnSignup: boolean;
  isActive: boolean;
  sortOrder: number;
  stripePriceId: string | null;
}

@Injectable({ providedIn: 'root' })
export class PlatformPlansService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiBaseUrl;

  readonly plans = signal<PlatformPlan[]>([]);
  readonly loading = signal(false);

  async list(): Promise<void> {
    this.loading.set(true);
    try {
      const rows = await firstValueFrom(this.http.get<PlatformPlan[]>(`${this.api}/plans/admin`));
      this.plans.set(rows);
    } finally {
      this.loading.set(false);
    }
  }
}
