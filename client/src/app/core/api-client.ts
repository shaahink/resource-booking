import { Observable } from 'rxjs';
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
  SaveResourceRequest,
  SaveUserRequest,
  User,
} from './models';

/**
 * The injection seam between features and data. `ApiService` implements it over HTTP;
 * `MockApiService` implements it in memory so feature work and tests never need the backend.
 * Which one is live is decided once, in app.config.ts, by USE_MOCKS.
 */
export abstract class ApiClient {
  // auth
  abstract getLoginUsers(): Observable<User[]>;
  abstract login(userId: string): Observable<User>;
  abstract logout(): Observable<void>;
  abstract me(): Observable<User>;

  // resources & booking
  abstract getResources(type?: ResourceType): Observable<Resource[]>;
  abstract getResource(id: string): Observable<Resource>;
  abstract getAvailability(resourceId: string, from: string, to: string): Observable<Availability>;
  abstract createBooking(request: CreateBookingRequest): Observable<Booking>;
  abstract cancelBooking(id: string): Observable<void>;
  abstract getMyBookings(): Observable<Booking[]>;

  // notifications
  abstract getNotifications(): Observable<NotificationItem[]>;
  abstract markNotificationRead(id: string): Observable<void>;

  // admin
  abstract getAdminResources(): Observable<Resource[]>; // includes inactive
  abstract createResource(request: SaveResourceRequest): Observable<Resource>;
  abstract updateResource(id: string, request: SaveResourceRequest): Observable<Resource>;
  abstract getUsers(): Observable<User[]>;
  abstract createUser(request: SaveUserRequest): Observable<User>;
  abstract updateUser(id: string, request: SaveUserRequest): Observable<User>;
  abstract getBookingHistory(params: {
    resourceId?: string;
    userId?: string;
    from?: string;
    to?: string;
  }): Observable<Booking[]>;
  abstract getDashboard(from: string, to: string): Observable<Dashboard>;
  abstract getInsightsSummary(): Observable<InsightsSummary>;
  abstract resetDemoData(): Observable<void>;

  // AI assistant
  abstract parseBookingRequest(text: string): Observable<AssistantParseResponse>;
}
