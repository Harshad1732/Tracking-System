import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { GlassSheet, SheetBulkResponse, SheetCreateInput, SheetListFilters, SheetMovement, SheetReplaceInput } from '../masters/master.types';

@Injectable({ providedIn: 'root' })
export class SheetsService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiBaseUrl}/sheets`;

  private readonly _items = signal<GlassSheet[]>([]);
  private readonly _loading = signal(false);
  private readonly _saving = signal(false);

  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly saving = this._saving.asReadonly();

  async listByShopfloor(shopfloorId: string): Promise<void> {
    await this.list({ shopfloorId });
  }

  async list(filters: SheetListFilters = {}): Promise<void> {
    this._loading.set(true);
    try {
      let params = new HttpParams();
      if (filters.shopfloorId) params = params.set('shopfloorId', filters.shopfloorId);
      if (filters.status) params = params.set('status', filters.status);
      if (filters.customerId) params = params.set('customerId', filters.customerId);
      if (filters.isStorage !== undefined) params = params.set('isStorage', String(filters.isStorage));
      const data = await firstValueFrom(this.http.get<GlassSheet[]>(this.api, { params }));
      this._items.set(data);
    } finally {
      this._loading.set(false);
    }
  }

  async create(input: SheetCreateInput): Promise<GlassSheet> {
    this._saving.set(true);
    try {
      const created = await firstValueFrom(this.http.post<GlassSheet>(this.api, input));
      return created;
    } finally {
      this._saving.set(false);
    }
  }

  async bulkCreate(sheets: SheetCreateInput[]): Promise<SheetBulkResponse> {
    this._saving.set(true);
    try {
      return await firstValueFrom(
        this.http.post<SheetBulkResponse>(`${this.api}/bulk`, { sheets })
      );
    } finally {
      this._saving.set(false);
    }
  }

  async move(sheetIds: string[], toShopfloorId: string, remarks: string | null, createBatch = false): Promise<number> {
    this._saving.set(true);
    try {
      const count = await firstValueFrom(
        this.http.post<number>(`${this.api}/move`, { sheetIds, toShopfloorId, remarks, createBatch })
      );
      // Drop moved sheets from current view immediately
      this._items.update(list => list.filter(s => !sheetIds.includes(s.id)));
      return count;
    } finally {
      this._saving.set(false);
    }
  }

  async setStatus(sheetIds: string[], status: string, remarks: string | null): Promise<number> {
    this._saving.set(true);
    try {
      const count = await firstValueFrom(
        this.http.post<number>(`${this.api}/status`, { sheetIds, status, remarks })
      );
      const ids = new Set(sheetIds);
      this._items.update(list => list.map(s =>
        ids.has(s.id) ? { ...s, status, lastMovedAtUtc: new Date().toISOString() } : s
      ));
      return count;
    } finally {
      this._saving.set(false);
    }
  }

  async remove(id: string): Promise<void> {
    this._saving.set(true);
    try {
      await firstValueFrom(this.http.delete<void>(`${this.api}/${id}`));
      this._items.update(list => list.filter(s => s.id !== id));
    } finally {
      this._saving.set(false);
    }
  }

  movements(sheetId: string): Promise<SheetMovement[]> {
    return firstValueFrom(this.http.get<SheetMovement[]>(`${this.api}/${sheetId}/movements`));
  }

  async replace(sheetId: string, input: SheetReplaceInput): Promise<GlassSheet> {
    this._saving.set(true);
    try {
      // The new sheet starts in Storage so it doesn't appear in this floor's view.
      // Callers refresh the list themselves if they want it to show somewhere else.
      return await firstValueFrom(this.http.post<GlassSheet>(`${this.api}/${sheetId}/replace`, input));
    } finally {
      this._saving.set(false);
    }
  }
}
