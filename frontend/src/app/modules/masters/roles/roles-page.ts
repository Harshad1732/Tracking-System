import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { CheckboxModule } from 'primeng/checkbox';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { SkeletonModule } from 'primeng/skeleton';
import { ConfirmationService, MessageService } from 'primeng/api';
import { RolesService } from '../roles.service';
import { Role, RolePermission } from '../master.types';
import { AuthService } from '../../../auth/auth.service';
import { RESOURCES, ACTIONS } from '../../../auth/auth.types';
import { PageHeaderComponent } from '../../../shared/page-header/page-header';
import { SearchInputComponent } from '../../../shared/search-input/search-input';
import { SkeletonTableComponent } from '../../../shared/skeleton-table/skeleton-table';
import { EmptyStateComponent } from '../../../shared/empty-state/empty-state';
import { RowActionsComponent } from '../../../shared/row-actions/row-actions';
import { FormDialogComponent } from '../../../shared/form-dialog/form-dialog';
import { HasPermDirective } from '../../../shared/has-perm.directive';

@Component({
  selector: 'app-roles-page',
  imports: [
    ReactiveFormsModule, FormsModule,
    PageHeaderComponent, SearchInputComponent, SkeletonTableComponent,
    EmptyStateComponent, RowActionsComponent, FormDialogComponent, HasPermDirective,
    ButtonModule, TableModule, InputTextModule, TextareaModule, CheckboxModule,
    ToggleSwitchModule, TagModule, TooltipModule, SkeletonModule
  ],
  templateUrl: './roles-page.html',
  styleUrl: './roles-page.scss'
})
export class RolesPage implements OnInit {
  protected readonly store = inject(RolesService);
  protected readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly confirm = inject(ConfirmationService);
  private readonly toast = inject(MessageService);

  // Role mutations require Roles.Edit / Roles.Add etc. Editing the matrix in the UI also
  // requires Roles.Edit since it's an update on a role.
  protected readonly canEditRoles = computed(() =>
    this.auth.has(RESOURCES.Roles, ACTIONS.Edit) || this.auth.isSystemAdmin());
  protected readonly canAddRoles = computed(() =>
    this.auth.has(RESOURCES.Roles, ACTIONS.Add) || this.auth.isSystemAdmin());
  protected readonly canDeleteRoles = computed(() =>
    this.auth.has(RESOURCES.Roles, ACTIONS.Delete) || this.auth.isSystemAdmin());

  protected readonly dialogOpen = signal(false);
  protected readonly editing = signal<Role | null>(null);
  protected readonly search = signal('');
  protected readonly skeletonRows = Array.from({ length: 4 });

  // The matrix is held in component state, NOT in a form group — checkbox-per-cell is
  // simpler this way and avoids dynamic FormControl creation per render.
  protected readonly granted = signal<Set<string>>(new Set());
  protected readonly catalog = this.store.catalog;

