import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { SkeletonModule } from 'primeng/skeleton';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AuthService } from '../../auth/auth.service';
import { AdminService } from './admin.service';
import { AdminUser, AdminUserAssignment, AssignmentInput } from './admin.types';
import { RolesService } from '../masters/roles.service';
import { PlantsService } from '../masters/plants.service';
import { PageHeaderComponent } from '../../shared/page-header/page-header';
import { SearchInputComponent } from '../../shared/search-input/search-input';
import { SkeletonTableComponent } from '../../shared/skeleton-table/skeleton-table';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state';
import { HasPermDirective } from '../../shared/has-perm.directive';

type Tab = 'users' | 'workspace';
const SCOPE_TENANT = 'Tenant';
const SCOPE_PLANT = 'Plant';

interface AssignmentRow {
  /** Stable react-tracking key — random for new rows, server id when editing. */
  uiKey: string;
  roleId: string | null;
  scopeType: string;
  scopeId: string | null;
}

@Component({
  selector: 'app-admin-page',
  imports: [
    ReactiveFormsModule, FormsModule, DatePipe,
    PageHeaderComponent, SearchInputComponent, SkeletonTableComponent,
    EmptyStateComponent, HasPermDirective,
    ButtonModule, TableModule, DialogModule, InputTextModule, PasswordModule, SelectModule,
    ToggleSwitchModule, TagModule, TooltipModule, SkeletonModule
  ],
  templateUrl: './admin-page.html',
  styleUrl: './admin-page.scss'
})
export class AdminPage implements OnInit {
  protected readonly store = inject(AdminService);
  protected readonly auth = inject(AuthService);
  protected readonly roles = inject(RolesService);
  protected readonly plants = inject(PlantsService);
  private readonly fb = inject(FormBuilder);
  private readonly confirm = inject(ConfirmationService);
  private readonly toast = inject(MessageService);

  protected readonly tab = signal<Tab>('users');
  protected readonly search = signal('');

  protected readonly dialogOpen = signal(false);
  protected readonly editing = signal<AdminUser | null>(null);
  protected readonly resetOpen = signal(false);
  protected readonly resetTarget = signal<AdminUser | null>(null);

  // Dialog-local assignment editor. Held outside the form group because rows are
  // added/removed dynamically and using FormArray adds a lot of ceremony for a
  // simple list.
  protected readonly assignmentRows = signal<AssignmentRow[]>([]);

  protected readonly roleOptions = computed(() =>
    this.roles.items()
      .filter(r => r.isActive)
      .map(r => ({ label: r.name, value: r.id })));

  protected readonly plantOptions = computed(() => [
    { label: 'All plants (no lock)', value: null },
    ...this.plants.plants().filter(p => p.isActive).map(p => ({ label: p.name, value: p.id }))
  ]);

  protected readonly scopeOptions = computed(() => [
    { label: 'Workspace (all plants)', value: SCOPE_TENANT },
    ...this.plants.plants()
      .filter(p => p.isActive)
      .map(p => ({ label: `Plant: ${p.name}`, value: `${SCOPE_PLANT}:${p.id}` }))
  ]);

  protected readonly form: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    fullName: ['', [Validators.maxLength(120)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    isActive: [true],
    defaultPlantId: [null as string | null]
  });

  protected readonly resetForm: FormGroup = this.fb.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

