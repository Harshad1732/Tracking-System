import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TooltipModule } from 'primeng/tooltip';

/**
 * Standard pair of Edit + Delete icon buttons for table action cells.
 * Hides each button based on the per-action capability inputs.
 *
 * Use as:
 *   <app-row-actions
 *     [canEdit]="auth.canEdit()" [canDelete]="auth.canDelete()"
 *     (edit)="openEdit(plant)" (delete)="onDelete(plant)" />
 */
@Component({
  selector: 'app-row-actions',
  standalone: true,
  imports: [TooltipModule],
  templateUrl: './row-actions.html',
  styleUrl: './row-actions.scss'
})
export class RowActionsComponent {
  @Input() canEdit = true;
  @Input() canDelete = true;
  @Input() editLabel = 'Edit';
  @Input() deleteLabel = 'Delete';
  /** Disable the delete button without hiding it (e.g. "you can't delete your own account"). */
  @Input() deleteDisabled = false;

  @Output() edit = new EventEmitter<void>();
  @Output() delete = new EventEmitter<void>();
}
