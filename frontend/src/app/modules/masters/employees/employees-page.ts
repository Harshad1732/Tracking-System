import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { SkeletonModule } from 'primeng/skeleton';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { ConfirmationService, MessageService } from 'primeng/api';
import { EmployeesService } from '../employees.service';
import { PlantsService } from '../plants.service';
import { ProcessesService } from '../processes.service';
import { Employee } from '../master.types';

@Component({
  selector: 'app-employees-page',
  imports: [
    ReactiveFormsModule,
    ButtonModule, TableModule, DialogModule, InputTextModule, SelectModule,
    ToggleSwitchModule, TagModule, TooltipModule, SkeletonModule,
    IconFieldModule, InputIconModule
  ],
  templateUrl: './employees-page.html',
  styleUrl: './employees-page.scss'
})
export class EmployeesPage implements OnInit {
  protected readonly store = inject(EmployeesService);
  protected readonly plants = inject(PlantsService);
  protected readonly processes = inject(ProcessesService);
  private readonly fb = inject(FormBuilder);
  private readonly confirm = inject(ConfirmationService);
  private readonly toast = inject(MessageService);

  protected readonly dialogOpen = signal(false);
  protected readonly editing = signal<Employee | null>(null);
  protected readonly search = signal('');
  protected readonly skeletonRows = Array.from({ length: 6 });

  protected readonly form: FormGroup = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(20)]],
    name: ['', [Validators.required, Validators.maxLength(120)]],
    mobile: ['', [Validators.maxLength(30)]],
    department: ['', [Validators.maxLength(60)]],
    designation: ['', [Validators.maxLength(60)]],
    plantId: [null as string | null],
    processId: [null as string | null],
    isActive: [true]
  });

  protected readonly plantOptions = computed(() => [
    { label: '— None —', value: null },
    ...this.plants.items().map(p => ({ label: p.name, value: p.id }))
  ]);

  protected readonly processOptions = computed(() => {
    const pid = this.form.get('plantId')?.value as string | null;
    const all = this.processes.items();
    const scoped = pid ? all.filter(p => p.plantId === pid) : all;
    return [
      { label: '— None —', value: null },
      ...scoped.map(p => ({ label: p.name, value: p.id }))
    ];
  });

  protected readonly filtered = computed<Employee[]>(() => {
    const q = this.search().trim().toLowerCase();
    const list = this.store.items();
    if (!q) return list;
    return list.filter(e =>
      e.code.toLowerCase().includes(q) ||
      e.name.toLowerCase().includes(q) ||
      (e.mobile ?? '').toLowerCase().includes(q) ||
      (e.department ?? '').toLowerCase().includes(q) ||
      (e.designation ?? '').toLowerCase().includes(q) ||
      (e.plantName ?? '').toLowerCase().includes(q) ||
      (e.processName ?? '').toLowerCase().includes(q)
    );
  });

  ngOnInit(): void {
    void this.store.list().catch(err => this.toastError(err, 'Could not load employees.'));
    void this.plants.list().catch(() => {});
    void this.processes.list().catch(() => {});
  }

  protected openAdd(): void {
    this.editing.set(null);
    this.form.reset({
      code: '', name: '', mobile: '', department: '', designation: '',
      plantId: null, processId: null, isActive: true
    });
    this.dialogOpen.set(true);
  }

  protected openEdit(e: Employee): void {
    this.editing.set(e);
    this.form.reset({
      code: e.code, name: e.name,
      mobile: e.mobile ?? '', department: e.department ?? '', designation: e.designation ?? '',
      plantId: e.plantId, processId: e.processId,
      isActive: e.isActive
    });
    this.dialogOpen.set(true);
  }

  protected closeDialog(): void { this.dialogOpen.set(false); }

  protected onPlantChange(): void {
    // Reset process if it's no longer valid for the new plant
    const pid = this.form.get('plantId')?.value as string | null;
    const prid = this.form.get('processId')?.value as string | null;
    if (pid && prid) {
      const found = this.processes.items().find(p => p.id === prid);
      if (!found || found.plantId !== pid) this.form.patchValue({ processId: null });
    }
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const input = {
      code: v.code, name: v.name,
      mobile: v.mobile || null,
      department: v.department || null,
      designation: v.designation || null,
      plantId: v.plantId, processId: v.processId,
      isActive: v.isActive
    };
    const current = this.editing();
    try {
      if (current) {
        await this.store.update(current.id, input);
        this.toast.add({ severity: 'success', summary: 'Employee updated', detail: `${v.name} has been saved.`, life: 2500 });
      } else {
        await this.store.create(input);
        this.toast.add({ severity: 'success', summary: 'Employee added', detail: `${v.name} has been added.`, life: 2500 });
      }
      this.dialogOpen.set(false);
    } catch (err) {
      this.toastError(err, 'Could not save employee.');
    }
  }

  protected onDelete(e: Employee): void {
    this.confirm.confirm({
      header: 'Delete employee?',
      message: `“${e.name}” will be permanently removed. This cannot be undone.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Delete', rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-text p-button-secondary',
      accept: async () => {
        try {
          await this.store.remove(e.id);
          this.toast.add({ severity: 'success', summary: 'Employee deleted', detail: `${e.name} has been removed.`, life: 2500 });
        } catch (err) {
          this.toastError(err, 'Could not delete employee.');
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
