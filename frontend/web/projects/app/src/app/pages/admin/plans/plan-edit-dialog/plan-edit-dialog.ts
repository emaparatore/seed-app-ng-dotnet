import { Component, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { AdminPlansService } from '../admin-plans.service';
import { AdminPlan, CreatePlanRequest } from '../admin-plans.models';

export interface PlanEditDialogData {
  plan?: AdminPlan;
}

@Component({
  selector: 'app-plan-edit-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatCheckboxModule,
  ],
  templateUrl: './plan-edit-dialog.html',
  styles: `
    .dialog-form {
      display: flex;
      flex-direction: column;
      gap: 8px;
      min-width: min(560px, 80vw);
    }

    .price-row {
      display: flex;
      gap: 12px;

      mat-form-field {
        flex: 1;
      }
    }

    .flags-row {
      display: flex;
      gap: 24px;
      flex-wrap: wrap;
      padding: 4px 0;
    }

    .features-section {
      margin-top: 8px;
    }

    .features-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 8px;

      h4 {
        margin: 0;
        font-size: 14px;
        font-weight: 500;
      }
    }

    .feature-row {
      display: flex;
      gap: 8px;
      align-items: flex-start;
      margin-bottom: 8px;
    }

    .feature-row mat-form-field {
      flex: 1;
      min-width: 0;
    }

    .feature-row .remove-btn {
      margin-top: 4px;
      flex-shrink: 0;
    }

    .price-warning {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      padding: 10px 12px;
      background: #fff8e1;
      border-radius: 6px;
      font-size: 13px;
      color: #e65100;
      margin-bottom: 4px;

      mat-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
        flex-shrink: 0;
        margin-top: 1px;
      }
    }

    .error-message {
      color: var(--mat-sys-error, #b3261e);
      font-size: 13px;
      margin: 0;
    }

    @media (max-width: 480px) {
      .price-row {
        flex-direction: column;
        gap: 0;
      }

      .flags-row {
        gap: 12px;
      }
    }
  `,
})
export class PlanEditDialog {
  private readonly fb = inject(FormBuilder);
  private readonly plansService = inject(AdminPlansService);
  private readonly dialogRef = inject(MatDialogRef<PlanEditDialog>);
  readonly data = inject<PlanEditDialogData>(MAT_DIALOG_DATA);

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly isEdit = !!this.data.plan;

  protected readonly form = this.fb.nonNullable.group({
    name: [this.data.plan?.name ?? '', Validators.required],
    description: [this.data.plan?.description ?? ''],
    monthlyPrice: [this.data.plan?.monthlyPrice ?? 0, [Validators.required, Validators.min(0)]],
    yearlyPrice: [this.data.plan?.yearlyPrice ?? 0, [Validators.required, Validators.min(0)]],
    trialDays: [this.data.plan?.trialDays ?? 0, Validators.min(0)],
    isFreeTier: [this.data.plan?.isFreeTier ?? false],
    isDefault: [this.data.plan?.isDefault ?? false],
    isPopular: [this.data.plan?.isPopular ?? false],
    sortOrder: [this.data.plan?.sortOrder ?? 0, [Validators.required, Validators.min(0)]],
    features: this.fb.array(
      (this.data.plan?.features ?? []).map((f) =>
        this.fb.nonNullable.group({
          key: [f.key, Validators.required],
          description: [f.description, Validators.required],
          limitValue: [f.limitValue ?? ''],
          sortOrder: [f.sortOrder, Validators.min(0)],
        }),
      ),
    ),
  });

  get featuresArray(): FormArray {
    return this.form.controls.features;
  }

  protected addFeature(): void {
    this.featuresArray.push(
      this.fb.nonNullable.group({
        key: ['', Validators.required],
        description: ['', Validators.required],
        limitValue: [''],
        sortOrder: [this.featuresArray.length, Validators.min(0)],
      }),
    );
  }

  protected removeFeature(index: number): void {
    this.featuresArray.removeAt(index);
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const request: CreatePlanRequest = {
      name: raw.name,
      description: raw.description || null,
      monthlyPrice: raw.monthlyPrice,
      yearlyPrice: raw.yearlyPrice,
      trialDays: raw.trialDays,
      isFreeTier: raw.isFreeTier,
      isDefault: raw.isDefault,
      isPopular: raw.isPopular,
      sortOrder: raw.sortOrder,
      features: raw.features.map((f) => ({
        key: f.key,
        description: f.description,
        limitValue: f.limitValue || null,
        sortOrder: f.sortOrder,
      })),
    };

    if (this.isEdit) {
      this.plansService.updatePlan(this.data.plan!.id, request).subscribe({
        next: () => {
          this.saving.set(false);
          this.dialogRef.close(true);
        },
        error: (err: { error?: { errors?: string[] } }) => {
          this.saving.set(false);
          this.errorMessage.set(err.error?.errors?.[0] ?? 'Errore durante il salvataggio del piano.');
        },
      });
    } else {
      this.plansService.createPlan(request).subscribe({
        next: (result) => {
          this.saving.set(false);
          this.dialogRef.close(result);
        },
        error: (err: { error?: { errors?: string[] } }) => {
          this.saving.set(false);
          this.errorMessage.set(err.error?.errors?.[0] ?? 'Errore durante il salvataggio del piano.');
        },
      });
    }
  }

  protected onCancel(): void {
    this.dialogRef.close(null);
  }
}
