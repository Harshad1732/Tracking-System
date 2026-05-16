import { Directive, Input, OnDestroy, TemplateRef, ViewContainerRef, effect, inject } from '@angular/core';
import { AuthService } from '../auth/auth.service';

/**
 * Structural directive — renders the host element only when the current user has the
 * given permission. Reactive to auth signal changes.
 *
 * Use as:
 *   <p-button *hasPerm="['Sheets', 'Add']" label="Add Sheet" />
 *   <button   *hasPerm="['Users', 'Delete']" (click)="remove()">Remove</button>
 *
 * Backward-compat shortcut — accepts a single string treated as `[<resource>, 'View']`:
 *   <a *hasPerm="'Reports'">View reports</a>
 */
@Directive({
  selector: '[hasPerm]',
  standalone: true
})
export class HasPermDirective implements OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly tpl = inject(TemplateRef<unknown>);
  private readonly vcr = inject(ViewContainerRef);

  private resource = '';
  private action = 'View';
  private rendered = false;

  @Input()
  set hasPerm(value: [string, string] | string) {
    if (typeof value === 'string') {
      this.resource = value;
      this.action = 'View';
    } else {
      [this.resource, this.action] = value;
    }
    this.evaluate();
  }

  constructor() {
    // Re-evaluate whenever the auth user signal changes (login, refresh, plant switch).
    effect(() => {
      // Touching the user() signal here registers the dependency.
      this.auth.user();
      this.evaluate();
    });
  }

  private evaluate(): void {
    const allow = this.auth.has(this.resource, this.action);
    if (allow && !this.rendered) {
      this.vcr.createEmbeddedView(this.tpl);
      this.rendered = true;
    } else if (!allow && this.rendered) {
      this.vcr.clear();
      this.rendered = false;
    }
  }

  ngOnDestroy(): void {
    this.vcr.clear();
  }
}
