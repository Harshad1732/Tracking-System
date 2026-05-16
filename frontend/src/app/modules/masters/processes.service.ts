import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { CrudStore } from './crud-store';
import { Process, ProcessInput } from './master.types';

@Injectable({ providedIn: 'root' })
export class ProcessesService extends CrudStore<Process, ProcessInput> {
  constructor() {
    super(inject(HttpClient), 'processes');
  }

  readonly processes = this.items;
  readonly activeCount = computed(() => this.items().filter(p => p.isActive).length);
}
