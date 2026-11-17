import { DatePipe } from '@angular/common';
import { HttpStatusCode } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { NotificationsService } from '../../core/notifications.service';
import {
  AssistantParseResponse,
  AssistantProposal,
  Booking,
  CreateBookingRequest,
  Resource,
  ResourceType,
  toRuleProblem,
} from '../../core/models';
import { matchesQuery } from '../../core/search';
import { SearchBoxComponent } from '../../shared/search-box.component';

@Component({
  selector: 'app-browse',
  imports: [RouterLink, DatePipe, SearchBoxComponent],
  templateUrl: './browse.component.html',
  styleUrl: './browse.component.scss',
})
export class BrowseComponent {
  private readonly api = inject(ApiService);
  private readonly notifications = inject(NotificationsService);

  protected readonly resources = signal<Resource[]>([]);
  protected readonly filters: (ResourceType | 'All')[] = ['All', 'Room', 'Car', 'Catering'];
  // ?type=Car opens the list already filtered — the quick actions on My bookings use it.
  protected readonly filter = signal<ResourceType | 'All'>(
    this.typeFrom(inject(ActivatedRoute).snapshot.queryParamMap.get('type')),
  );
  protected readonly search = signal('');

  protected readonly visible = computed(() =>
    this.resources().filter(
      (r) =>
        (this.filter() === 'All' || r.type === this.filter()) &&
        matchesQuery(this.search(), r.name, r.location, r.description),
    ),
  );

  // ── AI assistant ──
  protected readonly assistantText = signal('');
  protected readonly assistantBusy = signal(false);
  protected readonly assistantResult = signal<AssistantParseResponse | null>(null);
  protected readonly assistantError = signal<string | null>(null);
  protected readonly bookingBusy = signal(false);
  protected readonly booked = signal<Booking | null>(null);

  constructor() {
    this.api.getResources().subscribe((resources) => this.resources.set(resources));
  }

  protected typeLabel(type: ResourceType): string {
    return { Room: 'Meeting room', Car: 'Pool car', Catering: 'Catering' }[type];
  }

  protected onAssistantInput(event: Event): void {
    this.assistantText.set((event.target as HTMLInputElement).value);
  }

  protected ask(): void {
    const text = this.assistantText().trim();
    if (!text || this.assistantBusy()) return;
    this.assistantBusy.set(true);
    this.assistantError.set(null);
    this.assistantResult.set(null);
    this.booked.set(null);

    this.api.parseBookingRequest(text).subscribe({
      next: (result) => {
        this.assistantResult.set(result);
        this.assistantBusy.set(false);
      },
      error: (err: { status?: number }) => {
        this.assistantError.set(
          err.status === HttpStatusCode.ServiceUnavailable
            ? 'The assistant is not configured on this deployment.'
            : 'The assistant could not process that — try the grid below.',
        );
        this.assistantBusy.set(false);
      },
    });
  }

  private typeFrom(value: string | null): ResourceType | 'All' {
    return this.filters.find((option) => option === value) ?? 'All';
  }

  protected book(proposal: AssistantProposal): void {
    if (this.bookingBusy()) return;
    this.bookingBusy.set(true);
    this.assistantError.set(null);
    this.booked.set(null);

    const request: CreateBookingRequest = {
      resourceId: proposal.resourceId,
      start: proposal.start,
      end: proposal.end,
      quantity: proposal.quantity,
      notes: 'Booked via assistant',
    };
    this.api.createBooking(request).subscribe({
      next: (booking) => {
        this.bookingBusy.set(false);
        this.assistantResult.set(null);
        this.assistantText.set('');
        this.booked.set(booking);
        this.notifications.load();
      },
      error: (err: unknown) => {
        this.bookingBusy.set(false);
        const problem = toRuleProblem(err);
        this.assistantError.set(
          problem?.violations.map((v) => v.message).join(' ') ?? 'Booking failed — try the grid.',
        );
      },
    });
  }
}
