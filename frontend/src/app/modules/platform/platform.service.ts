import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse } from '../../auth/auth.types';
import { PlatformTenant } from './platform.types';

@Injectable({ providedIn: 'root' })
export class PlatformService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiBaseUrl;

  readonly tenants = signal<PlatformTenant[]>([]);
  readonly loading = signal(false);

  async list(): Promise<void> {
    this.loading.set(true);
    try {
      const rows = await firstValueFrom(this.http.get<PlatformTenant[]>(`${this.api}/platform/tenants`));
      this.tenants.set(rows);
    } finally {
      this.loading.set(false);
    }
  }

  switch(tenantId: string): Promise<AuthResponse> {
    return firstValueFrom(this.http.post<AuthResponse>(`${this.api}/platform/switch/${tenantId}`, {}));
  }

  setActive(tenantId: string, isActive: boolean): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.api}/platform/tenants/${tenantId}/active`, { isActive }));
  }
}
