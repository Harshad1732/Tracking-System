import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Plan } from './billing.types';

@Injectable({ providedIn: 'root' })
export class PlansService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiBaseUrl}/plans`;

  private readonly _items = signal<Plan[]>([]);
  private readonly _loading = signal(false);

  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();

  async list(): Promise<void> {
    this._loading.set(true);
    try {
      const data = await firstValueFrom(this.http.get<Plan[]>(this.api));
      this._items.set(data);
    } finally {
      this._loading.set(false);
    }
  }
}
