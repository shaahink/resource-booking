import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Component, computed, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { Dashboard, InsightsSummary } from '../../core/models';

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, DecimalPipe, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  private readonly api = inject(ApiService);

  protected readonly rangeDays = signal(14);
  protected readonly ranges = [7, 14, 30];
  protected readonly dashboard = signal<Dashboard | null>(null);
  protected readonly insights = signal<InsightsSummary | null>(null);
  protected readonly insightsError = signal<string | null>(null);
  protected readonly resetting = signal(false);

  /** Busiest first, so the rows read as a ranking. */
  protected readonly utilisation = computed(() =>
    [...(this.dashboard()?.utilisation ?? [])].sort((a, b) => b.utilisationPct - a.utilisationPct),
  );

  // With nothing booked there is no busiest resource or peak hour, only a dash.
  protected readonly busiest = computed(() => {
    const top = this.utilisation()[0];
    return top && top.utilisationPct > 0 ? top : null;
  });

  protected readonly peakHour = computed(() => {
    const top = [...(this.dashboard()?.peakHours ?? [])].sort((a, b) => b.count - a.count)[0];
    return top && top.count > 0 ? top : null;
  });

  /** yyyy-MM-dd of the range start, so a click into history keeps the same window. */
  protected readonly rangeStart = computed(() =>
    new Date(Date.now() - this.rangeDays() * 86_400_000).toISOString().slice(0, 10),
  );

  protected readonly maxPeakCount = computed(() =>
    Math.max(1, ...(this.dashboard()?.peakHours ?? []).map((p) => p.count)),
  );

  constructor() {
    this.load();
    this.loadInsights();
  }

  protected setRange(days: number): void {
    this.rangeDays.set(days);
    this.load();
  }

  private load(): void {
    const to = new Date();
    const from = new Date(to.getTime() - this.rangeDays() * 86_400_000);
    this.api
      .getDashboard(from.toISOString(), to.toISOString())
      .subscribe((dashboard) => this.dashboard.set(dashboard));
  }

  protected loadInsights(): void {
    this.insightsError.set(null);
    this.api.getInsightsSummary().subscribe({
      next: (insights) => this.insights.set(insights),
      error: (err: { status?: number; error?: { title?: string } }) =>
        this.insightsError.set(
          err.status === 503
            ? 'AI insights are not configured on this deployment.'
            : (err.error?.title ?? 'Could not generate insights right now.'),
        ),
    });
  }

  /** The demo is public and editable by design; this puts the seeded fortnight back. */
  protected resetDemo(): void {
    if (this.resetting()) return;
    if (!confirm('Reset the demo? All bookings, resources and users return to the seeded state.')) return;

    this.resetting.set(true);
    this.api.resetDemoData().subscribe({
      next: () => {
        this.resetting.set(false);
        this.load();
        this.loadInsights();
      },
      error: () => this.resetting.set(false),
    });
  }

  protected hourLabel(hour: number): string {
    return `${String(hour).padStart(2, '0')}:00`;
  }
}
