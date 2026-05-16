import { Component, EventEmitter, Input, Output } from '@angular/core';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';

/**
 * Single-source-of-truth search input. Drop-in replacement for the 8 copies of
 *   <p-iconfield><p-inputicon/><input/></p-iconfield>
 * scattered across master / report / admin pages.
 *
 * Use as: `<app-search-input [(value)]="search" placeholder="..."/>`
 */
@Component({
  selector: 'app-search-input',
  standalone: true,
  imports: [IconFieldModule, InputIconModule, InputTextModule],
  templateUrl: './search-input.html',
  styleUrl: './search-input.scss'
})
export class SearchInputComponent {
  @Input() value = '';
  @Output() valueChange = new EventEmitter<string>();
  @Input() placeholder = 'Search…';

  onInput(ev: Event): void {
    this.value = (ev.target as HTMLInputElement).value;
    this.valueChange.emit(this.value);
  }

  clear(): void {
    this.value = '';
    this.valueChange.emit('');
  }
}
