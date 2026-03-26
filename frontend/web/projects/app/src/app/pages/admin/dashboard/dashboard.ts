import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { AdminDashboardService } from './dashboard.service';
import { DashboardStats } from './models/dashboard.models';
import { LineChart } from './components/line-chart';
import { DonutChart } from './components/donut-chart';

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, RouterLink, MatCardModule, MatIconModule, MatButtonModule, LineChart, DonutChart],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit {
  private readonly dashboardService = inject(AdminDashboardService);

  protected readonly loading = signal(true);
  protected readonly stats = signal<DashboardStats | null>(null);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadStats();
  }

  reload(): void {
    this.loadStats();
  }

  private loadStats(): void {
    this.loading.set(true);
    this.error.set(null);
    this.dashboardService.getStats().subscribe({
      next: (data) => {
        this.stats.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore nel caricamento dei dati.');
        this.loading.set(false);
      },
    });
  }
}
