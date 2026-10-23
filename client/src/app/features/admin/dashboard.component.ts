import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ApiClient } from '../../core/api-client';
import { Dashboard, InsightsSummary } from '../../core/models';

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  private readonly api = inject(ApiClient);

  protected readonly rangeDays = signal(14);
  protected readonly ranges = [7, 14, 30];
  protected readonly dashboard = signal<Dashboard | null>(null);
  protected readonly insights = signal<InsightsSummary | null>(null);
  protected readonly insightsError = signal<string | null>(null);
  protected readonly resetting = signal(false);

  protected readonly busiest = computed(() => {
    const rows = this.dashboard()?.utilisation ?? [];
    return rows.length ? [...rows].sort((a, b) => b.utilisationPct - a.utilisationPct)[0] : null;
  });

  protected readonly peakHour = computed(() => {
    const hours = this.dashboard()?.peakHours ?? [];
    return hours.length ? [...hours].sort((a, b) => b.count - a.count)[0] : null;
  });

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
      error: (err: { status?: number }) =>
        this.insightsError.set(
          err.status === 503
            ? 'AI insights are not configured on this deployment.'
            : 'Could not generate insights right now.',
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
