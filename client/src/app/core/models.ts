// TypeScript mirror of Api/Contracts/Dtos.cs — the frozen API contract.
// Changing anything here requires updating the C# DTOs and docs/contracts.md in the same commit.

export type UserRole = 'User' | 'Admin';
export type ResourceType = 'Room' | 'Car' | 'Catering';
export type BookingStatus = 'Confirmed' | 'Cancelled';
export type ConstraintType =
  | 'MaxDurationMinutes'
  | 'MaxAdvanceDays'
  | 'MinNoticeMinutes'
  | 'OpeningHours'
  | 'MaxConcurrentPerUser'
  | 'MaxActiveBookingsPerUser';

export interface User {
  id: string;
  name: string;
  email: string;
  role: UserRole;
}

export interface ResourceConstraint {
  id: string;
  type: ConstraintType;
  paramsJson: string;
}

export interface Resource {
  id: string;
  name: string;
  type: ResourceType;
  capacity: number;
  seats: number | null;
  location: string | null;
  description: string | null;
  isActive: boolean;
  constraints: ResourceConstraint[];
}

export interface SaveConstraintRequest {
  type: ConstraintType;
  paramsJson: string;
}

export interface SaveResourceRequest {
  name: string;
  type: ResourceType;
  capacity: number;
  seats: number | null;
  location: string | null;
  description: string | null;
  isActive: boolean;
  constraints: SaveConstraintRequest[];
}

export interface SaveUserRequest {
  name: string;
  email: string;
  role: UserRole;
}

export interface Booking {
  id: string;
  resourceId: string;
  resourceName: string;
  resourceType: ResourceType;
  userId: string;
  userName: string;
  start: string; // ISO 8601, UTC
  end: string;
  quantity: number;
  status: BookingStatus;
  notes: string | null;
  createdAt: string;
}

export interface CreateBookingRequest {
  resourceId: string;
  start: string;
  end: string;
  quantity: number;
  notes: string | null;
}

export interface AvailabilityBooking {
  id: string;
  start: string;
  end: string;
  quantity: number;
  mine: boolean;
}

export interface Availability {
  resourceId: string;
  capacity: number;
  bookings: AvailabilityBooking[];
}

export interface NotificationItem {
  id: string;
  type: string;
  message: string;
  bookingId: string | null;
  createdAt: string;
  readAt: string | null;
}

export interface ResourceUtilisation {
  resourceId: string;
  name: string;
  type: ResourceType;
  bookedHours: number;
  availableHours: number;
  utilisationPct: number;
  bookingsCount: number;
}

export interface PeakHour {
  hour: number;
  count: number;
}

export interface TopUser {
  userId: string;
  name: string;
  count: number;
}

export interface Dashboard {
  utilisation: ResourceUtilisation[];
  peakHours: PeakHour[];
  topUsers: TopUser[];
  bookingsThisWeek: number;
  overallUtilisationPct: number;
}

export interface AssistantProposal {
  resourceId: string;
  resourceName: string;
  start: string;
  end: string;
  quantity: number;
}

export interface AssistantParseResponse {
  summary: string;
  proposals: AssistantProposal[];
}

export interface InsightsSummary {
  summary: string;
  generatedAt: string;
}

// ── Errors ──

/** Machine-readable rule codes; mirror of Api/Domain/BookingRules.cs RuleCodes. */
export interface RuleViolation {
  code: string;
  message: string;
}

/** RFC 7807 ProblemDetails with our `violations` extension (409 conflict / 422 rule failure). */
export interface RuleProblem {
  title: string;
  status: number;
  violations: RuleViolation[];
}

/** Narrow an HttpErrorResponse body to a RuleProblem, or null when it is not one. */
export function toRuleProblem(error: unknown): RuleProblem | null {
  const body = (error as { error?: unknown })?.error;
  if (body && typeof body === 'object' && 'violations' in body) {
    return body as RuleProblem;
  }
  return null;
}
