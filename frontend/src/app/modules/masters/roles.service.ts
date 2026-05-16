import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { CrudStore } from './crud-store';
import { Role, RoleInput } from './master.types';

@Injectable({ providedIn: 'root' })
export class RolesService extends CrudStore<Role, RoleInput> {
  constructor() {
    super(inject(HttpClient), 'roles');
  }

  readonly roles = this.items;
  readonly activeCount = computed(() => this.items().filter(r => r.isActive).length);
}
