import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client';
import { Booking, Resource, User } from '../../core/models';
import { matchesQuery } from '../../core/search';
import { SearchBoxComponent } from '../../shared/search-box.component';

@Component({
  selector: 'app-history',
  imports: [DatePipe, FormsModule, SearchBoxComponent],
  templateUrl: './history.component.html',
  styleUrl: './history.component.scss',
})
export class HistoryComponent {
  private readonly api = inject(ApiClient);

  protected readonly bookings = signal<Booking[]>([]);
  protected readonly resources = signal<Resource[]>([]);
  protected readonly users = signal<User[]>([]);
  protected readonly loading = signal(false);

  /** Free text, applied client-side to the loaded page of results. */
  protected readonly search = signal('');

  protected readonly visible = computed(() =>
    this.bookings().filter((b) =>
      matchesQuery(this.search(), b.resourceName, b.userName, b.notes, b.status),
    ),
  );

  // filters (template-driven; '' = any)
  protected resourceId = '';
  protected userId = '';
  protected from = '';
  protected to = '';

  constructor() {
    this.api.getAdminResources().subscribe((resources) => this.resources.set(resources));
    this.api.getUsers().subscribe((users) => this.users.set(users));
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.api
      .getBookingHistory({
        resourceId: this.resourceId || undefined,
        userId: this.userId || undefined,
        from: this.from ? new Date(this.from).toISOString() : undefined,
        to: this.to ? new Date(`${this.to}T23:59:59`).toISOString() : undefined,
      })
      .subscribe((bookings) => {
        this.bookings.set(bookings);
        this.loading.set(false);
      });
  }

  protected reset(): void {
    this.resourceId = '';
    this.userId = '';
    this.from = '';
    this.to = '';
    this.search.set('');
    this.load();
  }
}
