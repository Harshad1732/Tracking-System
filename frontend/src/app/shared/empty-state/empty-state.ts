import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';

/**
 * Friendly empty / no-data placeholder used inside every master / report panel.
 * Two flavors driven by `[subtle]`:
 *   - default — "No X yet, add one" with primary CTA
 *   - subtle  — "No matches for {term}" with secondary outlined CTA
 *
 * Use as:
 *   <app-empty-state icon="pi-building" title="No plants yet"
 *                    body="Add your first plant…"
 *                    ctaLabel="Add first plant" ctaIcon="pi pi-plus"
 *                    (cta)="openAdd()" />
 */
@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [ButtonModule],
  templateUrl: './empty-state.html',
  styleUrl: './empty-state.scss'
})
export class EmptyStateComponent {
  /** PrimeIcons class without the `pi pi-` prefix, e.g. `building`, `search`. */
  @Input() icon = 'pi-inbox';
  @Input() title = '';
  @Input() body: string | null = null;
  @Input() ctaLabel: string | null = null;
  @Input() ctaIcon: string | null = null;
  /** When true, renders the smaller "search returned nothing" variant. */
  @Input() subtle = false;
  @Output() cta = new EventEmitter<void>();
}
