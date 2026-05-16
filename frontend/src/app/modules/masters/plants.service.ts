import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CrudStore } from './crud-store';
import { Plant, PlantInput } from './master.types';
import { AuthResponse } from '../../auth/auth.types';

@Injectable({ providedIn: 'root' })
export class PlantsService extends CrudStore<Plant, PlantInput> {
  private readonly httpClient = inject(HttpClient);

  constructor() {
    super(inject(HttpClient), 'plants');
  }

  readonly plants = this.items;
  readonly activeCount = computed(() => this.items().filter(p => p.isActive).length);

  /** Plants the current user can switch into — backend already applies the lock filter. */
  readonly accessible = signal<Plant[]>([]);

  async loadAccessible(): Promise<void> {
    const rows = await firstValueFrom(
      this.httpClient.get<Plant[]>(`${environment.apiBaseUrl}/plants/mine`));
    this.accessible.set(rows);
  }

  switch(plantId: string): Promise<AuthResponse> {
    return firstValueFrom(
      this.httpClient.post<AuthResponse>(`${environment.apiBaseUrl}/plants/switch/${plantId}`, {}));
  }
}
