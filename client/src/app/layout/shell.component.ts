import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ApiClient } from '../core/api-client';
import { AuthService } from '../core/auth.service';
import { NotificationItem } from '../core/models';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, DatePipe],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(ApiClient);

  protected readonly notifications = signal<NotificationItem[]>([]);
  protected readonly bellOpen = signal(false);

  protected readonly unreadCount = computed(
    () => this.notifications().filter((n) => !n.readAt).length,
  );

  constructor() {
    this.loadNotifications();
  }

  private loadNotifications(): void {
    this.api.getNotifications().subscribe((items) => this.notifications.set(items));
  }

  protected toggleBell(): void {
    // Refresh on open — notifications are fetched, not pushed.
    if (!this.bellOpen()) this.loadNotifications();
    this.bellOpen.update((open) => !open);
  }

  protected markRead(notification: NotificationItem): void {
    if (notification.readAt) return;
    this.api.markNotificationRead(notification.id).subscribe(() => this.loadNotifications());
  }
}
