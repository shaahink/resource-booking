import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { USE_MOCKS } from './config';

export interface BookingEvent {
  type: 'bookingCreated' | 'bookingCancelled';
  resourceId: string;
  bookingId: string;
}

export interface UserNotificationEvent {
  message: string;
}

/**
 * SignalR wrapper. In mock mode every method is a silent no-op so feature code can call it
 * unconditionally. Components watch the resource they are displaying; the availability grid
 * refetches on each event rather than patching state — simpler, and the server stays the
 * single source of truth.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private connection: HubConnection | null = null;

  readonly bookingEvents$ = new Subject<BookingEvent>();
  readonly notifications$ = new Subject<UserNotificationEvent>();

  async connect(): Promise<void> {
    if (USE_MOCKS || this.connection) return;

    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/bookings')
      .withAutomaticReconnect()
      .build();

    this.connection.on('bookingCreated', (resourceId: string, bookingId: string) =>
      this.bookingEvents$.next({ type: 'bookingCreated', resourceId, bookingId }),
    );
    this.connection.on('bookingCancelled', (resourceId: string, bookingId: string) =>
      this.bookingEvents$.next({ type: 'bookingCancelled', resourceId, bookingId }),
    );
    this.connection.on('notification', (message: string) =>
      this.notifications$.next({ message }),
    );

    await this.connection.start();
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }

  async watchResource(resourceId: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('WatchResource', resourceId);
    }
  }

  async unwatchResource(resourceId: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('UnwatchResource', resourceId);
    }
  }
}
