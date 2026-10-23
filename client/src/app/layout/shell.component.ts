import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ApiClient } from '../core/api-client';
import { AuthService } from '../core/auth.service';
import { NotificationItem } from '../core/models';
import { RealtimeService } from '../core/realtime.service';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, DatePipe],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(ApiClient);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly notifications = signal<NotificationItem[]>([]);
  protected readonly bellOpen = signal(false);
  protected readonly liveToast = signal<string | null>(null);

  protected readonly unreadCount = computed(
    () => this.notifications().filter((n) => !n.readAt).length,
  );

  constructor() {
    this.loadNotifications();

    void this.realtime.connect();
    this.realtime.notifications$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.liveToast.set(event.message);
        setTimeout(() => this.liveToast.set(null), 6000);
        this.loadNotifications();
      });
  }

  private loadNotifications(): void {
    this.api.getNotifications().subscribe((items) => this.notifications.set(items));
  }

  protected toggleBell(): void {
    this.bellOpen.update((open) => !open);
  }

  protected markRead(notification: NotificationItem): void {
    if (notification.readAt) return;
    this.api.markNotificationRead(notification.id).subscribe(() => this.loadNotifications());
  }
}
