import { Injectable, computed, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { NotificationItem } from './models';

/** The bell's state, shared so a booking made anywhere can refresh the badge. Fetched, not pushed. */
@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly api = inject(ApiService);

  readonly items = signal<NotificationItem[]>([]);
  readonly unreadCount = computed(() => this.items().filter((n) => !n.readAt).length);

  load(): void {
    this.api.getNotifications().subscribe((items) => this.items.set(items));
  }

  markRead(notification: NotificationItem): void {
    if (notification.readAt) return;
    this.api.markNotificationRead(notification.id).subscribe(() => this.load());
  }
}