  protected readonly workspaceForm: FormGroup = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(120)]]
  });

  protected readonly filteredUsers = computed(() => {
    const q = this.search().trim().toLowerCase();
    const list = this.store.users();
    if (!q) return list;
    return list.filter(u =>
      u.email.toLowerCase().includes(q) ||
      (u.fullName ?? '').toLowerCase().includes(q) ||
      u.assignments.some(a => a.roleName.toLowerCase().includes(q)));
  });

  protected readonly isSelf = (u: AdminUser): boolean =>
    u.id === this.auth.user()?.id;

  protected readonly skeletonRows = Array.from({ length: 5 });

  ngOnInit(): void {
    void this.store.listUsers().catch(err => this.toastError(err, 'Could not load users.'));
    void this.roles.list().catch(() => {});
    void this.plants.list().catch(() => {});
    void this.store.loadWorkspace().then(() => {
      const ws = this.store.workspace();
      if (ws) this.workspaceForm.patchValue({ name: ws.name });
    }).catch(() => {});
  }

  protected setTab(t: Tab): void { this.tab.set(t); }

  // ============== USERS ==============

  protected openAdd(): void {
    this.editing.set(null);
    this.form.reset({ email: '', fullName: '', password: '', isActive: true, defaultPlantId: null });
    this.form.get('password')?.setValidators([Validators.required, Validators.minLength(8)]);
    this.form.get('password')?.updateValueAndValidity();
    this.form.get('email')?.enable();
    // Start with one empty assignment row to nudge the user into picking a role.
    this.assignmentRows.set([this.makeRow()]);
    this.dialogOpen.set(true);
  }

  protected openEdit(u: AdminUser): void {
    this.editing.set(u);
    this.form.reset({
      email: u.email, fullName: u.fullName ?? '',
      password: '', isActive: u.isActive,
      defaultPlantId: u.defaultPlantId
    });
    this.form.get('email')?.disable();
    this.form.get('password')?.clearValidators();
    this.form.get('password')?.updateValueAndValidity();
    this.assignmentRows.set(u.assignments.map(a => ({
      uiKey: a.id,
      roleId: a.roleId,
      scopeType: a.scopeType,
      scopeId: a.scopeId
    })));
    this.dialogOpen.set(true);
  }

  protected addAssignmentRow(): void {
    this.assignmentRows.update(rows => [...rows, this.makeRow()]);
  }

  protected removeAssignmentRow(uiKey: string): void {
    this.assignmentRows.update(rows => rows.filter(r => r.uiKey !== uiKey));
  }

  protected updateAssignmentRole(uiKey: string, roleId: string | null): void {
    this.assignmentRows.update(rows =>
      rows.map(r => r.uiKey === uiKey ? { ...r, roleId } : r));
  }

  protected scopeValue(row: AssignmentRow): string {
    return row.scopeType === SCOPE_TENANT
      ? SCOPE_TENANT
      : `${SCOPE_PLANT}:${row.scopeId}`;
  }

  protected updateAssignmentScope(uiKey: string, value: string): void {
    this.assignmentRows.update(rows =>
      rows.map(r => {
        if (r.uiKey !== uiKey) return r;
        if (value === SCOPE_TENANT) {
          return { ...r, scopeType: SCOPE_TENANT, scopeId: null };
        }
        // value format: "Plant:<guid>"
        const [type, id] = value.split(':');
        return { ...r, scopeType: type, scopeId: id };
      }));
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    // Strip incomplete rows. The backend will reject duplicates / unknown scopes — we
    // just keep the payload clean.
    const assignments: AssignmentInput[] = this.assignmentRows()
      .filter(r => r.roleId)
      .map(r => ({
        roleId: r.roleId!,
        scopeType: r.scopeType,
        scopeId: r.scopeType === SCOPE_TENANT ? null : r.scopeId
      }));

    const v = this.form.getRawValue();
    const current = this.editing();
    try {
      if (current) {
        await this.store.updateUser(current.id, {
          fullName: v.fullName || null,
          isActive: v.isActive,
          defaultPlantId: v.defaultPlantId ?? null,
          assignments
        });
        this.toast.add({ severity: 'success', summary: 'User updated', detail: v.email, life: 2500 });
      } else {
        await this.store.createUser({
          email: (v.email as string).trim().toLowerCase(),
          fullName: v.fullName || null,
          password: v.password,
          defaultPlantId: v.defaultPlantId ?? null,
          assignments
        });
        this.toast.add({ severity: 'success', summary: 'User created',
          detail: v.email + ' added. Share the temporary password.', life: 4000 });
      }
      this.dialogOpen.set(false);
    } catch (err) {
      this.toastError(err, 'Could not save user.');
    }
  }

  protected openReset(u: AdminUser): void {
    this.resetTarget.set(u);
    this.resetForm.reset({ newPassword: '' });
    this.resetOpen.set(true);
  }

  protected async submitReset(): Promise<void> {
    if (this.resetForm.invalid) { this.resetForm.markAllAsTouched(); return; }
    const u = this.resetTarget();
    if (!u) return;
    try {
      await this.store.resetUserPassword(u.id, this.resetForm.value.newPassword);
      this.toast.add({ severity: 'success', summary: 'Password reset',
        detail: `Share the new password with ${u.email}. They've been signed out.`, life: 4500 });
      this.resetOpen.set(false);
    } catch (err) {
      this.toastError(err, 'Could not reset password.');
    }
  }

  protected delete(u: AdminUser): void {
    if (this.isSelf(u)) {
      this.toast.add({ severity: 'warn', summary: 'Not allowed', detail: "You can't delete your own account." });
      return;
    }
    this.confirm.confirm({
      header: 'Delete user?',
      message: `${u.email} will lose access permanently. Their activity history stays in the audit trail.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Delete', rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-text p-button-secondary',
      accept: async () => {
        try {
          await this.store.deleteUser(u.id);
          this.toast.add({ severity: 'success', summary: 'User deleted', detail: u.email, life: 2500 });
        } catch (err) {
          this.toastError(err, 'Could not delete user.');
        }
      }
    });
  }

  /** Compact label for a user's assignments — used in the table. */
  protected assignmentSummary(u: AdminUser): string {
    if (u.assignments.length === 0) return '— No roles —';
    const labels = u.assignments.map(a =>
      a.scopeType === SCOPE_TENANT
        ? a.roleName
        : `${a.roleName} @ ${a.scopeName ?? 'plant'}`);
    return labels.join(', ');
  }

  protected roleSeverity(a: AdminUserAssignment): 'info' | 'success' | 'warn' | 'danger' | 'secondary' {
    if (a.isSystemAdmin) return 'danger';
    return 'info';
  }

  // ============== WORKSPACE ==============

  protected async saveWorkspace(): Promise<void> {
    if (this.workspaceForm.invalid) { this.workspaceForm.markAllAsTouched(); return; }
    try {
      await this.store.updateWorkspace(this.workspaceForm.value.name);
      this.toast.add({ severity: 'success', summary: 'Workspace saved', detail: 'Name updated.', life: 2500 });
    } catch (err) {
      this.toastError(err, 'Could not save workspace.');
    }
  }

  protected hasError(form: FormGroup, control: string, error: string): boolean {
    const c = form.get(control);
    return !!c && c.touched && c.hasError(error);
  }

  private makeRow(): AssignmentRow {
    return {
      uiKey: crypto.randomUUID(),
      roleId: null,
      scopeType: SCOPE_TENANT,
      scopeId: null
    };
  }

  private toastError(err: unknown, fallback: string): void {
    const msg = err instanceof HttpErrorResponse
      ? (err.error?.error ?? err.message ?? fallback)
      : fallback;
    this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
  }
}
