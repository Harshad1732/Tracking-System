import { Component, computed, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { toSignal } from '@angular/core/rxjs-interop';

export interface PlaceholderConfig {
  title: string;
  icon: string;
  description: string;
  bullets?: string[];
}

@Component({
  selector: 'app-placeholder-page',
  imports: [ButtonModule],
  templateUrl: './placeholder-page.html',
  styleUrl: './placeholder-page.scss'
})
export class PlaceholderPage {
  private readonly route = inject(ActivatedRoute);
  private readonly data = toSignal(this.route.data, { initialValue: this.route.snapshot.data });

  protected readonly config = computed<PlaceholderConfig>(() => {
    const d = this.data() as { placeholder?: PlaceholderConfig };
    return d.placeholder ?? {
      title: 'Coming soon',
      icon: 'pi pi-clock',
      description: 'This screen is under construction.'
    };
  });
}
