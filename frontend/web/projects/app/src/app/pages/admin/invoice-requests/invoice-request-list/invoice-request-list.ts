import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AdminInvoiceRequestsService } from '../admin-invoice-requests.service';
import { AdminInvoiceRequest } from '../admin-invoice-requests.models';

@Component({
  selector: 'app-invoice-request-list',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
  ],
  templateUrl: './invoice-request-list.html',
  styleUrl: './invoice-request-list.scss',
})
export class InvoiceRequestList implements OnInit {
  private readonly service = inject(AdminInvoiceRequestsService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly displayedColumns = ['userEmail', 'name', 'customerType', 'status', 'createdAt', 'actions'];

  protected readonly loading = signal(true);
  protected readonly requests = signal<AdminInvoiceRequest[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly error = signal<string | null>(null);
  protected readonly updatingId = signal<string | null>(null);

  protected readonly statusFilterControl = new FormControl('');

  protected pageIndex = 0;
  protected pageSize = 10;

  protected readonly statusOptions = [
    { value: 'Requested', label: 'Richiesta' },
    { value: 'InProgress', label: 'In lavorazione' },
    { value: 'Issued', label: 'Emessa' },
  ];

  ngOnInit(): void {
    this.loadRequests();
  }

  protected onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadRequests();
  }

  protected onFilterChange(): void {
    this.pageIndex = 0;
    this.loadRequests();
  }

  protected updateStatus(req: AdminInvoiceRequest, newStatus: string): void {
    if (req.status === newStatus) return;
    this.updatingId.set(req.id);
    this.service.updateInvoiceRequestStatus(req.id, newStatus).subscribe({
      next: () => {
        this.updatingId.set(null);
        this.snackBar.open('Stato aggiornato.', 'Chiudi', { duration: 3000 });
        this.loadRequests();
      },
      error: (err) => {
        this.updatingId.set(null);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Errore nell\'aggiornamento dello stato.', 'Chiudi', { duration: 5000 });
      },
    });
  }

  protected reload(): void {
    this.loadRequests();
  }

  protected statusLabel(status: string): string {
    switch (status) {
      case 'Requested': return 'Richiesta';
      case 'InProgress': return 'In lavorazione';
      case 'Issued': return 'Emessa';
      default: return status;
    }
  }

  protected statusClass(status: string): string {
    switch (status) {
      case 'Requested': return 'requested';
      case 'InProgress': return 'in-progress';
      case 'Issued': return 'issued';
      default: return '';
    }
  }

  private loadRequests(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.getInvoiceRequests({
      pageNumber: this.pageIndex + 1,
      pageSize: this.pageSize,
      status: this.statusFilterControl.value || undefined,
    }).subscribe({
      next: (result) => {
        this.requests.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore nel caricamento delle richieste.');
        this.loading.set(false);
      },
    });
  }
}
