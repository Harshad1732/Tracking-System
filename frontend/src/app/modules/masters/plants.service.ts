import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { CrudStore } from './crud-store';
import { Plant, PlantInput } from './master.types';

@Injectable({ providedIn: 'root' })
export class PlantsService extends CrudStore<Plant, PlantInput> {
  constructor() {
    super(inject(HttpClient), 'plants');
  }

  readonly plants = this.items;
  readonly activeCount = computed(() => this.items().filter(p => p.isActive).length);
}
