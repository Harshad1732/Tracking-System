import { Component, OnDestroy, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, NgClass } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { SkeletonModule } from 'primeng/skeleton';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { ConfirmationService, MessageService } from 'primeng/api';
import { CheckboxModule } from 'primeng/checkbox';
import { SheetsService } from './sheets.service';
import { BatchesService } from './batches.service';
import { ShopfloorsService } from '../masters/shopfloors.service';
import { CustomersService } from '../masters/customers.service';
import { floorTone, FloorTone } from '../masters/floor-tones';
import { Batch, GlassSheet, Shopfloor, SheetCreateInput, SheetMovement } from '../masters/master.types';
import { I18nService } from '../../shared/i18n/i18n.service';
import { TPipe } from '../../shared/i18n/t.pipe';

const NEUTRAL_TONE: FloorTone = { base: '#475569', dark: '#1f2937', bg: '#f1f5f9', text: '#475569' };

interface ParsedRow {
  sheetNo: string;
  orderNo: string | null;
  customerName: string | null;
  glassType: string | null;
  thickness: number | null;
  width: number | null;
  height: number | null;
  quantity: number;
  remarks: string | null;
  _customerId: string | null;
}

@Component({
  selector: 'app-shopfloor-page',
  imports: [
    ReactiveFormsModule, FormsModule, DatePipe, NgClass, RouterLink,
    ButtonModule, TableModule, DialogModule, DrawerModule, InputTextModule, InputNumberModule,
    TextareaModule, SelectModule, TagModule, TooltipModule, SkeletonModule, CheckboxModule,
    IconFieldModule, InputIconModule, TPipe
  ],
  templateUrl: './shopfloor-page.html',
  styleUrl: './shopfloor-page.scss'
})
export class ShopfloorPage implements OnInit, OnDestroy {
  protected readonly sheets = inject(SheetsService);
  protected readonly batches = inject(BatchesService);
  protected readonly shopfloors = inject(ShopfloorsService);
  protected readonly customers = inject(CustomersService);
  protected readonly i18n = inject(I18nService);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly confirm = inject(ConfirmationService);
  private readonly toast = inject(MessageService);

  protected readonly code = signal<string>('');
  protected readonly search = signal('');
  protected readonly selectedSheets = signal<GlassSheet[]>([]);
  protected readonly selectedBatches = signal<Batch[]>([]);
  protected readonly expandedBatches = signal<Set<string>>(new Set());
  protected readonly skeletonRows = Array.from({ length: 6 });

  protected readonly addOpen = signal(false);
  protected readonly moveOpen = signal(false);
  protected readonly importOpen = signal(false);
  protected readonly statusOpen = signal(false);
  protected readonly historyOpen = signal(false);
  protected readonly formBatchOpen = signal(false);
  protected readonly batchMoveOpen = signal(false);

  // Import state
  protected readonly importParsed = signal<ParsedRow[]>([]);
  protected readonly importFileName = signal<string | null>(null);
  protected readonly importing = signal(false);
  protected readonly importErrors = signal<string[]>([]);

  // History drawer state
  protected readonly historySheet = signal<GlassSheet | null>(null);
  protected readonly historyMovements = signal<SheetMovement[]>([]);
  protected readonly historyLoading = signal(false);

  protected readonly statusOptions = [
    { label: 'Completed', value: 'Completed', icon: 'pi pi-check-circle', tone: 'completed' },
    { label: 'On Hold',   value: 'Hold',      icon: 'pi pi-pause-circle', tone: 'hold' },
    { label: 'Rejected',  value: 'Rejected',  icon: 'pi pi-times-circle', tone: 'rejected' },
    { label: 'Delivered', value: 'Delivered', icon: 'pi pi-truck',        tone: 'delivered' }
  ];

