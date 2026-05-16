import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ShopfloorsService } from '../shopfloors.service';
import { ProcessesService } from '../processes.service';
import { Shopfloor } from '../master.types';
import { COLOR_CHOICES, floorTone } from '../floor-tones';
import { AuthService } from '../../../auth/auth.service';
import { PageHeaderComponent } from '../../../shared/page-header/page-header';
import { SearchInputComponent } from '../../../shared/search-input/search-input';
import { SkeletonTableComponent } from '../../../shared/skeleton-table/skeleton-table';
import { EmptyStateComponent } from '../../../shared/empty-state/empty-state';
import { RowActionsComponent } from '../../../shared/row-actions/row-actions';
import { FormDialogComponent } from '../../../shared/form-dialog/form-dialog';
import { HasPermDirective } from '../../../shared/has-perm.directive';

@Component({
  selector: 'app-shopfloors-page',
  imports: [
    ReactiveFormsModule,
    PageHeaderComponent, SearchInputComponent, SkeletonTableComponent,
    EmptyStateComponent, RowActionsComponent, FormDialogComponent, HasPermDirective,
    ButtonModule, TableModule, InputTextModule, InputNumberModule, SelectModule,
    ToggleSwitchModule, TagModule, TooltipModule
  ],
  templateUrl: './shopfloors-page.html',
  styleUrl: './shopfloors-page.scss'
})
export class ShopfloorsPage implements OnInit {
  protected readonly store = inject(ShopfloorsService);
  protected readonly processes = inject(ProcessesService);
  protected readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly confirm = inject(ConfirmationService);
  private readonly toast = inject(MessageService);

  protected readonly dialogOpen = signal(false);
  protected readonly editing = signal<Shopfloor | null>(null);
  protected readonly search = signal('');
  protected readonly skeletonRows = Array.from({ length: 5 });

  protected readonly form: FormGroup = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    sequenceNo: [10, [Validators.required, Validators.min(0)]],
    isStorage: [false],
    batchMode: ['None' as 'None' | 'AutoConfirm' | 'Manual'],
    processId: [null as string | null],
    color: [null as string | null],
    isActive: [true]
  });

  protected readonly colorChoices = COLOR_CHOICES;

  /** Tile preview that updates live as the user picks options in the dialog. */
  protected previewTone(): { base: string; dark: string } {
    const v = this.form.value;
    return floorTone({
      code: this.editing()?.code ?? 'NEW',
      isStorage: !!v.isStorage,
      sequenceNo: v.sequenceNo ?? 0,
      color: v.color
    });
  }

  /** Color shown for a shopfloor row in the table. */
  protected rowTone(s: Shopfloor): { base: string; dark: string } {
    return floorTone(s);
  }

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
    this.form.reset({ name: '', sequenceNo: 10, isStorage: false, batchMode: 'None', processId: null, color: null, isActive: true });
    this.dialogOpen.set(true);
  }

  protected openEdit(s: Shopfloor): void {
    this.editing.set(s);
    this.form.reset({
      name: s.name,
      sequenceNo: s.sequenceNo, isStorage: s.isStorage,
      batchMode: s.batchMode ?? 'None',
      processId: s.processId,
      color: s.color ?? null,
      isActive: s.isActive
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
