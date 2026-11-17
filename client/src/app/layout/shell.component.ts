import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { NotificationItem } from '../core/models';
import { NotificationsService } from '../core/notifications.service';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, DatePipe],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly notifications = inject(NotificationsService);
  private readonly router = inject(Router);

  protected readonly bellOpen = signal(false);

  constructor() {
    this.notifications.load();
  }

  protected toggleBell(): void {
    if (!this.bellOpen()) this.notifications.load();
    this.bellOpen.update((open) => !open);
  }

  /** Reading a notification about a booking takes you to that booking. */
  protected open(notification: NotificationItem): void {
    this.notifications.markRead(notification);
    if (!notification.bookingId) return;
    this.bellOpen.set(false);
    void this.router.navigate(['/my-bookings'], { queryParams: { booking: notification.bookingId } });
  }
}