  protected readonly addForm: FormGroup = this.fb.group({
    sheetNo: ['', [Validators.required, Validators.maxLength(60)]],
    orderNo: ['', [Validators.maxLength(80)]],
    customerId: [null as string | null],
    glassType: ['', [Validators.maxLength(60)]],
    thickness: [null as number | null],
    width: [null as number | null],
    height: [null as number | null],
    quantity: [1, [Validators.required, Validators.min(1)]],
    remarks: ['', [Validators.maxLength(250)]]
  });

  protected readonly moveForm: FormGroup = this.fb.group({
    toShopfloorId: [null as string | null, [Validators.required]],
    remarks: ['', [Validators.maxLength(250)]],
    createBatch: [true]
  });

  protected readonly statusForm: FormGroup = this.fb.group({
    status: [null as string | null, [Validators.required]],
    remarks: ['', [Validators.maxLength(250)]]
  });

  protected readonly formBatchForm: FormGroup = this.fb.group({
    remarks: ['', [Validators.maxLength(250)]]
  });

  protected readonly batchMoveForm: FormGroup = this.fb.group({
    toShopfloorId: [null as string | null, [Validators.required]],
    remarks: ['', [Validators.maxLength(250)]]
  });

  protected readonly batchMode = computed<'None' | 'AutoConfirm' | 'Manual'>(() =>
    (this.currentShopfloor()?.batchMode ?? 'None') as 'None' | 'AutoConfirm' | 'Manual'
  );

  protected readonly isBatchMode = computed(() => this.batchMode() !== 'None');

  protected readonly looseSheets = computed<GlassSheet[]>(() =>
    this.sheets.items().filter(s => !s.batchId)
  );

  protected readonly moveTargetIsAutoBatch = computed(() => {
    const toId = this.moveForm.get('toShopfloorId')?.value as string | null;
    if (!toId) return false;
    const target = this.shopfloors.items().find(s => s.id === toId);
    return target?.batchMode === 'AutoConfirm';
  });

  protected readonly floorTone = computed<FloorTone>(() => {
    const sf = this.currentShopfloor();
    return sf ? floorTone(sf) : NEUTRAL_TONE;
  });

  protected readonly currentShopfloor = computed<Shopfloor | null>(() => {
    const c = this.code();
    if (!c) return null;
    return this.shopfloors.byCode(c) ?? null;
  });

  protected readonly nextShopfloor = computed<Shopfloor | null>(() => {
    const cur = this.currentShopfloor();
    return cur ? this.shopfloors.nextAfter(cur) : null;
  });

  protected readonly moveTargets = computed(() =>
    this.shopfloors.items()
      .filter(s => s.isActive && s.id !== this.currentShopfloor()?.id)
      .map(s => ({
        label: `${s.name}${s.isStorage ? ' (Storage)' : ''}`,
        value: s.id
      }))
  );

  protected readonly customerOptions = computed(() => [
    { label: '— None —', value: null },
    ...this.customers.items().map(c => ({ label: c.name, value: c.id }))
  ]);

  protected readonly filteredSheets = computed<GlassSheet[]>(() => {
    const q = this.search().trim().toLowerCase();
    // In batch mode, only show loose (un-batched) sheets in the main table.
    const list = this.isBatchMode() ? this.looseSheets() : this.sheets.items();
    if (!q) return list;
    return list.filter(s =>
      s.sheetNo.toLowerCase().includes(q) ||
      (s.orderNo ?? '').toLowerCase().includes(q) ||
      (s.customerName ?? '').toLowerCase().includes(q) ||
      (s.glassType ?? '').toLowerCase().includes(q)
    );
  });

  private routeSub?: Subscription;
  private pendingAction: 'import' | 'add' | null = null;

