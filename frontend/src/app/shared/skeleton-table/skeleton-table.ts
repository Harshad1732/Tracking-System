import { Component, Input, computed, signal } from '@angular/core';
import { SkeletonModule } from 'primeng/skeleton';

export interface SkeletonColumn {
  /** Column header label. */
  label: string;
  /** Optional skeleton width (default 6rem). */
  width?: string;
  /** If true, renders two circle skeletons (icon-button row). Use for "Actions" column. */
  actions?: boolean;
}

/**
 * Drop-in replacement for the hand-rolled `<table class="m-skeleton-table">` block.
 *
 * Use as:
 *   <app-skeleton-table [columns]="[
 *     { label: 'Code', width: '4rem' },
 *     { label: 'Name', width: '9rem' },
 *     { label: 'Status', width: '4rem' },
 *     { label: 'Actions', actions: true }
 *   ]" [rows]="5" />
 */
@Component({
  selector: 'app-skeleton-table',
  standalone: true,
  imports: [SkeletonModule],
  templateUrl: './skeleton-table.html',
  styleUrl: './skeleton-table.scss'
})
export class SkeletonTableComponent {
  @Input() columns: SkeletonColumn[] = [];
  @Input() rows = 5;

  protected readonly skeletonRows = computed(() => Array.from({ length: this.rows }));
  // Need a getter rather than computed because rows is an Input setter, not a signal.
  protected rowArray(): unknown[] {
    return Array.from({ length: this.rows });
  }
}
