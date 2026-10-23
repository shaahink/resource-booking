import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { ApiClient } from '../../core/api-client';
import { Booking, Resource, RuleViolation, toRuleProblem } from '../../core/models';

/**
 * Confirmation dialog for one slot. The server is the authority on every rule —
 * this dialog collects duration/quantity/notes, pre-checks only what the form itself
 * can get wrong (an empty or out-of-range quantity), and renders the violations
 * that come back rather than pre-judging them.
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

  protected readonly quantityError = computed(() => {
    const capacity = this.resource().capacity;
    if (capacity === 1) return null;
    const quantity = this.quantity();
    if (!Number.isInteger(quantity) || quantity < 1) return 'Enter how many units you need.';
    if (quantity > capacity) return `This resource has ${capacity} units.`;
    return null;
  });

  protected readonly canSubmit = computed(() => !this.busy() && this.quantityError() === null);

  protected onDurationChange(event: Event): void {
    this.durationMinutes.set(Number((event.target as HTMLSelectElement).value));
  }

  protected onQuantityInput(event: Event): void {
    // An emptied number field reads as NaN; 0 keeps the field controlled and trips the error.
    const raw = (event.target as HTMLInputElement).valueAsNumber;
    this.quantity.set(Number.isFinite(raw) ? Math.trunc(raw) : 0);
  }

  protected onNotesInput(event: Event): void {
    this.notes.set((event.target as HTMLInputElement).value);
  }

  protected submit(): void {
    if (!this.canSubmit()) return;
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