  constructor() {
    // Whenever shopfloors or code changes, re-fetch sheets; also handle deep-link actions.
    effect(() => {
      const sf = this.currentShopfloor();
      if (sf) {
        this.selectedSheets.set([]);
        this.selectedBatches.set([]);
        void this.sheets.listByShopfloor(sf.id).catch(err =>
          this.toastError(err, 'Could not load sheets.'));
        if (sf.batchMode && sf.batchMode !== 'None') {
          void this.batches.listByShopfloor(sf.id).catch(err =>
            this.toastError(err, 'Could not load batches.'));
        }

        // Consume deep-link action (?import=1 / ?add=1) once the floor resolves.
        if (this.pendingAction === 'import' && sf.isStorage) {
          this.pendingAction = null;
          this.openImport();
        } else if (this.pendingAction === 'add') {
          this.pendingAction = null;
          this.openAdd();
        }
      }
    });
  }

  ngOnInit(): void {
    this.routeSub = this.route.paramMap.subscribe(params => {
      this.code.set(params.get('code') ?? '');
    });

    // Honor ?import=1 / ?add=1 deep links from the Workspace page.
    const qp = this.route.snapshot.queryParamMap;
    if (qp.get('import') === '1') this.pendingAction = 'import';
    else if (qp.get('add') === '1') this.pendingAction = 'add';

    void this.shopfloors.list().catch(() => {});
    void this.customers.list().catch(() => {});
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
  }

  // ============== ADD SHEET ==============

  protected openAdd(): void {
    this.addForm.reset({
      sheetNo: '', orderNo: '', customerId: null, glassType: '',
      thickness: null, width: null, height: null, quantity: 1, remarks: ''
    });
    this.addOpen.set(true);
  }

  protected async submitAdd(): Promise<void> {
    if (this.addForm.invalid) { this.addForm.markAllAsTouched(); return; }
    const v = this.addForm.getRawValue();
    const input: SheetCreateInput = {
      sheetNo: v.sheetNo, orderNo: v.orderNo || null, customerId: v.customerId,
      glassType: v.glassType || null,
      thickness: v.thickness, width: v.width, height: v.height,
      quantity: v.quantity, remarks: v.remarks || null
    };
    try {
      await this.sheets.create(input);
      this.toast.add({ severity: 'success', summary: 'Sheet added', detail: `${v.sheetNo} added to Storage.`, life: 2500 });
      this.addOpen.set(false);
      // Refresh current view (sheet may or may not appear here depending on where we are)
      const sf = this.currentShopfloor();
      if (sf?.isStorage) void this.sheets.listByShopfloor(sf.id);
    } catch (err) {
      this.toastError(err, 'Could not add sheet.');
    }
  }

  // ============== MOVE ==============

  protected openMove(toShopfloorId?: string): void {
    const target = toShopfloorId ?? this.nextShopfloor()?.id ?? null;
    this.moveForm.reset({ toShopfloorId: target, remarks: '', createBatch: true });
    this.moveOpen.set(true);
  }

  protected async submitMove(): Promise<void> {
    if (this.moveForm.invalid) { this.moveForm.markAllAsTouched(); return; }
    const ids = this.selectedSheets().map(s => s.id);
    if (ids.length === 0) {
      this.toast.add({ severity: 'warn', summary: 'Nothing selected', detail: 'Select at least one sheet to move.' });
      return;
    }
    const v = this.moveForm.getRawValue();
    const target = this.shopfloors.items().find(s => s.id === v.toShopfloorId);
    if (!target) return;
    const wantBatch = this.moveTargetIsAutoBatch() && v.createBatch === true;
    try {
      const count = await this.sheets.move(ids, v.toShopfloorId, v.remarks || null, wantBatch);
      const batchHint = wantBatch ? ' as a new batch' : '';
      this.toast.add({
        severity: 'success', summary: 'Sheets moved',
        detail: `${count} sheet${count === 1 ? '' : 's'} moved to ${target.name}${batchHint}.`,
        life: 3000
      });
      this.selectedSheets.set([]);
      this.moveOpen.set(false);
      void this.shopfloors.list();
    } catch (err) {
      this.toastError(err, 'Could not move sheets.');
    }
  }

