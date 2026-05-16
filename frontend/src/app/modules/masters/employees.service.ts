import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { CrudStore } from './crud-store';
import { Employee, EmployeeInput } from './master.types';

@Injectable({ providedIn: 'root' })
export class EmployeesService extends CrudStore<Employee, EmployeeInput> {
  constructor() {
    super(inject(HttpClient), 'employees');
  }

  readonly employees = this.items;
  readonly activeCount = computed(() => this.items().filter(e => e.isActive).length);
}
