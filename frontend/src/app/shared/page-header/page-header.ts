import { Component, Input } from '@angular/core';

/**
 * Professional page title bar used on every full-page module screen.
 *
 * Slots (content-projected):
 *   `[chips]`   — small stat pills shown next to the subtitle (e.g. "12 total · 8 active")
 *   `[actions]` — primary action buttons on the right side (Add, Export, …)
 *
 * Inputs are deliberately strings, not template refs — every page has the same
 * shape (icon → crumb → title → subtitle) so making them properties keeps call sites
 * uniform and prevents accidental drift in copy / casing.
 */
@Component({
  selector: 'app-page-header',
  standalone: true,
  templateUrl: './page-header.html',
  styleUrl: './page-header.scss'
})
export class PageHeaderComponent {
  /** PrimeIcons class (without the `pi pi-` prefix). Example: `database`, `chart-bar`. */
  @Input() icon = 'pi-th-large';

  /** Top-level breadcrumb section, e.g. "Masters", "Reports", "Administration". */
  @Input() category: string | null = null;

  /** Sub-section name, e.g. "Customer Master". Rendered after a `/` separator. */
  @Input() section: string | null = null;

  /** Bold page title, e.g. "Customers". */
  @Input() title = '';

  /** One-sentence description shown below the title. */
  @Input() subtitle: string | null = null;

  /**
   * Optional accent tone for the icon badge background. Falls back to the primary
   * theme color. Useful when a page has its own brand color (Platform = global blue,
   * Billing = green, etc.).
   */
  @Input() accent: string | null = null;
}
