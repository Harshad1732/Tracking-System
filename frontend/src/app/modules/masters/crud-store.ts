import { HttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

interface HasId {
  id: string;
}

export class CrudStore<T extends HasId, TInput> {
  private readonly _items = signal<T[]>([]);
  private readonly _loading = signal(false);
  private readonly _saving = signal(false);

  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly saving = this._saving.asReadonly();

  constructor(
    private readonly http: HttpClient,
    private readonly path: string
  ) {}

  private url(suffix = ''): string {
    return `${environment.apiBaseUrl}/${this.path}${suffix}`;
  }

  async list(): Promise<void> {
    this._loading.set(true);
    try {
      const data = await firstValueFrom(this.http.get<T[]>(this.url()));
      this._items.set(data);
    } finally {
      this._loading.set(false);
    }
  }

  async create(input: TInput): Promise<T> {
    this._saving.set(true);
    try {
      const created = await firstValueFrom(this.http.post<T>(this.url(), input));
      this._items.update(list => [created, ...list]);
      return created;
    } finally {
      this._saving.set(false);
    }
  }

  async update(id: string, input: TInput): Promise<T> {
    this._saving.set(true);
    try {
      const updated = await firstValueFrom(this.http.put<T>(this.url(`/${id}`), input));
      this._items.update(list => list.map(x => (x.id === id ? updated : x)));
      return updated;
    } finally {
      this._saving.set(false);
    }
  }

  async remove(id: string): Promise<void> {
    this._saving.set(true);
    try {
      await firstValueFrom(this.http.delete<void>(this.url(`/${id}`)));
      this._items.update(list => list.filter(x => x.id !== id));
    } finally {
      this._saving.set(false);
    }
  }
}