  // ============== BATCH ACTIONS ==============

  protected toggleBatchExpanded(id: string): void {
    const next = new Set(this.expandedBatches());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.expandedBatches.set(next);
  }

  protected isBatchSelected(b: Batch): boolean {
    return this.selectedBatches().some(x => x.id === b.id);
  }

  protected toggleBatchSelected(b: Batch): void {
    const cur = this.selectedBatches();
    if (cur.some(x => x.id === b.id)) {
      this.selectedBatches.set(cur.filter(x => x.id !== b.id));
    } else {
      this.selectedBatches.set([...cur, b]);
    }
  }

  protected openFormBatch(): void {
    if (this.selectedSheets().length === 0) {
      this.toast.add({ severity: 'warn', summary: 'Nothing selected', detail: 'Select loose sheets to group.' });
      return;
    }
    this.formBatchForm.reset({ remarks: '' });
    this.formBatchOpen.set(true);
  }

  protected async submitFormBatch(): Promise<void> {
    const sf = this.currentShopfloor();
    if (!sf) return;
    const ids = this.selectedSheets().map(s => s.id);
    const remarks = (this.formBatchForm.value.remarks as string) || null;
    try {
      const created = await this.batches.create({ shopfloorId: sf.id, sheetIds: ids, remarks });
      this.toast.add({
        severity: 'success', summary: 'Batch created',
        detail: `${created.batchNo} created with ${ids.length} sheet${ids.length === 1 ? '' : 's'}.`,
        life: 3000
      });
      this.formBatchOpen.set(false);
      this.selectedSheets.set([]);
      // refresh sheets so the newly-batched ones get batchId
      await this.sheets.listByShopfloor(sf.id);
    } catch (err) {
      this.toastError(err, 'Could not create batch.');
    }
  }

  protected openBatchMove(toShopfloorId?: string): void {
    if (this.selectedBatches().length === 0) {
      this.toast.add({ severity: 'warn', summary: 'No batches selected', detail: 'Select at least one batch.' });
      return;
    }
    const target = toShopfloorId ?? this.nextShopfloor()?.id ?? null;
    this.batchMoveForm.reset({ toShopfloorId: target, remarks: '' });
    this.batchMoveOpen.set(true);
  }

  protected async submitBatchMove(): Promise<void> {
    if (this.batchMoveForm.invalid) { this.batchMoveForm.markAllAsTouched(); return; }
    const ids = this.selectedBatches().map(b => b.id);
    const v = this.batchMoveForm.getRawValue();
    const target = this.shopfloors.items().find(s => s.id === v.toShopfloorId);
    if (!target) return;
    try {
      const count = await this.batches.move({ batchIds: ids, toShopfloorId: v.toShopfloorId, remarks: v.remarks || null });
      this.toast.add({
        severity: 'success', summary: 'Batches moved',
        detail: `${count} batch${count === 1 ? '' : 'es'} moved to ${target.name}.`,
        life: 3000
      });
      this.selectedBatches.set([]);
      this.batchMoveOpen.set(false);
      const sf = this.currentShopfloor();
      if (sf) await this.sheets.listByShopfloor(sf.id);
      void this.shopfloors.list();
    } catch (err) {
      this.toastError(err, 'Could not move batches.');
    }
  }

