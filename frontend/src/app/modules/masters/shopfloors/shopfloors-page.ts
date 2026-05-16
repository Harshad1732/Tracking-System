import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { ShopfloorsService } from '../shopfloors.service';
import { ProcessesService } from '../processes.service';
import { Shopfloor } from '../master.types';

@Component({
  selector: 'app-shopfloors-page',
  imports: [
    ReactiveFormsModule,
    ButtonModule, TableModule, DialogModule, InputTextModule, InputNumberModule, SelectModule,
    ToggleSwitchModule, TagModule, TooltipModule, SkeletonModule,
    IconFieldModule, InputIconModule
  ],
  templateUrl: './shopfloors-page.html',
  styleUrl: './shopfloors-page.scss'
})
export class ShopfloorsPage implements OnInit {
  protected readonly store = inject(ShopfloorsService);
  protected readonly processes = inject(ProcessesService);
  private readonly fb = inject(FormBuilder);
  private readonly confirm = inject(ConfirmationService);
  private readonly toast = inject(MessageService);

  protected readonly dialogOpen = signal(false);
  protected readonly editing = signal<Shopfloor | null>(null);
  protected readonly search = signal('');
  protected readonly skeletonRows = Array.from({ length: 5 });

  protected readonly form: FormGroup = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(20)]],
    name: ['', [Validators.required, Validators.maxLength(80)]],
    sequenceNo: [10, [Validators.required, Validators.min(0)]],
    isStorage: [false],
    batchMode: ['None' as 'None' | 'AutoConfirm' | 'Manual'],
    processId: [null as string | null],
    isActive: [true]
  });

  protected readonly processOptions = computed(() => [
    { label: '— None —', value: null },
    ...this.processes.items().map(p => ({ label: p.name, value: p.id }))
  ]);

  protected readonly batchModeOptions = [
    { label: 'None — single sheets', value: 'None' },
    { label: 'Auto-confirm — system suggests batching', value: 'AutoConfirm' },
    { label: 'Manual — operator builds batches', value: 'Manual' }
  ];

  protected readonly filtered = computed<Shopfloor[]>(() => {
    const q = this.search().trim().toLowerCase();
    const list = this.store.items();
    if (!q) return list;
    return list.filter(s =>
      s.code.toLowerCase().includes(q) ||
      s.name.toLowerCase().includes(q) ||
      (s.processName ?? '').toLowerCase().includes(q)
    );
  });

  ngOnInit(): void {
    void this.store.list().catch(err => this.toastError(err, 'Could not load shopfloors.'));
    void this.processes.list().catch(() => {});
  }

  protected openAdd(): void {
    this.editing.set(null);
    this.form.reset({ code: '', name: '', sequenceNo: 10, isStorage: false, batchMode: 'None', processId: null, isActive: true });
    this.dialogOpen.set(true);
  }

  protected openEdit(s: Shopfloor): void {
    this.editing.set(s);
    this.form.reset({
      code: s.code, name: s.name,
      sequenceNo: s.sequenceNo, isStorage: s.isStorage,
      batchMode: s.batchMode ?? 'None',
      processId: s.processId, isActive: s.isActive
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
        this.toast.add({ severity: 'success', summary: 'Shopfloor updated', detail: `${v.name} saved.`, life: 2500 });
      } else {
        await this.store.create(v);
        this.toast.add({ severity: 'success', summary: 'Shopfloor added', detail: `${v.name} added.`, life: 2500 });
      }
      this.dialogOpen.set(false);
    } catch (err) {
      this.toastError(err, 'Could not save shopfloor.');
    }
  }

  protected onDelete(s: Shopfloor): void {
    this.confirm.confirm({
      header: 'Delete shopfloor?',
      message: `“${s.name}” will be permanently removed. Sheets must be moved off first.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Delete', rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-text p-button-secondary',
      accept: async () => {
        try {
          await this.store.remove(s.id);
          this.toast.add({ severity: 'success', summary: 'Shopfloor deleted', detail: `${s.name} removed.`, life: 2500 });
        } catch (err) {
          this.toastError(err, 'Could not delete shopfloor.');
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