  protected readonly form: FormGroup = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(60)]],
    description: ['', [Validators.maxLength(250)]],
    isActive: [true]
  });

  protected readonly filtered = computed<Role[]>(() => {
    const q = this.search().trim().toLowerCase();
    const list = this.store.items();
    if (!q) return list;
    return list.filter(r =>
      r.name.toLowerCase().includes(q) ||
      (r.description ?? '').toLowerCase().includes(q)
    );
  });

  /** Compact preview for the table — shows resource counts grouped by action. */
  protected permSummary(r: Role): string {
    if (r.isSystemAdmin) return 'Everything (system admin)';
    if (r.permissions.length === 0) return '— None —';

    const byAction = new Map<string, number>();
    for (const p of r.permissions) {
      byAction.set(p.action, (byAction.get(p.action) ?? 0) + 1);
    }
    const order = [ACTIONS.View, ACTIONS.Add, ACTIONS.Edit, ACTIONS.Delete];
    return order
      .filter(a => byAction.has(a))
      .map(a => `${a} × ${byAction.get(a)}`)
      .join(' · ');
  }

  ngOnInit(): void {
    void this.store.list().catch(err => this.toastError(err, 'Could not load roles.'));
    void this.store.loadCatalog().catch(err => this.toastError(err, 'Could not load permission catalog.'));
  }

  protected openAdd(): void {
    this.editing.set(null);
    this.form.reset({ name: '', description: '', isActive: true });
    this.granted.set(new Set());
    this.dialogOpen.set(true);
  }

  protected openEdit(r: Role): void {
    this.editing.set(r);
    this.form.reset({ name: r.name, description: r.description ?? '', isActive: r.isActive });
    this.granted.set(new Set(r.permissions.map(p => this.key(p.resource, p.action))));
    this.dialogOpen.set(true);
  }

  protected closeDialog(): void { this.dialogOpen.set(false); }

  protected isGranted(resource: string, action: string): boolean {
    return this.granted().has(this.key(resource, action));
  }

  protected toggleCell(resource: string, action: string, checked: boolean): void {
    const next = new Set(this.granted());
    const k = this.key(resource, action);
    if (checked) next.add(k); else next.delete(k);
    this.granted.set(next);
  }

  /** Bulk-select an entire row (all actions for a resource). */
  protected toggleResource(resource: string, on: boolean): void {
    const cat = this.catalog();
    if (!cat) return;
    const next = new Set(this.granted());
    for (const a of cat.actions) {
      const k = this.key(resource, a.code);
      if (on) next.add(k); else next.delete(k);
    }
    this.granted.set(next);
  }

  protected resourceFullySelected(resource: string): boolean {
    const cat = this.catalog();
    if (!cat) return false;
    return cat.actions.every(a => this.granted().has(this.key(resource, a.code)));
  }

  protected resourcePartiallySelected(resource: string): boolean {
    const cat = this.catalog();
    if (!cat) return false;
    const hits = cat.actions.filter(a => this.granted().has(this.key(resource, a.code))).length;
    return hits > 0 && hits < cat.actions.length;
  }

  /** Bulk-select an entire column (one action across every resource). */
  protected toggleAction(action: string, on: boolean): void {
    const cat = this.catalog();
    if (!cat) return;
    const next = new Set(this.granted());
    for (const r of cat.resources) {
      const k = this.key(r.code, action);
      if (on) next.add(k); else next.delete(k);
    }
    this.granted.set(next);
  }

  protected actionFullySelected(action: string): boolean {
    const cat = this.catalog();
    if (!cat) return false;
    return cat.resources.every(r => this.granted().has(this.key(r.code, action)));
  }

  protected get isEditingSystemAdmin(): boolean {
    return this.editing()?.isSystemAdmin === true;
  }
  protected get isEditingSystem(): boolean {
    return this.editing()?.isSystem === true;
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const permissions: RolePermission[] = Array.from(this.granted()).map(k => {
      const [resource, action] = k.split('|');
      return { resource, action };
    });
    const input = {
      name: v.name,
      description: v.description || null,
      isActive: v.isActive,
      permissions
    };
    const current = this.editing();
    try {
      if (current) {
        await this.store.update(current.id, input);
        this.toast.add({ severity: 'success', summary: 'Role updated', detail: `${v.name} has been saved.`, life: 2500 });
      } else {
        await this.store.create(input);
        this.toast.add({ severity: 'success', summary: 'Role added', detail: `${v.name} has been added.`, life: 2500 });
      }
      this.dialogOpen.set(false);
    } catch (err) {
      this.toastError(err, 'Could not save role.');
    }
  }

  protected onDelete(r: Role): void {
    if (r.isSystem) {
      this.toast.add({ severity: 'warn', summary: 'Built-in role', detail: 'Built-in roles cannot be deleted.' });
      return;
    }
    this.confirm.confirm({
      header: 'Delete role?',
      message: `“${r.name}” will be permanently removed. This cannot be undone.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Delete', rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-text p-button-secondary',
      accept: async () => {
        try {
          await this.store.remove(r.id);
          this.toast.add({ severity: 'success', summary: 'Role deleted', detail: `${r.name} has been removed.`, life: 2500 });
        } catch (err) {
          this.toastError(err, 'Could not delete role.');
        }
      }
    });
  }

  protected clearSearch(): void { this.search.set(''); }

  protected hasError(control: string, error: string): boolean {
    const c = this.form.get(control);
    return !!c && c.touched && c.hasError(error);
  }

  private key(resource: string, action: string): string { return `${resource}|${action}`; }

  private toastError(err: unknown, fallback: string): void {
    const msg = err instanceof HttpErrorResponse
      ? (err.error?.error ?? err.message ?? fallback)
      : fallback;
    this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
  }
}
