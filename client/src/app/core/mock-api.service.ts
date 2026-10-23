import { Injectable } from '@angular/core';
import { Observable, delay, of, throwError } from 'rxjs';
import { ApiClient } from './api-client';
import { MOCK_RESOURCES, MOCK_USERS, generateMockBookings } from './mock-data';
import {
  AssistantParseResponse,
  Availability,
  Booking,
  CreateBookingRequest,
  Dashboard,
  InsightsSummary,
  NotificationItem,
  Resource,
  ResourceType,
  RuleViolation,
  SaveResourceRequest,
  SaveUserRequest,
  User,
} from './models';

/**
 * In-memory ApiClient for feature development and tests. Approximates the server's
 * BookingRules (overlap + capacity only) so the booking UX — including rejection paths —
 * is buildable without the backend. The real engine lives server-side; this never ships.
 */
@Injectable()
export class MockApiService extends ApiClient {
  private users = [...MOCK_USERS];
  private resources: Resource[] = structuredClone(MOCK_RESOURCES);
  private bookings = generateMockBookings();
  private notifications: NotificationItem[] = [
    {
      id: 'n1',
      type: 'booking_cancelled',
      message: 'Your Boardroom booking on Monday was cancelled by Priya Sharma.',
      bookingId: null,
      createdOn: new Date(Date.now() - 3600_000).toISOString(),
      readAt: null,
    },
  ];
  private currentUser: User | null = null;
  private seq = 1000;

  private ok<T>(value: T): Observable<T> {
    return of(structuredClone(value)).pipe(delay(150));
  }

  private problem(status: number, title: string, violations: RuleViolation[]): Observable<never> {
    return throwError(() => ({ status, error: { title, status, violations } })).pipe(delay(150)) as Observable<never>;
  }

  // auth
  getLoginUsers(): Observable<User[]> {
    return this.ok(this.users.filter((u) => u.isActive).sort((a, b) => a.name.localeCompare(b.name)));
  }
  login(userId: string): Observable<User> {
    const user = this.users.find((u) => u.id === userId && u.isActive);
    if (!user) return this.problem(401, 'Unknown user', []);
    this.currentUser = user;
    return this.ok(user);
  }
  logout(): Observable<void> {
    this.currentUser = null;
    return this.ok(undefined);
  }
  me(): Observable<User> {
    return this.currentUser ? this.ok(this.currentUser) : this.problem(401, 'Not signed in', []);
  }

  // resources & booking
  getResources(type?: ResourceType): Observable<Resource[]> {
    return this.ok(this.resources.filter((r) => r.isActive && (!type || r.type === type)));
  }
  getResource(id: string): Observable<Resource> {
    const resource = this.resources.find((r) => r.id === id);
    return resource ? this.ok(resource) : this.problem(404, 'Resource not found', []);
  }
  getAvailability(resourceId: string, from: string, to: string): Observable<Availability> {
    const resource = this.resources.find((r) => r.id === resourceId);
    if (!resource) return this.problem(404, 'Resource not found', []);
    const bookings = this.bookings
      .filter(
        (b) =>
          b.resourceId === resourceId &&
          b.status === 'Confirmed' &&
          b.start < to &&
          b.end > from,
      )
      .map((b) => ({
        id: b.id,
        start: b.start,
        end: b.end,
        quantity: b.quantity,
        mine: b.userId === this.currentUser?.id,
      }));
    return this.ok({ resourceId, capacity: resource.capacity, bookings });
  }
  createBooking(request: CreateBookingRequest): Observable<Booking> {
    const resource = this.resources.find((r) => r.id === request.resourceId);
    const user = this.currentUser;
    if (!resource || !user) return this.problem(401, 'Not signed in', []);

    const violations: RuleViolation[] = [];
    if (request.end <= request.start) {
      violations.push({ code: 'invalid_range', message: 'End must be after start.' });
    }
    const overlapping = this.bookings.filter(
      (b) =>
        b.resourceId === resource.id &&
        b.status === 'Confirmed' &&
        b.start < request.end &&
        b.end > request.start,
    );
    const used = overlapping.reduce((sum, b) => sum + b.quantity, 0);
    if (used + request.quantity > resource.capacity) {
      violations.push(
        resource.capacity === 1
          ? { code: 'overlap_conflict', message: 'This slot is already booked.' }
          : { code: 'capacity_exceeded', message: `Only ${resource.capacity - used} of ${resource.capacity} units are free in this window.` },
      );
    }
    if (violations.length > 0) return this.problem(409, 'Booking rejected', violations);

    const booking: Booking = {
      id: `b${++this.seq}`,
      resourceId: resource.id,
      resourceName: resource.name,
      resourceType: resource.type,
      userId: user.id,
      userName: user.name,
      start: request.start,
      end: request.end,
      quantity: request.quantity,
      status: 'Confirmed',
      notes: request.notes,
      createdOn: new Date().toISOString(),
    };
    this.bookings.push(booking);
    return this.ok(booking);
  }
  cancelBooking(id: string): Observable<void> {
    const booking = this.bookings.find((b) => b.id === id);
    if (booking) booking.status = 'Cancelled';
    return this.ok(undefined);
  }
  getMyBookings(): Observable<Booking[]> {
    return this.ok(this.bookings.filter((b) => b.userId === this.currentUser?.id));
  }

  // notifications
  getNotifications(): Observable<NotificationItem[]> {
    return this.ok(this.notifications);
  }
  markNotificationRead(id: string): Observable<void> {
    const item = this.notifications.find((x) => x.id === id);
    if (item) item.readAt = new Date().toISOString();
    return this.ok(undefined);
  }

