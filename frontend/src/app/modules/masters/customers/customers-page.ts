import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TagModule } from 'primeng/tag';
import { ConfirmationService, MessageService } from 'primeng/api';
import { CustomersService } from '../customers.service';
import { Customer } from '../master.types';
import { AuthService } from '../../../auth/auth.service';
import { PageHeaderComponent } from '../../../shared/page-header/page-header';
import { SearchInputComponent } from '../../../shared/search-input/search-input';
import { SkeletonTableComponent } from '../../../shared/skeleton-table/skeleton-table';
import { EmptyStateComponent } from '../../../shared/empty-state/empty-state';
import { RowActionsComponent } from '../../../shared/row-actions/row-actions';
import { FormDialogComponent } from '../../../shared/form-dialog/form-dialog';
import { HasPermDirective } from '../../../shared/has-perm.directive';

@Component({
  selector: 'app-customers-page',
  imports: [
    ReactiveFormsModule,
    PageHeaderComponent, SearchInputComponent, SkeletonTableComponent,
    EmptyStateComponent, RowActionsComponent, FormDialogComponent, HasPermDirective,
    ButtonModule, TableModule, InputTextModule, TextareaModule,
    ToggleSwitchModule, TagModule
  ],
  templateUrl: './customers-page.html',
  styleUrl: './customers-page.scss'
})
export class CustomersPage implements OnInit {
  protected readonly store = inject(CustomersService);
  protected readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly confirm = inject(ConfirmationService);
  private readonly toast = inject(MessageService);

  protected readonly dialogOpen = signal(false);
  protected readonly editing = signal<Customer | null>(null);
  protected readonly search = signal('');
  protected readonly skeletonRows = Array.from({ length: 5 });

  protected readonly form: FormGroup = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    contactPerson: ['', [Validators.maxLength(120)]],
    mobile: ['', [Validators.maxLength(30)]],
    email: ['', [Validators.email, Validators.maxLength(150)]],
    address: ['', [Validators.maxLength(250)]],
    isActive: [true]
  });

  protected readonly filtered = computed<Customer[]>(() => {
    const q = this.search().trim().toLowerCase();
    const list = this.store.items();
    if (!q) return list;
    return list.filter(c =>
      c.code.toLowerCase().includes(q) ||
      c.name.toLowerCase().includes(q) ||
      (c.contactPerson ?? '').toLowerCase().includes(q) ||
      (c.mobile ?? '').toLowerCase().includes(q) ||
      (c.email ?? '').toLowerCase().includes(q)
    );
  });

  ngOnInit(): void {
    void this.store.list().catch(err => this.toastError(err, 'Could not load customers.'));
  }

  protected openAdd(): void {
    this.editing.set(null);
    this.form.reset({
      name: '', contactPerson: '', mobile: '', email: '', address: '', isActive: true
    });
    this.dialogOpen.set(true);
  }

  protected openEdit(c: Customer): void {
    this.editing.set(c);
    this.form.reset({
      name: c.name,
      contactPerson: c.contactPerson ?? '',
      mobile: c.mobile ?? '',
      email: c.email ?? '',
      address: c.address ?? '',
      isActive: c.isActive
    });
    this.dialogOpen.set(true);
  }

  protected closeDialog(): void { this.dialogOpen.set(false); }

  protected async submit(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const input = {
      name: v.name,
      contactPerson: v.contactPerson || null,
      mobile: v.mobile || null,
      email: v.email || null,
      address: v.address || null,
      isActive: v.isActive
    };
    const current = this.editing();
    try {
      if (current) {
        await this.store.update(current.id, input);
        this.toast.add({ severity: 'success', summary: 'Customer updated', detail: `${v.name} has been saved.`, life: 2500 });
      } else {
        await this.store.create(input);
        this.toast.add({ severity: 'success', summary: 'Customer added', detail: `${v.name} has been added.`, life: 2500 });
      }
      this.dialogOpen.set(false);
    } catch (err) {
      this.toastError(err, 'Could not save customer.');
    }
  }

  protected onDelete(c: Customer): void {
    this.confirm.confirm({
      header: 'Delete customer?',
      message: `“${c.name}” will be permanently removed. This cannot be undone.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Delete', rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-text p-button-secondary',
      accept: async () => {
        try {
          await this.store.remove(c.id);
          this.toast.add({ severity: 'success', summary: 'Customer deleted', detail: `${c.name} has been removed.`, life: 2500 });
        } catch (err) {
          this.toastError(err, 'Could not delete customer.');
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
