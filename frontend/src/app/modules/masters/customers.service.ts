import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { CrudStore } from './crud-store';
import { Customer, CustomerInput } from './master.types';

@Injectable({ providedIn: 'root' })
export class CustomersService extends CrudStore<Customer, CustomerInput> {
  constructor() {
    super(inject(HttpClient), 'customers');
  }

  readonly customers = this.items;
  readonly activeCount = computed(() => this.items().filter(c => c.isActive).length);
}
