import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiClient } from '../../core/api-client';
import { Booking } from '../../core/models';

@Component({
  selector: 'app-my-bookings',
  imports: [DatePipe, RouterLink],
  templateUrl: './my-bookings.component.html',
  styleUrl: './my-bookings.component.scss',
})
export class MyBookingsComponent {
  private readonly api = inject(ApiClient);

  protected readonly bookings = signal<Booking[]>([]);
  protected readonly cancelling = signal<string | null>(null);

  protected readonly upcoming = computed(() =>
    this.bookings()
      .filter((b) => b.status === 'Confirmed' && new Date(b.end) > new Date())
      .sort((a, b) => a.start.localeCompare(b.start)),
  );

  protected readonly past = computed(() =>
    this.bookings()
      .filter((b) => b.status === 'Cancelled' || new Date(b.end) <= new Date())
      .sort((a, b) => b.start.localeCompare(a.start)),
  );

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.getMyBookings().subscribe((bookings) => this.bookings.set(bookings));
  }

  protected cancel(booking: Booking): void {
    if (this.cancelling()) return;
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
