import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { Booking, ResourceType } from '../../core/models';
import { matchesQuery } from '../../core/search';
import { SearchBoxComponent } from '../../shared/search-box.component';
import { groupByDay, isUpcoming } from './my-bookings';

type Tab = 'upcoming' | 'past';

@Component({
  selector: 'app-my-bookings',
  imports: [DatePipe, RouterLink, SearchBoxComponent],
  templateUrl: './my-bookings.component.html',
})
export class MyBookingsComponent {
  private readonly api = inject(ApiService);

  /** A booking to point at, from a notification or a "View booking" link. */
  protected readonly highlighted = inject(ActivatedRoute).snapshot.queryParamMap.get('booking');

  protected readonly bookings = signal<Booking[]>([]);
  protected readonly cancelling = signal<string | null>(null);
  protected readonly search = signal('');
  protected readonly tab = signal<Tab>('upcoming');
  protected readonly type = signal<ResourceType | 'All'>('All');
  protected readonly types: (ResourceType | 'All')[] = ['All', 'Room', 'Car', 'Catering'];

  private readonly now = new Date();

  private readonly matching = computed(() =>
    this.bookings().filter(
      (b) =>
        (this.type() === 'All' || b.resourceType === this.type()) &&
        matchesQuery(this.search(), b.resourceName, b.notes, b.resourceType, b.status),
    ),
  );

  private readonly upcoming = computed(() =>
    this.matching()
      .filter((b) => isUpcoming(b, this.now))
      .sort((a, b) => a.start.localeCompare(b.start)),
  );

  private readonly past = computed(() =>
    this.matching()
      .filter((b) => !isUpcoming(b, this.now))
      .sort((a, b) => b.start.localeCompare(a.start)),
  );

  protected readonly counts = computed(() => ({
    upcoming: this.upcoming().length,
    past: this.past().length,
  }));

  protected readonly days = computed(() =>
    groupByDay(this.tab() === 'upcoming' ? this.upcoming() : this.past(), this.now),
  );

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.getMyBookings().subscribe((bookings) => {
      this.bookings.set(bookings);
      const target = bookings.find((b) => b.id === this.highlighted);
      if (!target) return;
      this.tab.set(isUpcoming(target, this.now) ? 'upcoming' : 'past');
      setTimeout(() =>
        document
          .getElementById(`booking-${target.id}`)
          ?.scrollIntoView({ block: 'center', behavior: 'smooth' }),
      );
    });
  }

  protected cancel(booking: Booking): void {
    if (this.cancelling()) return;
    const when = new Date(booking.start).toLocaleString([], {
      weekday: 'short',
      day: 'numeric',
      month: 'short',
      hour: '2-digit',
      minute: '2-digit',
    });
    if (!confirm(`Cancel ${booking.resourceName}, ${when}?`)) return;

    this.cancelling.set(booking.id);
    this.api.cancelBooking(booking.id).subscribe({
      next: () => {
        this.cancelling.set(null);
        this.load();
      },
      error: () => this.cancelling.set(null),
    });
  }
}
