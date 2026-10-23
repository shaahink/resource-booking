import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiClient } from '../../core/api-client';
import { AssistantParseResponse, AssistantProposal, CreateBookingRequest, Resource, ResourceType, toRuleProblem } from '../../core/models';
import { matchesQuery } from '../../core/search';
import { SearchBoxComponent } from '../../shared/search-box.component';

@Component({
  selector: 'app-browse',
  imports: [RouterLink, DatePipe, SearchBoxComponent],
  templateUrl: './browse.component.html',
  styleUrl: './browse.component.scss',
})
export class BrowseComponent {
  private readonly api = inject(ApiClient);

  protected readonly resources = signal<Resource[]>([]);
  protected readonly filter = signal<ResourceType | 'All'>('All');
  protected readonly filters: (ResourceType | 'All')[] = ['All', 'Room', 'Car', 'Catering'];
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
  protected readonly bookedMessage = signal<string | null>(null);

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
    this.bookedMessage.set(null);

    this.api.parseBookingRequest(text).subscribe({
      next: (result) => {
        this.assistantResult.set(result);
        this.assistantBusy.set(false);
      },
      error: (err: { status?: number }) => {
        this.assistantError.set(
          err.status === 503
            ? 'The assistant is not configured on this deployment.'
            : 'The assistant could not process that — try the grid below.',
        );
        this.assistantBusy.set(false);
      },
    });
  }

  protected book(proposal: AssistantProposal): void {
    const request: CreateBookingRequest = {
      resourceId: proposal.resourceId,
      start: proposal.start,
      end: proposal.end,
      quantity: proposal.quantity,
      notes: 'Booked via assistant',
    };
    this.api.createBooking(request).subscribe({
      next: (booking) => {
        this.assistantResult.set(null);
        this.assistantText.set('');
        this.bookedMessage.set(
          `Booked ${booking.resourceName}, ${new Date(booking.start).toLocaleString([], {
            weekday: 'short',
            day: 'numeric',
            month: 'short',
            hour: '2-digit',
            minute: '2-digit',
          })}.`,
        );
      },
      error: (err: unknown) => {
        const problem = toRuleProblem(err);
        this.assistantError.set(
          problem?.violations.map((v) => v.message).join(' ') ?? 'Booking failed — try the grid.',
        );
      },
    });
  }
}
