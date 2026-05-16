import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { CheckboxModule } from 'primeng/checkbox';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { SkeletonModule } from 'primeng/skeleton';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { ConfirmationService, MessageService } from 'primeng/api';
import { RolesService } from '../roles.service';
import { Role } from '../master.types';

@Component({
  selector: 'app-roles-page',
  imports: [
    ReactiveFormsModule, DatePipe,
    ButtonModule, TableModule, DialogModule, InputTextModule, TextareaModule, CheckboxModule,
    ToggleSwitchModule, TagModule, TooltipModule, SkeletonModule,
    IconFieldModule, InputIconModule
  ],
  templateUrl: './roles-page.html',
  styleUrl: './roles-page.scss'
})
export class RolesPage implements OnInit {
  protected readonly store = inject(RolesService);
  private readonly fb = inject(FormBuilder);
  private readonly confirm = inject(ConfirmationService);
  private readonly toast = inject(MessageService);

  protected readonly dialogOpen = signal(false);
  protected readonly editing = signal<Role | null>(null);
  protected readonly search = signal('');
  protected readonly skeletonRows = Array.from({ length: 4 });

  protected readonly form: FormGroup = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(60)]],
    description: ['', [Validators.maxLength(250)]],
    canView: [true],
    canAdd: [false],
    canEdit: [false],
    canDelete: [false],
    canViewReports: [false],
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

  ngOnInit(): void {
    void this.store.list().catch(err => this.toastError(err, 'Could not load roles.'));
  }

  protected openAdd(): void {
    this.editing.set(null);
    this.form.reset({
      name: '', description: '',
      canView: true, canAdd: false, canEdit: false, canDelete: false, canViewReports: false,
      isActive: true
    });
    this.dialogOpen.set(true);
  }

  protected openEdit(r: Role): void {
    this.editing.set(r);
    this.form.reset({
      name: r.name, description: r.description ?? '',
      canView: r.canView, canAdd: r.canAdd, canEdit: r.canEdit,
      canDelete: r.canDelete, canViewReports: r.canViewReports,
      isActive: r.isActive
    });
    this.dialogOpen.set(true);
  }

  protected closeDialog(): void { this.dialogOpen.set(false); }

  protected permLabels(r: Role): string[] {
    const out: string[] = [];
    if (r.canView) out.push('View');
    if (r.canAdd) out.push('Add');
    if (r.canEdit) out.push('Edit');
    if (r.canDelete) out.push('Delete');
    if (r.canViewReports) out.push('Reports');
    return out;
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const input = {
      name: v.name,
      description: v.description || null,
      canView: v.canView, canAdd: v.canAdd, canEdit: v.canEdit,
      canDelete: v.canDelete, canViewReports: v.canViewReports,
      isActive: v.isActive
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

  private toastError(err: unknown, fallback: string): void {
    const msg = err instanceof HttpErrorResponse
      ? (err.error?.error ?? err.message ?? fallback)
      : fallback;
    this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
  }
}
