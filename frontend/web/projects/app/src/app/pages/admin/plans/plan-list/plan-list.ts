import { Component, inject, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { HasPermissionDirective, PERMISSIONS, PermissionService } from 'shared-auth';
import { AdminPlansService } from '../admin-plans.service';
import { AdminPlan } from '../admin-plans.models';
import { ConfirmDialog } from '../../users/confirm-dialog/confirm-dialog';
import { PlanEditDialog } from '../plan-edit-dialog/plan-edit-dialog';

@Component({
  selector: 'app-plan-list',
  imports: [
    DecimalPipe,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    HasPermissionDirective,
  ],
  templateUrl: './plan-list.html',
  styleUrl: './plan-list.scss',
})
export class PlanList implements OnInit {
  private readonly plansService = inject(AdminPlansService);
  private readonly permissionService = inject(PermissionService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  protected readonly permissions = PERMISSIONS;
  protected readonly displayedColumns = ['name', 'monthlyPrice', 'yearlyPrice', 'status', 'subscribers', 'actions'];

  protected readonly loading = signal(true);
  protected readonly plans = signal<AdminPlan[]>([]);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadPlans();
  }

  protected openCreateDialog(): void {
    const dialogRef = this.dialog.open(PlanEditDialog, {
      width: '640px',
      maxWidth: '95vw',
      data: {},
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadPlans();
        this.snackBar.open('Piano creato con successo', 'Chiudi', { duration: 3000 });
      }
    });
  }

  protected openEditDialog(plan: AdminPlan, event: Event): void {
    event.stopPropagation();
    const dialogRef = this.dialog.open(PlanEditDialog, {
      width: '640px',
      maxWidth: '95vw',
      data: { plan },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadPlans();
        this.snackBar.open('Piano aggiornato con successo', 'Chiudi', { duration: 3000 });
      }
    });
  }

  protected archivePlan(plan: AdminPlan, event: Event): void {
    event.stopPropagation();
    const dialogRef = this.dialog.open(ConfirmDialog, {
      width: '400px',
      data: {
        title: 'Archivia piano',
        message: `Sei sicuro di voler archiviare il piano "${plan.name}"? I subscriber attivi non saranno modificati.`,
        confirmText: 'Archivia',
        cancelText: 'Annulla',
      },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.plansService.archivePlan(plan.id).subscribe({
          next: () => {
            this.loadPlans();
            this.snackBar.open('Piano archiviato con successo', 'Chiudi', { duration: 3000 });
          },
          error: (err) => {
            this.snackBar.open(err.error?.errors?.[0] ?? 'Errore durante l\'archiviazione', 'Chiudi', { duration: 5000 });
          },
        });
      }
    });
  }

  protected hasPermission(permission: string): boolean {
    return this.permissionService.hasPermission(permission);
  }

  protected reload(): void {
    this.loadPlans();
  }

  private loadPlans(): void {
    this.loading.set(true);
    this.error.set(null);

    this.plansService.getPlans().subscribe({
      next: (plans) => {
        this.plans.set(plans);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore nel caricamento dei piani.');
        this.loading.set(false);
      },
    });
  }
}
