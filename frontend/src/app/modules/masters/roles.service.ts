import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CrudStore } from './crud-store';
import { PermissionCatalog, Role, RoleInput } from './master.types';

@Injectable({ providedIn: 'root' })
export class RolesService extends CrudStore<Role, RoleInput> {
  private readonly _api = inject(HttpClient);
  private readonly _catalog = signal<PermissionCatalog | null>(null);

  constructor() {
    super(inject(HttpClient), 'roles');
  }

  readonly roles = this.items;
  readonly activeCount = computed(() => this.items().filter(r => r.isActive).length);
  readonly catalog = this._catalog.asReadonly();

  async loadCatalog(): Promise<void> {
    if (this._catalog()) return; // cached for session
    const data = await firstValueFrom(
      this._api.get<PermissionCatalog>(`${environment.apiBaseUrl}/roles/catalog`));
    this._catalog.set(data);
  }
}
