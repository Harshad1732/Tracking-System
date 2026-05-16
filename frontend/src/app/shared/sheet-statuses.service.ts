import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

/** Shape of a row from GET /api/sheet-statuses — matches backend SheetStatusDto. */
export interface SheetStatusDto {
  id: string;
  code: string;
  name: string;
  sortOrder: number;
  isInitial: boolean;
  isTerminal: boolean;
  isReplaceable: boolean;
  appliesToSheets: boolean;
  appliesToBatches: boolean;
  isSystem: boolean;
  isActive: boolean;
}

/**
 * Catalog of valid sheet/batch statuses, loaded once per session. Replaces every
 * hardcoded ['Pending','InProcess', ...] set in the frontend with a single
 * server-driven source of truth.
 */
@Injectable({ providedIn: 'root' })
export class SheetStatusesService {
  private readonly http = inject(HttpClient);
  private readonly _statuses = signal<SheetStatusDto[]>([]);
  private readonly _loaded = signal(false);

  readonly statuses = this._statuses.asReadonly();
  readonly loaded = this._loaded.asReadonly();

  /** Codes whose `isReplaceable` flag is true. Use in place of the old REPLACEABLE Set. */
  readonly replaceableCodes = computed(() =>
    new Set(this._statuses().filter(s => s.isReplaceable).map(s => s.code)));

  /** Codes whose `isTerminal` flag is true (e.g. "Delivered"). */
  readonly terminalCodes = computed(() =>
    new Set(this._statuses().filter(s => s.isTerminal).map(s => s.code)));

  async load(): Promise<void> {
    if (this._loaded()) return;
    const list = await firstValueFrom(
      this.http.get<SheetStatusDto[]>(`${environment.apiBaseUrl}/sheet-statuses`));
    this._statuses.set(list);
    this._loaded.set(true);
  }

  /** Convenience — true if a status code can have a replacement issued. */
  canReplace(code: string): boolean {
    return this.replaceableCodes().has(code);
  }
}