  protected dissolveBatch(b: Batch): void {
    this.confirm.confirm({
      header: 'Dissolve batch?',
      message: `Batch ${b.batchNo} will be broken up. Its ${b.sheetCount} sheet(s) will go back to individual handling on this floor.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Dissolve', rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-text p-button-secondary',
      accept: async () => {
        try {
          await this.batches.dissolve(b.id);
          this.toast.add({ severity: 'success', summary: 'Batch dissolved', detail: b.batchNo, life: 2500 });
          const sf = this.currentShopfloor();
          if (sf) await this.sheets.listByShopfloor(sf.id);
        } catch (err) {
          this.toastError(err, 'Could not dissolve batch.');
        }
      }
    });
  }

  protected quickSendNext(): void {
    const next = this.nextShopfloor();
    if (!next) {
      this.toast.add({ severity: 'warn', summary: 'No next shopfloor', detail: 'This is the last shopfloor in sequence.' });
      return;
    }
    if (this.selectedSheets().length === 0) {
      this.toast.add({ severity: 'warn', summary: 'Nothing selected', detail: 'Select sheets to send.' });
      return;
    }
    this.openMove(next.id);
  }

  // ============== STATUS ==============

  protected openStatus(presetStatus?: string): void {
    if (this.selectedSheets().length === 0) {
      this.toast.add({ severity: 'warn', summary: 'Nothing selected', detail: 'Select sheets first.' });
      return;
    }
    this.statusForm.reset({ status: presetStatus ?? null, remarks: '' });
    this.statusOpen.set(true);
  }

  protected async submitStatus(): Promise<void> {
    if (this.statusForm.invalid) { this.statusForm.markAllAsTouched(); return; }
    const ids = this.selectedSheets().map(s => s.id);
    const v = this.statusForm.getRawValue();
    try {
      const count = await this.sheets.setStatus(ids, v.status, v.remarks || null);
      this.toast.add({
        severity: 'success', summary: 'Status updated',
        detail: `${count} sheet${count === 1 ? '' : 's'} marked as ${v.status}.`,
        life: 2800
      });
      this.statusOpen.set(false);
      this.selectedSheets.set([]);
    } catch (err) {
      this.toastError(err, 'Could not update status.');
    }
  }

  // ============== HISTORY DRAWER ==============

  protected async openHistory(sheet: GlassSheet): Promise<void> {
    this.historySheet.set(sheet);
    this.historyMovements.set([]);
    this.historyOpen.set(true);
    this.historyLoading.set(true);
    try {
      const movs = await this.sheets.movements(sheet.id);
      this.historyMovements.set(movs);
    } catch (err) {
      this.toastError(err, 'Could not load history.');
    } finally {
      this.historyLoading.set(false);
    }
  }

  protected closeHistory(): void {
    this.historyOpen.set(false);
  }

  protected clearSelection(): void { this.selectedSheets.set([]); }
  protected onSelectionChange(value: GlassSheet[] | GlassSheet | null): void {
    this.selectedSheets.set(Array.isArray(value) ? value : value ? [value] : []);
  }

  // ============== IMPORT EXCEL ==============

  protected openImport(): void {
    this.importParsed.set([]);
    this.importFileName.set(null);
    this.importErrors.set([]);
    this.importOpen.set(true);
  }

  protected async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.importFileName.set(file.name);
    try {
      const XLSX = await import('xlsx');
      const buf = await file.arrayBuffer();
      const wb = XLSX.read(buf);
      const wsName = wb.SheetNames[0];
      if (!wsName) {
        this.importErrors.set(['The workbook has no sheets.']);
        this.importParsed.set([]);
        return;
      }
      const ws = wb.Sheets[wsName];
      const rows = XLSX.utils.sheet_to_json<Record<string, unknown>>(ws, { defval: null, raw: true });
      const errors: string[] = [];
      const customers = this.customers.items();
      const parsed: ParsedRow[] = [];

      rows.forEach((row, idx) => {
        const sheetNo = String(pick(row, ['sheetno', 'sheetnumber', 'sheet']) ?? '').trim();
        if (!sheetNo) {
          errors.push(`Row ${idx + 2}: missing Sheet No`);
          return;
        }
        const customerName = nullableString(pick(row, ['customer', 'customername']));
        const matchedCustomer = customerName
          ? customers.find(c => c.name.toLowerCase() === customerName.toLowerCase())
          : null;
        parsed.push({
          sheetNo,
          orderNo: nullableString(pick(row, ['orderno', 'ordernumber'])),
          customerName,
          _customerId: matchedCustomer?.id ?? null,
          glassType: nullableString(pick(row, ['glasstype', 'type'])),
          thickness: nullableNumber(pick(row, ['thickness'])),
          width: nullableNumber(pick(row, ['width'])),
          height: nullableNumber(pick(row, ['height'])),
          quantity: Math.max(1, Math.floor(Number(pick(row, ['quantity', 'qty']) ?? 1)) || 1),
          remarks: nullableString(pick(row, ['remarks', 'remark', 'notes']))
        });
      });

      this.importParsed.set(parsed);
      this.importErrors.set(errors);
    } catch (err) {
      this.toastError(err, 'Could not read the file.');
    } finally {
      // Reset the input so the same file can be picked again
      input.value = '';
    }
  }

  protected async submitImport(): Promise<void> {
    const rows = this.importParsed();
    if (rows.length === 0) return;
    this.importing.set(true);
    try {
      const payload: SheetCreateInput[] = rows.map(r => ({
        sheetNo: r.sheetNo,
        orderNo: r.orderNo,
        customerId: r._customerId,
        glassType: r.glassType,
        thickness: r.thickness,
        width: r.width,
        height: r.height,
        quantity: r.quantity,
        remarks: r.remarks
      }));
      const res = await this.sheets.bulkCreate(payload);
      const skippedDetail = res.skipped > 0 ? ` ${res.skipped} skipped.` : '';
      this.toast.add({
        severity: 'success', summary: 'Import complete',
        detail: `${res.created} sheets imported.${skippedDetail}`,
        life: 4000
      });
      this.importOpen.set(false);
      const sf = this.currentShopfloor();
      if (sf?.isStorage) void this.sheets.listByShopfloor(sf.id);
      void this.shopfloors.list();
    } catch (err) {
      this.toastError(err, 'Import failed.');
    } finally {
      this.importing.set(false);
    }
  }

  // ============== DELETE ==============

  protected onDelete(sheet: GlassSheet): void {
    this.confirm.confirm({
      header: 'Delete sheet?',
      message: `Sheet ${sheet.sheetNo} will be permanently removed.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Delete', rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-text p-button-secondary',
      accept: async () => {
        try {
          await this.sheets.remove(sheet.id);
          this.toast.add({ severity: 'success', summary: 'Sheet deleted', detail: sheet.sheetNo, life: 2500 });
          void this.shopfloors.list();
        } catch (err) {
          this.toastError(err, 'Could not delete sheet.');
        }
      }
    });
  }

  protected clearSearch(): void { this.search.set(''); }

  protected statusSeverity(status: string | null): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (status) {
      case 'Completed': return 'success';
      case 'Delivered': return 'info';
      case 'InProcess': return 'info';
      case 'Hold': return 'warn';
      case 'Rejected': return 'danger';
      default: return 'secondary';
    }
  }

  protected addHasError(control: string, error: string): boolean {
    const c = this.addForm.get(control);
    return !!c && c.touched && c.hasError(error);
  }

  private toastError(err: unknown, fallback: string): void {
    const msg = err instanceof HttpErrorResponse
      ? (err.error?.error ?? err.message ?? fallback)
      : fallback;
    this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
  }
}

function pick(row: Record<string, unknown>, keys: string[]): unknown {
  const norm = (s: string) => s.toLowerCase().replace(/[\s_\-./]+/g, '');
  for (const k of Object.keys(row)) {
    const nk = norm(k);
    if (keys.some(target => norm(target) === nk)) return row[k];
  }
  return null;
}

function nullableString(v: unknown): string | null {
  if (v === null || v === undefined) return null;
  const s = String(v).trim();
  return s.length === 0 ? null : s;
}

function nullableNumber(v: unknown): number | null {
  if (v === null || v === undefined || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}
