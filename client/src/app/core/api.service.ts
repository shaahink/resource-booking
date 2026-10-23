import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from './api-client';
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

/** HTTP implementation of ApiClient. URLs are relative: the dev proxy and the
 *  production same-origin hosting both resolve them without CORS. */
@Injectable()
export class ApiService extends ApiClient {
  private readonly http = inject(HttpClient);

  // auth
  getLoginUsers(): Observable<User[]> {
    return this.http.get<User[]>('/api/auth/users');
  }
  login(userId: string): Observable<User> {
    return this.http.post<User>('/api/auth/login', { userId });
  }
  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', {});
  }
  me(): Observable<User> {
    return this.http.get<User>('/api/auth/me');
  }

  // resources & booking
  getResources(type?: ResourceType): Observable<Resource[]> {
    const params = type ? new HttpParams().set('type', type) : undefined;
    return this.http.get<Resource[]>('/api/resources', { params });
  }
  getResource(id: string): Observable<Resource> {
    return this.http.get<Resource>(`/api/resources/${id}`);
  }
  getAvailability(resourceId: string, from: string, to: string): Observable<Availability> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<Availability>(`/api/resources/${resourceId}/availability`, { params });
  }
  createBooking(request: CreateBookingRequest): Observable<Booking> {
    return this.http.post<Booking>('/api/bookings', request);
  }
  cancelBooking(id: string): Observable<void> {
    return this.http.delete<void>(`/api/bookings/${id}`);
  }
  getMyBookings(): Observable<Booking[]> {
    return this.http.get<Booking[]>('/api/bookings/mine');
  }

  // notifications
  getNotifications(): Observable<NotificationItem[]> {
    return this.http.get<NotificationItem[]>('/api/notifications');
  }
  markNotificationRead(id: string): Observable<void> {
    return this.http.post<void>(`/api/notifications/${id}/read`, {});
  }

  // admin
  getAdminResources(): Observable<Resource[]> {
    return this.http.get<Resource[]>('/api/admin/resources');
  }
  createResource(request: SaveResourceRequest): Observable<Resource> {
    return this.http.post<Resource>('/api/admin/resources', request);
  }
  updateResource(id: string, request: SaveResourceRequest): Observable<Resource> {
    return this.http.put<Resource>(`/api/admin/resources/${id}`, request);
  }
  getUsers(): Observable<User[]> {
    return this.http.get<User[]>('/api/admin/users');
  }
  createUser(request: SaveUserRequest): Observable<User> {
    return this.http.post<User>('/api/admin/users', request);
  }
  updateUser(id: string, request: SaveUserRequest): Observable<User> {
    return this.http.put<User>(`/api/admin/users/${id}`, request);
  }
  getBookingHistory(filter: {
    resourceId?: string;
    userId?: string;
    from?: string;
    to?: string;
  }): Observable<Booking[]> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value) params = params.set(key, value);
    }
    return this.http.get<Booking[]>('/api/admin/bookings', { params });
  }
  getDashboard(from: string, to: string): Observable<Dashboard> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<Dashboard>('/api/admin/stats/dashboard', { params });
  }
  getInsightsSummary(): Observable<InsightsSummary> {
    return this.http.get<InsightsSummary>('/api/admin/stats/summary');
  }

  // AI assistant
  parseBookingRequest(text: string): Observable<AssistantParseResponse> {
    return this.http.post<AssistantParseResponse>('/api/assistant/parse-booking', { text });
  }
}
