import { Component, inject, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { CreateInvoiceRequest } from '../../pricing/billing.models';

export interface InvoiceRequestPurchaseContext {
  userSubscriptionId: string;
  serviceName: string;
  periodStart: string;
  periodEnd: string;
}

export interface InvoiceRequestDialogData {
  prefill?: Partial<CreateInvoiceRequest>;
  purchaseContext?: InvoiceRequestPurchaseContext;
}

@Component({
  selector: 'app-invoice-request-dialog',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>Richiedi fattura</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="invoice-form">
        @if (purchaseContext) {
          <div class="purchase-context">
            <p class="context-title">Riferimento acquisto</p>
            <p><strong>Servizio:</strong> {{ purchaseContext.serviceName }}</p>
            <p><strong>Periodo:</strong> {{ purchaseContext.periodStart | date:'dd/MM/yyyy' }} - {{ purchaseContext.periodEnd | date:'dd/MM/yyyy' }}</p>
          </div>
        }

        <div class="customer-type-toggle">
          <mat-button-toggle-group formControlName="customerType" (change)="onCustomerTypeChange()">
            <mat-button-toggle value="Individual">Persona fisica</mat-button-toggle>
            <mat-button-toggle value="Company">Azienda</mat-button-toggle>
          </mat-button-toggle-group>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Nome e cognome</mat-label>
          <input matInput formControlName="fullName" />
          @if (form.get('fullName')?.hasError('required') && form.get('fullName')?.touched) {
            <mat-error>Campo obbligatorio</mat-error>
          }
        </mat-form-field>

        @if (isCompany()) {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Ragione sociale</mat-label>
            <input matInput formControlName="companyName" />
            @if (form.get('companyName')?.hasError('required') && form.get('companyName')?.touched) {
              <mat-error>Campo obbligatorio per aziende</mat-error>
            }
          </mat-form-field>
        }

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Indirizzo</mat-label>
          <input matInput formControlName="address" />
          @if (form.get('address')?.hasError('required') && form.get('address')?.touched) {
            <mat-error>Campo obbligatorio</mat-error>
          }
        </mat-form-field>

        <div class="two-columns">
          <mat-form-field appearance="outline">
            <mat-label>Città</mat-label>
            <input matInput formControlName="city" />
            @if (form.get('city')?.hasError('required') && form.get('city')?.touched) {
              <mat-error>Campo obbligatorio</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>CAP</mat-label>
            <input matInput formControlName="postalCode" />
            @if (form.get('postalCode')?.hasError('required') && form.get('postalCode')?.touched) {
              <mat-error>Campo obbligatorio</mat-error>
            }
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Paese</mat-label>
          <input matInput formControlName="country" />
          @if (form.get('country')?.hasError('required') && form.get('country')?.touched) {
            <mat-error>Campo obbligatorio</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Codice fiscale</mat-label>
          <input matInput formControlName="fiscalCode" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Partita IVA{{ isCompany() ? ' *' : '' }}</mat-label>
          <input matInput formControlName="vatNumber" />
          @if (form.get('vatNumber')?.hasError('required') && form.get('vatNumber')?.touched) {
            <mat-error>Campo obbligatorio per aziende</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Codice SDI</mat-label>
          <input matInput formControlName="sdiCode" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>PEC</mat-label>
          <input matInput formControlName="pecEmail" type="email" />
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Annulla</button>
      <button mat-flat-button color="primary" (click)="submit()" [disabled]="form.invalid">
        Invia richiesta
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .invoice-form {
      display: flex;
      flex-direction: column;
      gap: 4px;
      min-width: 400px;
      padding-top: 8px;
    }
    .purchase-context {
      border: 1px solid rgba(0, 0, 0, 0.12);
      border-radius: 12px;
      padding: 10px 12px;
      margin-bottom: 12px;

      p {
        margin: 0;
        color: rgba(0, 0, 0, 0.75);
        font-size: 13px;
      }

      .context-title {
        font-weight: 600;
        margin-bottom: 4px;
      }
    }
    .customer-type-toggle {
      margin-bottom: 12px;
    }
    .full-width {
      width: 100%;
    }
    .two-columns {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 12px;
    }
    @media (max-width: 480px) {
      .invoice-form { min-width: unset; }
      .two-columns { grid-template-columns: 1fr; gap: 0; }
    }
  `],
})
export class InvoiceRequestDialog implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<InvoiceRequestDialog>);
  readonly data: InvoiceRequestDialogData | null = inject(MAT_DIALOG_DATA, { optional: true });

  protected form!: FormGroup;
  protected purchaseContext: InvoiceRequestPurchaseContext | null = null;

  ngOnInit(): void {
    this.purchaseContext = this.data?.purchaseContext ?? null;
    const prefill = this.data?.prefill;

    this.form = this.fb.group({
      customerType: [prefill?.customerType ?? 'Individual'],
      fullName: [prefill?.fullName ?? '', Validators.required],
      companyName: [prefill?.companyName ?? ''],
      address: [prefill?.address ?? '', Validators.required],
      city: [prefill?.city ?? '', Validators.required],
      postalCode: [prefill?.postalCode ?? '', Validators.required],
      country: [prefill?.country ?? 'Italia', Validators.required],
      fiscalCode: [prefill?.fiscalCode ?? ''],
      vatNumber: [prefill?.vatNumber ?? ''],
      sdiCode: [prefill?.sdiCode ?? ''],
      pecEmail: [prefill?.pecEmail ?? ''],
      userSubscriptionId: [
        prefill?.userSubscriptionId ?? this.purchaseContext?.userSubscriptionId ?? '',
        Validators.required,
      ],
    });
    this.updateCompanyValidators();
  }

  protected isCompany(): boolean {
    return this.form.get('customerType')?.value === 'Company';
  }

  protected onCustomerTypeChange(): void {
    this.updateCompanyValidators();
  }

  protected submit(): void {
    if (this.form.invalid) return;
    const value = this.form.value;
    const request: CreateInvoiceRequest = {
      customerType: value.customerType,
      fullName: value.fullName,
      address: value.address,
      city: value.city,
      postalCode: value.postalCode,
      country: value.country,
      userSubscriptionId: value.userSubscriptionId,
    };
    if (value.companyName) request.companyName = value.companyName;
    if (value.fiscalCode) request.fiscalCode = value.fiscalCode;
    if (value.vatNumber) request.vatNumber = value.vatNumber;
    if (value.sdiCode) request.sdiCode = value.sdiCode;
    if (value.pecEmail) request.pecEmail = value.pecEmail;
    this.dialogRef.close(request);
  }

  private updateCompanyValidators(): void {
    const companyName = this.form.get('companyName');
    const vatNumber = this.form.get('vatNumber');
    if (this.isCompany()) {
      companyName?.setValidators(Validators.required);
      vatNumber?.setValidators(Validators.required);
    } else {
      companyName?.clearValidators();
      vatNumber?.clearValidators();
    }
    companyName?.updateValueAndValidity();
    vatNumber?.updateValueAndValidity();
  }
}
