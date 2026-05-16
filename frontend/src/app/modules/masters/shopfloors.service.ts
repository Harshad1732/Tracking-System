import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { CrudStore } from './crud-store';
import { Shopfloor, ShopfloorInput } from './master.types';

@Injectable({ providedIn: 'root' })
export class ShopfloorsService extends CrudStore<Shopfloor, ShopfloorInput> {
  constructor() {
    super(inject(HttpClient), 'shopfloors');
  }

  readonly shopfloors = this.items;
  readonly activeCount = computed(() => this.items().filter(s => s.isActive).length);
  readonly storage = computed(() => this.items().find(s => s.isStorage && s.isActive) ?? null);

  byCode(code: string): Shopfloor | undefined {
    return this.items().find(s => s.code.toLowerCase() === code.toLowerCase());
  }

  nextAfter(current: Shopfloor): Shopfloor | null {
    const sorted = this.items()
      .filter(s => s.isActive)
      .sort((a, b) => a.sequenceNo - b.sequenceNo);
    const idx = sorted.findIndex(s => s.id === current.id);
    if (idx < 0) return null;
    return sorted[idx + 1] ?? null;
  }
}
