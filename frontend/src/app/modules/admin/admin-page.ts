import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { AdminUser } from './admin.types';

type Tab = 'users' | 'workspace';

@Component({
  selector: 'app-admin-page',
  imports: [
    ReactiveFormsModule, DatePipe,
    ButtonModule, TableModule, DialogModule, InputTextModule, PasswordModule, SelectModule,
    ToggleSwitchModule, TagModule, TooltipModule, SkeletonModule, IconFieldModule, InputIconModule
  ],
  templateUrl: './admin-page.html',
  styleUrl: './admin-page.scss'
})
export class AdminPage implements OnInit {
  protected readonly store = inject(AdminService);
  protected readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly confirm = inject(ConfirmationService);
  private readonly toast = inject(MessageService);

  protected readonly tab = signal<Tab>('users');
  protected readonly search = signal('');

  protected readonly dialogOpen = signal(false);
  protected readonly editing = signal<AdminUser | null>(null);
  protected readonly resetOpen = signal(false);
  protected readonly resetTarget = signal<AdminUser | null>(null);

  protected readonly roleOptions = [
    { label: 'Admin',      value: 'Admin' },
    { label: 'Supervisor', value: 'Supervisor' },
    { label: 'Operator',   value: 'Operator' },
    { label: 'Viewer',     value: 'Viewer' }
  ];

  protected readonly form: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    fullName: ['', [Validators.maxLength(120)]],
    role: ['Operator', [Validators.required]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    isActive: [true]
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
      u.role.toLowerCase().includes(q));
  });

  protected readonly isSelf = (u: AdminUser): boolean =>
    u.id === this.auth.user()?.id;

  protected readonly skeletonRows = Array.from({ length: 5 });

  ngOnInit(): void {
    void this.store.listUsers().catch(err => this.toastError(err, 'Could not load users.'));
    void this.store.loadWorkspace().then(() => {
      const ws = this.store.workspace();
      if (ws) this.workspaceForm.patchValue({ name: ws.name });
    }).catch(() => {});
  }

  protected setTab(t: Tab): void { this.tab.set(t); }

  // ============== USERS ==============

  protected openAdd(): void {
    this.editing.set(null);
    this.form.reset({ email: '', fullName: '', role: 'Operator', password: '', isActive: true });
    this.form.get('password')?.setValidators([Validators.required, Validators.minLength(8)]);
    this.form.get('password')?.updateValueAndValidity();
    this.form.get('email')?.enable();
    this.dialogOpen.set(true);
  }

  protected openEdit(u: AdminUser): void {
    this.editing.set(u);
    this.form.reset({
      email: u.email, fullName: u.fullName ?? '', role: u.role, password: '', isActive: u.isActive
    });
    // Email is immutable post-creation in this v1.
    this.form.get('email')?.disable();
    // Password is optional on edit (use the reset password action instead).
    this.form.get('password')?.clearValidators();
    this.form.get('password')?.updateValueAndValidity();
    this.dialogOpen.set(true);
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const current = this.editing();
    try {
      if (current) {
        await this.store.updateUser(current.id, {
          fullName: v.fullName || null,
          role: v.role,
          isActive: v.isActive
        });
        this.toast.add({ severity: 'success', summary: 'User updated', detail: v.email, life: 2500 });
      } else {
        await this.store.createUser({
          email: (v.email as string).trim().toLowerCase(),
          fullName: v.fullName || null,
          role: v.role,
          password: v.password
        });
        this.toast.add({ severity: 'success', summary: 'User created', detail: v.email + ' added. Share the temporary password.', life: 4000 });
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
      this.toast.add({ severity: 'success', summary: 'Password reset', detail: `Share the new password with ${u.email}. They've been signed out.`, life: 4500 });
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

  protected roleSeverity(role: string): 'info' | 'success' | 'warn' | 'danger' | 'secondary' {
    switch (role.toLowerCase()) {
      case 'admin':      return 'danger';
      case 'supervisor': return 'warn';
      case 'operator':   return 'info';
      case 'viewer':     return 'secondary';
      default:           return 'secondary';
    }
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

  private toastError(err: unknown, fallback: string): void {
    const msg = err instanceof HttpErrorResponse
      ? (err.error?.error ?? err.message ?? fallback)
      : fallback;
    this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
  }
}
