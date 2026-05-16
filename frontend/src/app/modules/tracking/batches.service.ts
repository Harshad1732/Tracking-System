import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Batch, BatchCreateInput, BatchMoveInput, BatchStatusInput
} from '../masters/master.types';

@Injectable({ providedIn: 'root' })
export class BatchesService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiBaseUrl}/batches`;

  private readonly _items = signal<Batch[]>([]);
  private readonly _loading = signal(false);
  private readonly _saving = signal(false);

  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly saving = this._saving.asReadonly();

  async listByShopfloor(shopfloorId: string): Promise<void> {
    this._loading.set(true);
    try {
      const params = new HttpParams().set('shopfloorId', shopfloorId);
      const data = await firstValueFrom(this.http.get<Batch[]>(this.api, { params }));
      this._items.set(data);
    } finally {
      this._loading.set(false);
    }
  }

  async create(input: BatchCreateInput): Promise<Batch> {
    this._saving.set(true);
    try {
      const created = await firstValueFrom(this.http.post<Batch>(this.api, input));
      this._items.update(list => [created, ...list]);
      return created;
    } finally {
      this._saving.set(false);
    }
  }

  async move(input: BatchMoveInput): Promise<number> {
    this._saving.set(true);
    try {
      const count = await firstValueFrom(this.http.post<number>(`${this.api}/move`, input));
      const removed = new Set(input.batchIds);
      this._items.update(list => list.filter(b => !removed.has(b.id)));
      return count;
    } finally {
      this._saving.set(false);
    }
  }

  async setStatus(input: BatchStatusInput): Promise<number> {
    this._saving.set(true);
    try {
      const count = await firstValueFrom(this.http.post<number>(`${this.api}/status`, input));
      const ids = new Set(input.batchIds);
      this._items.update(list => list.map(b =>
        ids.has(b.id) ? { ...b, status: input.status } : b
      ));
      return count;
    } finally {
      this._saving.set(false);
    }
  }

  async dissolve(id: string): Promise<void> {
    this._saving.set(true);
    try {
      await firstValueFrom(this.http.delete<void>(`${this.api}/${id}/dissolve`));
      this._items.update(list => list.filter(b => b.id !== id));
    } finally {
      this._saving.set(false);
    }
  }
}
