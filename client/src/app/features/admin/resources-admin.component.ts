import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client';
import { describeConstraint } from '../../core/constraints';
import {
  ConstraintType,
  Resource,
  ResourceConstraint,
  ResourceType,
  SaveResourceRequest,
} from '../../core/models';
import { matchesQuery } from '../../core/search';
import { SearchBoxComponent } from '../../shared/search-box.component';
import {
  ConstraintDraft,
  PARAM_MAX,
  WEEKDAY_KEYS,
  constraintDraftValid,
  isWholeInRange,
  numericParam,
  toConstraintDraft,
  toParamsJson,
} from './constraint-draft';

/** The scalar fields, edited in place in the table row. Rules and description live in the drawer. */
interface RowDraft {
  id: string;
  name: string;
  type: ResourceType;
  capacity: number | null;
  seats: number | null;
  location: string;
  isActive: boolean;
}

interface ResourceDraft {
  id: string | null;
  name: string;
  type: ResourceType;
  capacity: number | null;
  seats: number | null;
  location: string;
  description: string;
  isActive: boolean;
  constraints: ConstraintDraft[];
}

/** Capacity and seats: whole numbers from 1 to the server's bound of 10,000; seats optional. */
function unitsValid(capacity: number | null, seats: number | null): boolean {
  return isWholeInRange(capacity, 10_000) && (seats === null || isWholeInRange(seats, 10_000));
}

@Component({
  selector: 'app-resources-admin',
  imports: [DatePipe, FormsModule, SearchBoxComponent],
  templateUrl: './resources-admin.component.html',
  styleUrl: './resources-admin.component.scss',
})
export class ResourcesAdminComponent {
  private readonly api = inject(ApiClient);

  protected readonly resources = signal<Resource[]>([]);
  protected readonly search = signal('');
  protected readonly rowDraft = signal<RowDraft | null>(null);
  protected readonly draft = signal<ResourceDraft | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly weekdayKeys = WEEKDAY_KEYS;
  protected readonly paramMax = PARAM_MAX;
  protected readonly numericParam = numericParam;
  protected readonly constraintValid = constraintDraftValid;
  protected readonly resourceTypes: ResourceType[] = ['Room', 'Car', 'Catering'];
  protected readonly constraintTypes: ConstraintType[] = [
    'MaxDurationMinutes',
    'MaxAdvanceDays',
    'MinNoticeMinutes',
    'OpeningHours',
    'MaxConcurrentPerUser',
    'MaxActiveBookingsPerUser',
  ];

  protected readonly visible = computed(() =>
    this.resources().filter((r) => matchesQuery(this.search(), r.name, r.location, r.type)),
  );

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.getAdminResources().subscribe((resources) => this.resources.set(resources));
  }

  protected constraintLabel(type: ConstraintType): string {
    return {
      MaxDurationMinutes: 'Max duration (minutes)',
      MaxAdvanceDays: 'Max days in advance',
      MinNoticeMinutes: 'Min notice (minutes)',
      OpeningHours: 'Opening hours',
      MaxConcurrentPerUser: 'Max overlapping per user',
      MaxActiveBookingsPerUser: 'Max upcoming per user',
    }[type];
  }

  protected constraintChip(constraint: ResourceConstraint): string {
    return describeConstraint(constraint);
  }

  protected rawSummary(draft: ConstraintDraft): string {
    return describeConstraint({ type: draft.type, paramsJson: draft.raw ?? '' });
  }

  // ── inline row editing: the scalar fields, saved against the row's existing rules ──

  protected startRowEdit(resource: Resource): void {
    this.error.set(null);
    this.draft.set(null);
    this.rowDraft.set({
      id: resource.id,
      name: resource.name,
      type: resource.type,
      capacity: resource.capacity,
      seats: resource.seats,
      location: resource.location ?? '',
      isActive: resource.isActive,
    });
  }

  protected rowValid(row: RowDraft): boolean {
    return row.name.trim().length > 0 && unitsValid(row.capacity, row.seats);
  }

  protected saveRow(): void {
    const row = this.rowDraft();
    const source = this.resources().find((r) => r.id === row?.id);
    if (!row || !source || this.saving() || !this.rowValid(row)) return;
    this.saving.set(true);
    this.error.set(null);

    const request: SaveResourceRequest = {
      name: row.name.trim(),
      type: row.type,
      capacity: row.capacity ?? 1,
      seats: row.seats || null,
      location: row.location.trim() || null,
      description: source.description,
      isActive: row.isActive,
      constraints: source.constraints.map((c) => ({ type: c.type, paramsJson: c.paramsJson })),
    };

    this.api.updateResource(row.id, request).subscribe({
      next: () => {
        this.saving.set(false);
        this.rowDraft.set(null);
        this.load();
      },
      error: (err: { error?: { title?: string } }) => {
        this.saving.set(false);
        this.error.set(err.error?.title ?? 'Could not save the resource.');
      },
    });
  }

  // ── drawer: the full editor, including description and booking rules ──

  protected startNew(): void {
    this.error.set(null);
    this.rowDraft.set(null);
    this.draft.set({
      id: null,
      name: '',
      type: 'Room',
      capacity: 1,
      seats: null,
      location: '',
      description: '',
      isActive: true,
      constraints: [],
    });
  }

  protected startEdit(resource: Resource): void {
    this.error.set(null);
    this.rowDraft.set(null);
    this.draft.set({
      id: resource.id,
      name: resource.name,
      type: resource.type,
      capacity: resource.capacity,
      seats: resource.seats,
      location: resource.location ?? '',
      description: resource.description ?? '',
      isActive: resource.isActive,
      constraints: resource.constraints.map((c) => toConstraintDraft(c.type, c.paramsJson)),
    });
  }

  protected addConstraint(type: string): void {
    const draft = this.draft();
    if (!draft || !(this.constraintTypes as string[]).includes(type)) return;
    draft.constraints.push(toConstraintDraft(type as ConstraintType, null));
  }

  protected removeConstraint(index: number): void {
    this.draft()?.constraints.splice(index, 1);
  }

  protected draftValid(draft: ResourceDraft): boolean {
    return (
      draft.name.trim().length > 0 &&
      unitsValid(draft.capacity, draft.seats) &&
      draft.constraints.every(constraintDraftValid)
    );
  }

  protected save(): void {
    const draft = this.draft();
    if (!draft || this.saving() || !this.draftValid(draft)) return;
    this.saving.set(true);
    this.error.set(null);

    const request: SaveResourceRequest = {
      name: draft.name.trim(),
      type: draft.type,
      capacity: draft.capacity ?? 1,
      seats: draft.seats || null,
      location: draft.location.trim() || null,
      description: draft.description.trim() || null,
      isActive: draft.isActive,
      constraints: draft.constraints.map((c) => ({ type: c.type, paramsJson: toParamsJson(c) })),
    };

    const call = draft.id
      ? this.api.updateResource(draft.id, request)
      : this.api.createResource(request);

    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.draft.set(null);
        this.load();
      },
      error: (err: { error?: { title?: string } }) => {
        this.saving.set(false);
        this.error.set(err.error?.title ?? 'Could not save the resource.');
      },
    });
  }
}