  // admin
  getAdminResources(): Observable<Resource[]> {
    return this.ok(this.resources);
  }
  createResource(request: SaveResourceRequest): Observable<Resource> {
    const resource: Resource = {
      id: `r${++this.seq}`,
      ...request,
      createdOn: new Date().toISOString(),
      constraints: request.constraints.map((c, i) => ({ id: `c${this.seq}-${i}`, ...c })),
    };
    this.resources.push(resource);
    return this.ok(resource);
  }
  updateResource(id: string, request: SaveResourceRequest): Observable<Resource> {
    const index = this.resources.findIndex((r) => r.id === id);
    if (index < 0) return this.problem(404, 'Resource not found', []);
    this.resources[index] = {
      id,
      ...request,
      createdOn: this.resources[index].createdOn,
      constraints: request.constraints.map((c, i) => ({ id: `c${id}-${i}`, ...c })),
    };
    return this.ok(this.resources[index]);
  }
  getUsers(): Observable<User[]> {
    return this.ok(this.users);
  }
  createUser(request: SaveUserRequest): Observable<User> {
    if (this.users.some((u) => u.email === request.email)) {
      return this.problem(409, `A user with email ${request.email} already exists.`, []);
    }
    const user: User = { id: `u${++this.seq}`, ...request, createdOn: new Date().toISOString() };
    this.users.push(user);
    return this.ok(user);
  }
  updateUser(id: string, request: SaveUserRequest): Observable<User> {
    const index = this.users.findIndex((u) => u.id === id);
    if (index < 0) return this.problem(404, 'User not found', []);
    if (this.users.some((u) => u.email === request.email && u.id !== id)) {
      return this.problem(409, `A user with email ${request.email} already exists.`, []);
    }
    this.users[index] = { id, ...request, createdOn: this.users[index].createdOn };
    return this.ok(this.users[index]);
  }
  getBookingHistory(filter: {
    resourceId?: string;
    userId?: string;
    from?: string;
    to?: string;
  }): Observable<Booking[]> {
    return this.ok(
      this.bookings.filter(
        (b) =>
          (!filter.resourceId || b.resourceId === filter.resourceId) &&
          (!filter.userId || b.userId === filter.userId) &&
          (!filter.from || b.start >= filter.from) &&
          (!filter.to || b.start <= filter.to),
      ),
    );
  }
  getDashboard(from: string, to: string): Observable<Dashboard> {
    const inRange = this.bookings.filter(
      (b) => b.status === 'Confirmed' && b.start >= from && b.start <= to,
    );
    const hours = (b: Booking): number =>
      (new Date(b.end).getTime() - new Date(b.start).getTime()) / 3600_000;

    const utilisation = this.resources.map((resource) => {
      const own = inRange.filter((b) => b.resourceId === resource.id);
      const bookedHours = own.reduce((sum, b) => sum + hours(b), 0);
      const availableHours = 50; // mock: ~10 bookable hours × 5 weekdays
      return {
        resourceId: resource.id,
        name: resource.name,
        type: resource.type,
        bookedHours: Math.round(bookedHours * 10) / 10,
        availableHours,
        utilisationPct: Math.min(100, Math.round((bookedHours / availableHours) * 100)),
        bookingsCount: own.length,
      };
    });

    const peakHours = Array.from({ length: 11 }, (_, i) => ({
      hour: 8 + i,
      count: inRange.filter((b) => new Date(b.start).getHours() === 8 + i).length,
    }));

    const topUsers = this.users
      .map((u) => ({
        userId: u.id,
        name: u.name,
        count: inRange.filter((b) => b.userId === u.id).length,
      }))
      .filter((x) => x.count > 0)
      .sort((a, b) => b.count - a.count)
      .slice(0, 5);

    return this.ok({
      utilisation,
      peakHours,
      topUsers,
      bookingsInRange: inRange.length,
      overallUtilisationPct: Math.round(
        utilisation.reduce((sum, u) => sum + u.utilisationPct, 0) / Math.max(1, utilisation.length),
      ),
    });
  }
  getInsightsSummary(): Observable<InsightsSummary> {
    return this.ok({
      headline: 'Meeting rooms ran at moderate load this week; cars and catering have headroom.',
      highlights: [
        'Boardroom is the clear favourite, busiest in the late mornings.',
        'Workshop Room is underused for its size.',
        'Catering demand is steady at 15–20 portions a day.',
      ],
      suggestion: 'Promote the Workshop Room for team sessions to spread the late-morning peak.',
      generatedAt: new Date().toISOString(),
    });
  }
  resetDemoData(): Observable<void> {
    this.users = [...MOCK_USERS];
    this.resources = structuredClone(MOCK_RESOURCES);
    this.bookings = generateMockBookings();
    this.notifications = [];
    return this.ok(undefined);
  }

  // AI assistant
  parseBookingRequest(text: string): Observable<AssistantParseResponse> {
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    tomorrow.setHours(14, 0, 0, 0);
    const end = new Date(tomorrow.getTime() + 60 * 60_000);
    return this.ok({
      summary: `Mock parse of “${text}” — proposing free rooms tomorrow 14:00–15:00.`,
      proposals: this.resources
        .filter((r) => r.type === 'Room')
        .slice(0, 2)
        .map((r) => ({
          resourceId: r.id,
          resourceName: r.name,
          start: tomorrow.toISOString(),
          end: end.toISOString(),
          quantity: 1,
        })),
    });
  }
}
