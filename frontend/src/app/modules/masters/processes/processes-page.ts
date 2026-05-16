import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { SkeletonModule } from 'primeng/skeleton';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ProcessesService } from '../processes.service';
import { PlantsService } from '../plants.service';
import { Process } from '../master.types';

@Component({
  selector: 'app-processes-page',
  imports: [
    ReactiveFormsModule, DatePipe,
    ButtonModule, TableModule, DialogModule, InputTextModule, InputNumberModule,
    SelectModule, ToggleSwitchModule, TagModule, TooltipModule, SkeletonModule,
    IconFieldModule, InputIconModule
  ],
  templateUrl: './processes-page.html',
  styleUrl: './processes-page.scss'
})
export class ProcessesPage implements OnInit {
  protected readonly store = inject(ProcessesService);
  protected readonly plants = inject(PlantsService);
  private readonly fb = inject(FormBuilder);
  private readonly confirm = inject(ConfirmationService);
  private readonly toast = inject(MessageService);

  protected readonly dialogOpen = signal(false);
  protected readonly editing = signal<Process | null>(null);
  protected readonly search = signal('');
  protected readonly skeletonRows = Array.from({ length: 5 });

  protected readonly form: FormGroup = this.fb.group({
    plantId: [null as string | null, [Validators.required]],
    code: ['', [Validators.required, Validators.maxLength(20)]],
    name: ['', [Validators.required, Validators.maxLength(120)]],
    sequenceNo: [10, [Validators.required, Validators.min(0)]],
    isActive: [true]
  });

  protected readonly plantOptions = computed(() =>
    this.plants.items().map(p => ({ label: p.name, value: p.id }))
  );

  protected readonly filtered = computed<Process[]>(() => {
    const q = this.search().trim().toLowerCase();
    const list = this.store.items();
    if (!q) return list;
    return list.filter(p =>
      p.code.toLowerCase().includes(q) ||
      p.name.toLowerCase().includes(q) ||
      p.plantName.toLowerCase().includes(q)
    );
  });

  ngOnInit(): void {
    void this.store.list().catch(err => this.toastError(err, 'Could not load processes.'));
    void this.plants.list().catch(() => { /* dropdown will just be empty */ });
  }

  protected openAdd(): void {
    this.editing.set(null);
    this.form.reset({ plantId: null, code: '', name: '', sequenceNo: 10, isActive: true });
    this.dialogOpen.set(true);
  }

  protected openEdit(process: Process): void {
    this.editing.set(process);
    this.form.reset({
      plantId: process.plantId,
      code: process.code,
      name: process.name,
      sequenceNo: process.sequenceNo,
      isActive: process.isActive
    });
    this.dialogOpen.set(true);
  }

  protected closeDialog(): void { this.dialogOpen.set(false); }

  protected async submit(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const current = this.editing();
    try {
      if (current) {
        await this.store.update(current.id, v);
        this.toast.add({ severity: 'success', summary: 'Process updated', detail: `${v.name} has been saved.`, life: 2500 });
      } else {
        await this.store.create(v);
        this.toast.add({ severity: 'success', summary: 'Process added', detail: `${v.name} has been added.`, life: 2500 });
      }
      this.dialogOpen.set(false);
    } catch (err) {
      this.toastError(err, 'Could not save process.');
    }
  }

  protected onDelete(p: Process): void {
    this.confirm.confirm({
      header: 'Delete process?',
      message: `“${p.name}” will be permanently removed. This cannot be undone.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Delete', rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-text p-button-secondary',
      accept: async () => {
        try {
          await this.store.remove(p.id);
          this.toast.add({ severity: 'success', summary: 'Process deleted', detail: `${p.name} has been removed.`, life: 2500 });
        } catch (err) {
          this.toastError(err, 'Could not delete process.');
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
