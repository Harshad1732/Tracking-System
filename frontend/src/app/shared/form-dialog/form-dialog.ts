import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';

/**
 * Modal dialog shell for "Add / Edit" forms. Owns the open state, the header
 * label switch, and the Cancel + Save footer — every master uses the same shape.
 *
 * Use as:
 *   <app-form-dialog
 *     [(open)]="dialogOpen" [editing]="!!editing()" [saving]="store.saving()"
 *     [valid]="form.valid" entityLabel="Plant" width="520px" (save)="submit()">
 *     <form [formGroup]="form" class="m-form"> ... fields ... </form>
 *   </app-form-dialog>
 */
@Component({
  selector: 'app-form-dialog',
  standalone: true,
  imports: [DialogModule, ButtonModule],
  templateUrl: './form-dialog.html',
  styleUrl: './form-dialog.scss'
})
export class FormDialogComponent {
  @Input() open = false;
  @Output() openChange = new EventEmitter<boolean>();

  /** True when editing an existing row (changes header + button label/icon). */
  @Input() editing = false;
  /** Disables Cancel/Save and prevents close while a request is in flight. */
  @Input() saving = false;
  /** When false, the Save button is disabled. */
  @Input() valid = true;
  /** Used to build "Add X" / "Edit X" / "Add X" button label by default. */
  @Input() entityLabel = 'Item';
  /** Override the dialog header entirely. */
  @Input() headerOverride: string | null = null;
  /** Override the save button label entirely. */
  @Input() saveLabelOverride: string | null = null;
  /** Dialog width — accepts CSS values like '520px' or 'min(880px, 95vw)'. */
  @Input() width = '520px';
  /** Hide the Save button entirely (read-only viewer dialogs). Cancel becomes "Close". */
  @Input() showSave = true;

  @Output() save = new EventEmitter<void>();

  protected get header(): string {
    return this.headerOverride ?? (this.editing ? `Edit ${this.entityLabel}` : `Add ${this.entityLabel}`);
  }

  protected get saveLabel(): string {
    return this.saveLabelOverride ?? (this.editing ? 'Save changes' : `Add ${this.entityLabel}`);
  }

  protected get saveIcon(): string {
    return this.editing ? 'pi pi-check' : 'pi pi-plus';
  }

  protected close(): void {
    this.openChange.emit(false);
  }
}
