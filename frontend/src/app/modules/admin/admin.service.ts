import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminUser, CreateUserInput, UpdateUserInput, Workspace } from './admin.types';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiBaseUrl;

  private readonly _users = signal<AdminUser[]>([]);
  private readonly _usersLoading = signal(false);
  private readonly _saving = signal(false);
  private readonly _workspace = signal<Workspace | null>(null);
  private readonly _workspaceLoading = signal(false);

  readonly users = this._users.asReadonly();
  readonly usersLoading = this._usersLoading.asReadonly();
  readonly saving = this._saving.asReadonly();
  readonly workspace = this._workspace.asReadonly();
  readonly workspaceLoading = this._workspaceLoading.asReadonly();

  async listUsers(): Promise<void> {
    this._usersLoading.set(true);
    try {
      const data = await firstValueFrom(this.http.get<AdminUser[]>(`${this.api}/users`));
      this._users.set(data);
    } finally {
      this._usersLoading.set(false);
    }
  }

  async createUser(input: CreateUserInput): Promise<AdminUser> {
    this._saving.set(true);
    try {
      const created = await firstValueFrom(this.http.post<AdminUser>(`${this.api}/users`, input));
      this._users.update(list => [created, ...list]);
      return created;
    } finally {
      this._saving.set(false);
    }
  }

  async updateUser(id: string, input: UpdateUserInput): Promise<AdminUser> {
    this._saving.set(true);
    try {
      const updated = await firstValueFrom(this.http.put<AdminUser>(`${this.api}/users/${id}`, input));
      this._users.update(list => list.map(u => u.id === id ? updated : u));
      return updated;
    } finally {
      this._saving.set(false);
    }
  }

  async resetUserPassword(id: string, newPassword: string): Promise<void> {
    this._saving.set(true);
    try {
      await firstValueFrom(this.http.post<void>(`${this.api}/users/${id}/reset-password`, { newPassword }));
    } finally {
      this._saving.set(false);
    }
  }

  async deleteUser(id: string): Promise<void> {
    this._saving.set(true);
    try {
      await firstValueFrom(this.http.delete<void>(`${this.api}/users/${id}`));
      this._users.update(list => list.filter(u => u.id !== id));
    } finally {
      this._saving.set(false);
    }
  }

  async loadWorkspace(): Promise<void> {
    this._workspaceLoading.set(true);
    try {
      const data = await firstValueFrom(this.http.get<Workspace>(`${this.api}/workspace`));
      this._workspace.set(data);
    } finally {
      this._workspaceLoading.set(false);
    }
  }

  async updateWorkspace(name: string): Promise<Workspace> {
    this._saving.set(true);
    try {
      const updated = await firstValueFrom(this.http.put<Workspace>(`${this.api}/workspace`, { name }));
      this._workspace.set(updated);
      return updated;
    } finally {
      this._saving.set(false);
    }
  }
}
