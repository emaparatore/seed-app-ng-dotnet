import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AdminSubscriptionsService } from '../admin-subscriptions.service';
import { AdminPlansService } from '../../plans/admin-plans.service';
import { AdminPlan } from '../../plans/admin-plans.models';
import { AdminSubscription, SubscriptionMetrics } from '../admin-subscriptions.models';
import { SubscriptionDetailDialog } from '../subscription-detail-dialog/subscription-detail-dialog';

@Component({
  selector: 'app-subscription-list',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatTooltipModule,
  ],
  templateUrl: './subscription-list.html',
  styleUrl: './subscription-list.scss',
})
export class SubscriptionList implements OnInit {
  private readonly subscriptionsService = inject(AdminSubscriptionsService);
  private readonly plansService = inject(AdminPlansService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly displayedColumns = ['userEmail', 'planName', 'status', 'currentPeriodStart', 'currentPeriodEnd', 'actions'];

  protected readonly loadingMetrics = signal(true);
  protected readonly loadingList = signal(true);
  protected readonly metrics = signal<SubscriptionMetrics | null>(null);
  protected readonly subscriptions = signal<AdminSubscription[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly error = signal<string | null>(null);
  protected readonly plans = signal<AdminPlan[]>([]);

  protected readonly planFilterControl = new FormControl('');
  protected readonly statusFilterControl = new FormControl('');

  protected pageIndex = 0;
  protected pageSize = 10;

  protected readonly statusOptions = [
    { value: 'Active', label: 'Attivo' },
    { value: 'Trialing', label: 'In prova' },
    { value: 'PastDue', label: 'Scaduto' },
    { value: 'Canceled', label: 'Annullato' },
    { value: 'Expired', label: 'Scaduto (periodo)' },
  ];

  ngOnInit(): void {
    this.loadMetrics();
    this.loadSubscriptions();
    this.loadPlans();
  }

  protected onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadSubscriptions();
  }

  protected onFilterChange(): void {
    this.pageIndex = 0;
    this.loadSubscriptions();
  }

  protected openDetail(subscription: AdminSubscription): void {
    this.subscriptionsService.getSubscriptionById(subscription.id).subscribe({
      next: (detail) => {
        this.dialog.open(SubscriptionDetailDialog, {
          width: '600px',
          maxWidth: '95vw',
          data: detail,
        });
      },
      error: (err) => {
        this.snackBar.open(err.error?.errors?.[0] ?? 'Errore nel caricamento del dettaglio', 'Chiudi', { duration: 5000 });
      },
    });
  }

  protected reload(): void {
    this.loadMetrics();
    this.loadSubscriptions();
  }

  private loadMetrics(): void {
    this.loadingMetrics.set(true);
    this.subscriptionsService.getMetrics().subscribe({
      next: (data) => {
        this.metrics.set(data);
        this.loadingMetrics.set(false);
      },
      error: () => {
        this.loadingMetrics.set(false);
      },
    });
  }

  private loadSubscriptions(): void {
    this.loadingList.set(true);
    this.error.set(null);

    this.subscriptionsService.getSubscriptions({
      pageNumber: this.pageIndex + 1,
      pageSize: this.pageSize,
      planId: this.planFilterControl.value || undefined,
      status: this.statusFilterControl.value || undefined,
    }).subscribe({
      next: (result) => {
        this.subscriptions.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loadingList.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore nel caricamento degli abbonamenti.');
        this.loadingList.set(false);
      },
    });
  }

  private loadPlans(): void {
    this.plansService.getPlans().subscribe({
      next: (plans) => this.plans.set(plans),
    });
  }
}
