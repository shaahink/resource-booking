import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiClient } from '../../core/api-client';
import { Availability, Booking, Resource } from '../../core/models';
import { RealtimeService } from '../../core/realtime.service';
import { GridSlot, bookingsInSlot, slotsForDay, usedQuantity, weekDays } from './grid';
import { BookingDialogComponent } from './booking-dialog.component';

interface GridCell {
  slot: GridSlot;
  used: number;
  mine: boolean;
  full: boolean;
  past: boolean;
}

interface GridDay {
  date: Date;
  cells: GridCell[];
}

const DAY_START_HOUR = 7;
const DAY_END_HOUR = 21;

@Component({
  selector: 'app-resource-detail',
  imports: [DatePipe, RouterLink, BookingDialogComponent],
  templateUrl: './resource-detail.component.html',
  styleUrl: './resource-detail.component.scss',
})
export class ResourceDetailComponent implements OnInit {
  private readonly api = inject(ApiClient);
  private readonly route = inject(ActivatedRoute);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly id = this.route.snapshot.paramMap.get('id')!;

  protected readonly resource = signal<Resource | null>(null);
  protected readonly availability = signal<Availability | null>(null);
  protected readonly anchor = signal(new Date());
  protected readonly selectedSlot = signal<Date | null>(null);
  protected readonly toast = signal<string | null>(null);

  protected readonly hourLabels = Array.from(
    { length: DAY_END_HOUR - DAY_START_HOUR },
    (_, i) => `${String(DAY_START_HOUR + i).padStart(2, '0')}:00`,
  );

  protected readonly days = computed<GridDay[]>(() => {
    const availability = this.availability();
    const bookings = availability?.bookings ?? [];
    const capacity = availability?.capacity ?? 1;
    const now = new Date();

    return weekDays(this.anchor()).map((date) => ({
      date,
      cells: slotsForDay(date, DAY_START_HOUR, DAY_END_HOUR).map((slot) => {
        const used = usedQuantity(bookings, slot);
        return {
          slot,
          used,
          mine: bookingsInSlot(bookings, slot).some((b) => b.mine),
          full: used >= capacity,
          past: slot.end <= now,
        };
      }),
    }));
  });

  ngOnInit(): void {
    this.api.getResource(this.id).subscribe((resource) => this.resource.set(resource));
    this.refetch();

    void this.realtime.connect().then(() => this.realtime.watchResource(this.id));
    this.realtime.bookingEvents$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event.resourceId === this.id) this.refetch();
      });
    this.destroyRef.onDestroy(() => void this.realtime.unwatchResource(this.id));
  }

  protected refetch(): void {
    const days = weekDays(this.anchor());
    const from = days[0];
    const to = new Date(days[6]);
    to.setDate(to.getDate() + 1);
    this.api
      .getAvailability(this.id, from.toISOString(), to.toISOString())
      .subscribe((availability) => this.availability.set(availability));
  }

  protected shiftWeek(weeks: number): void {
    const next = new Date(this.anchor());
    next.setDate(next.getDate() + weeks * 7);
    this.anchor.set(next);
    this.refetch();
  }

  protected today(): void {
    this.anchor.set(new Date());
    this.refetch();
  }

  protected select(cell: GridCell): void {
    if (cell.past || cell.full) return;
    this.selectedSlot.set(cell.slot.start);
  }

  protected onBooked(booking: Booking): void {
    this.selectedSlot.set(null);
    this.refetch();
    this.toast.set(
      `Booked ${new Date(booking.start).toLocaleString([], {
        weekday: 'short',
        hour: '2-digit',
        minute: '2-digit',
      })}–${new Date(booking.end).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}.`,
    );
    setTimeout(() => this.toast.set(null), 4000);
  }

  protected isToday(date: Date): boolean {
    return date.toDateString() === new Date().toDateString();
  }
}
