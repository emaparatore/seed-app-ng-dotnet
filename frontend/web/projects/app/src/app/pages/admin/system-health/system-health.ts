import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { SystemHealthService } from './system-health.service';
import { SystemHealth } from './system-health.models';

@Component({
  selector: 'app-system-health',
  imports: [DatePipe, DecimalPipe, MatCardModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <div class="health-page">
      <div class="page-header">
        <h1>Stato del Sistema</h1>
        @if (!loading() && !error()) {
          <button mat-flat-button (click)="refresh()" [disabled]="refreshing()">
            @if (refreshing()) {
              <mat-icon class="spin">sync</mat-icon>
            } @else {
              <mat-icon>refresh</mat-icon>
            }
            Ricontrolla
          </button>
        }
      </div>

      @if (refreshing()) {
        <mat-progress-bar mode="indeterminate" />
      }

      @if (loading()) {
        <div class="skeleton-container">
          @for (i of [1, 2, 3]; track i) {
            <div class="skeleton-card">
              <div class="skeleton-title"></div>
              @for (j of [1, 2]; track j) {
                <div class="skeleton-row">
                  <div class="skeleton-label"></div>
                  <div class="skeleton-input"></div>
                </div>
              }
            </div>
          }
        </div>
      } @else if (error()) {
        <mat-card class="error-card">
          <mat-card-content>
            <div class="error-content">
              <mat-icon>error_outline</mat-icon>
              <p>{{ error() }}</p>
              <button mat-flat-button (click)="reload()">
                <mat-icon>refresh</mat-icon>
                Riprova
              </button>
            </div>
          </mat-card-content>
        </mat-card>
      } @else if (data(); as health) {
        <div class="health-cards">
          <mat-card class="health-card">
            <mat-card-header>
              <mat-card-title>
                <div class="card-title-row">
                  <span class="status-indicator" [class]="getStatusClass(health.database.status)"></span>
                  Database
                </div>
              </mat-card-title>
            </mat-card-header>
            <mat-card-content>
              <div class="status-row">
                <span class="label">Stato</span>
                <span class="value" [class]="'status-text ' + getStatusClass(health.database.status)">
                  {{ health.database.status }}
                </span>
              </div>
              @if (health.database.description) {
                <div class="status-row">
                  <span class="label">Dettagli</span>
                  <span class="value">{{ health.database.description }}</span>
                </div>
              }
            </mat-card-content>
          </mat-card>

          <mat-card class="health-card">
            <mat-card-header>
              <mat-card-title>
                <div class="card-title-row">
                  <span class="status-indicator" [class]="getEmailStatusClass(health.email.status)"></span>
                  Email
                </div>
              </mat-card-title>
            </mat-card-header>
            <mat-card-content>
              <div class="status-row">
                <span class="label">Stato</span>
                <span class="value" [class]="'status-text ' + getEmailStatusClass(health.email.status)">
                  {{ health.email.status }}
                </span>
              </div>
              @if (health.email.description) {
                <div class="status-row">
                  <span class="label">Dettagli</span>
                  <span class="value">{{ health.email.description }}</span>
                </div>
              }
            </mat-card-content>
          </mat-card>

          <mat-card class="health-card">
            <mat-card-header>
              <mat-card-title>
                <div class="card-title-row">
                  <span class="status-indicator" [class]="getStatusClass(health.paymentsWebhook.status)"></span>
                  Webhook pagamenti
                </div>
              </mat-card-title>
            </mat-card-header>
            <mat-card-content>
              <div class="status-row">
                <span class="label">Stato</span>
                <span class="value" [class]="'status-text ' + getStatusClass(health.paymentsWebhook.status)">
                  {{ health.paymentsWebhook.status }}
                </span>
              </div>
              <div class="status-row">
                <span class="label">Dettagli</span>
                <span class="value">{{ health.paymentsWebhook.description }}</span>
              </div>
              <div class="status-row">
                <span class="label">Webhook ricevuto (ultimo)</span>
                <span class="value">{{ health.paymentsWebhook.lastWebhookReceivedAt ? (health.paymentsWebhook.lastWebhookReceivedAt | date: 'dd/MM/yyyy HH:mm:ss') : '-' }}</span>
              </div>
              <div class="status-row">
                <span class="label">Ultimo errore</span>
                <span class="value">{{ health.paymentsWebhook.lastFailureAt ? (health.paymentsWebhook.lastFailureAt | date: 'dd/MM/yyyy HH:mm:ss') : '-' }}</span>
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card class="health-card">
            <mat-card-header>
              <mat-card-title>
                <div class="card-title-row">
                  <mat-icon class="info-icon">info</mat-icon>
                  Informazioni Generali
                </div>
              </mat-card-title>
            </mat-card-header>
            <mat-card-content>
              <div class="status-row">
                <span class="label">Versione</span>
                <span class="value">{{ health.version }}</span>
              </div>
              <div class="status-row">
                <span class="label">Ambiente</span>
                <span class="value">{{ health.environment }}</span>
              </div>
              <div class="status-row">
                <span class="label">Uptime</span>
                <span class="value">{{ health.uptime.formatted }}</span>
              </div>
              <div class="status-row">
                <span class="label">Memoria (Working Set)</span>
                <span class="value">{{ health.memory.workingSetMegabytes | number: '1.1-1' }} MB</span>
              </div>
              <div class="status-row">
                <span class="label">Memoria (GC Allocata)</span>
                <span class="value">{{ health.memory.gcAllocatedMegabytes | number: '1.1-1' }} MB</span>
              </div>
            </mat-card-content>
          </mat-card>
        </div>
      }
    </div>
  `,
  styles: `
    .health-page {
      max-width: 800px;
    }

    .page-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 24px;

      h1 {
        margin: 0;
        font-size: 24px;
        font-weight: 400;
      }

      button mat-icon {
        margin-right: 8px;
      }
    }

    mat-progress-bar {
      margin-bottom: 16px;
    }

    .health-cards {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .health-card {
      margin-bottom: 0;
    }

    .card-title-row {
      display: flex;
      align-items: center;
      gap: 10px;
    }

    .status-indicator {
      display: inline-block;
      width: 12px;
      height: 12px;
      border-radius: 50%;
      flex-shrink: 0;

      &.healthy {
        background-color: #4caf50;
      }

      &.degraded {
        background-color: #ff9800;
      }

      &.unhealthy {
        background-color: #f44336;
      }
    }

    .info-icon {
      color: var(--mat-sys-primary, #6750a4);
    }

    .status-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 10px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant, rgba(0, 0, 0, 0.12));

      &:last-child {
        border-bottom: none;
      }
    }

    .label {
      font-size: 14px;
      color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
    }

    .value {
      font-size: 14px;
      font-weight: 500;
    }

    .status-text {
      &.healthy {
        color: #4caf50;
      }

      &.degraded {
        color: #ff9800;
      }

      &.unhealthy {
        color: #f44336;
      }
    }

    .spin {
      animation: spin 1s linear infinite;
    }

    @keyframes spin {
      from {
        transform: rotate(0deg);
      }
      to {
        transform: rotate(360deg);
      }
    }

    .error-card {
      .error-content {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 16px;
        padding: 24px;
        text-align: center;

        mat-icon {
          font-size: 48px;
          width: 48px;
          height: 48px;
          color: var(--mat-sys-error, #b3261e);
        }

        p {
          margin: 0;
          color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
        }
      }
    }

    .skeleton-container {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .skeleton-card {
      background: var(--mat-sys-surface-container, #f3edf7);
      border-radius: 12px;
      padding: 24px;
      animation: pulse 1.5s ease-in-out infinite;
    }

    .skeleton-title {
      width: 120px;
      height: 20px;
      border-radius: 4px;
      background: var(--mat-sys-surface-container-high, #ece6f0);
      margin-bottom: 20px;
    }

    .skeleton-row {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin-bottom: 16px;

      &:last-child {
        margin-bottom: 0;
      }
    }

    .skeleton-label {
      width: 200px;
      height: 14px;
      border-radius: 4px;
      background: var(--mat-sys-surface-container-high, #ece6f0);
    }

    .skeleton-input {
      width: 100%;
      height: 40px;
      border-radius: 4px;
      background: var(--mat-sys-surface-container-high, #ece6f0);
    }

    @keyframes pulse {
      0%,
      100% {
        opacity: 1;
      }
      50% {
        opacity: 0.5;
      }
    }

    @media (max-width: 768px) {
      .health-page {
        max-width: 100%;
      }

      .page-header {
        flex-wrap: wrap;
        gap: 12px;

        h1 {
          font-size: 20px;
        }
      }

      .status-row {
        flex-direction: column;
        align-items: flex-start;
        gap: 4px;
      }
    }
  `,
})
export class SystemHealthComponent implements OnInit {
  private readonly healthService = inject(SystemHealthService);

  protected readonly loading = signal(true);
  protected readonly refreshing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly data = signal<SystemHealth | null>(null);

  ngOnInit(): void {
    this.loadHealth();
  }

  protected refresh(): void {
    this.refreshing.set(true);
    this.healthService.getSystemHealth().subscribe({
      next: (health) => {
        this.data.set(health);
        this.refreshing.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore durante il refresh.');
        this.refreshing.set(false);
      },
    });
  }

  protected reload(): void {
    this.loadHealth();
  }

  protected getStatusClass(status: string): string {
    switch (status) {
      case 'Healthy':
        return 'healthy';
      case 'Degraded':
        return 'degraded';
      case 'Unhealthy':
        return 'unhealthy';
      default:
        return 'degraded';
    }
  }

  protected getEmailStatusClass(status: string): string {
    switch (status) {
      case 'Configured':
        return 'healthy';
      case 'NotConfigured':
        return 'degraded';
      default:
        return 'degraded';
    }
  }

  private loadHealth(): void {
    this.loading.set(true);
    this.error.set(null);
    this.healthService.getSystemHealth().subscribe({
      next: (health) => {
        this.data.set(health);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore nel caricamento dello stato del sistema.');
        this.loading.set(false);
      },
    });
  }
}
