import { Component, input, model } from '@angular/core';

/** Free-text filter box used across the list pages; the filtering itself stays in each page. */
@Component({
  selector: 'app-search-box',
  templateUrl: './search-box.component.html',
  styleUrl: './search-box.component.scss',
})
export class SearchBoxComponent {
  readonly value = model('');
  readonly placeholder = input('Search…');

  protected onInput(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
  }
}
