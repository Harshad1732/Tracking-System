import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Subscription } from './billing.types';

@Injectable({ providedIn: 'root' })
export class SubscriptionService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiBaseUrl}/subscription`;

  private readonly _current = signal<Subscription | null>(null);
  private readonly _loading = signal(false);
  private readonly _saving = signal(false);

  readonly current = this._current.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly saving = this._saving.asReadonly();

  async load(): Promise<void> {
    this._loading.set(true);
    try {
      const sub = await firstValueFrom(this.http.get<Subscription>(`${this.api}/me`));
      this._current.set(sub);
    } finally {
      this._loading.set(false);
    }
  }

  async upgrade(planCode: string): Promise<void> {
    this._saving.set(true);
    try {
      const sub = await firstValueFrom(this.http.post<Subscription>(`${this.api}/upgrade`, { planCode }));
      this._current.set(sub);
    } finally {
      this._saving.set(false);
    }
  }

  async cancel(): Promise<void> {
    this._saving.set(true);
    try {
      const sub = await firstValueFrom(this.http.post<Subscription>(`${this.api}/cancel`, {}));
      this._current.set(sub);
    } finally {
      this._saving.set(false);
    }
  }
}
