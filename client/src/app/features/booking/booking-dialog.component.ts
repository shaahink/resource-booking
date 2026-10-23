import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { ApiClient } from '../../core/api-client';
import { Booking, Resource, RuleViolation, toRuleProblem } from '../../core/models';

/**
 * Confirmation dialog for one slot. The server is the authority on every rule —
 * this dialog's job is to collect duration/quantity/notes and to render the
 * violations that come back, not to pre-judge them.
 */
@Component({
  selector: 'app-booking-dialog',
  imports: [DatePipe],
  templateUrl: './booking-dialog.component.html',
  styleUrl: './booking-dialog.component.scss',
})
export class BookingDialogComponent {
  private readonly api = inject(ApiClient);

  readonly resource = input.required<Resource>();
  readonly slotStart = input.required<Date>();

  readonly booked = output<Booking>();
  readonly closed = output<void>();

  protected readonly durations = [30, 60, 90, 120, 180, 240];
  protected readonly durationMinutes = signal(60);
  protected readonly quantity = signal(1);
  protected readonly notes = signal('');
  protected readonly busy = signal(false);
  protected readonly violations = signal<RuleViolation[]>([]);

  protected readonly end = computed(
    () => new Date(this.slotStart().getTime() + this.durationMinutes() * 60_000),
  );

  protected submit(): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.violations.set([]);

    this.api
      .createBooking({
        resourceId: this.resource().id,
        start: this.slotStart().toISOString(),
        end: this.end().toISOString(),
        quantity: this.quantity(),
        notes: this.notes().trim() || null,
      })
      .subscribe({
        next: (booking) => this.booked.emit(booking),
        error: (err: unknown) => {
          this.busy.set(false);
          const problem = toRuleProblem(err);
          this.violations.set(
            problem?.violations ?? [{ code: 'unknown', message: 'Something went wrong — please try again.' }],
          );
        },
      });
  }
}
