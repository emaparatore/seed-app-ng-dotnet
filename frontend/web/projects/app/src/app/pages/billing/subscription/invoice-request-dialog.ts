import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { CreateInvoiceRequest } from '../../pricing/billing.models';

@Component({
  selector: 'app-invoice-request-dialog',
  imports: [
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
  readonly data: Partial<CreateInvoiceRequest> | null = inject(MAT_DIALOG_DATA, { optional: true });

  protected form!: FormGroup;

  ngOnInit(): void {
    this.form = this.fb.group({
      customerType: [this.data?.customerType ?? 'Individual'],
      fullName: [this.data?.fullName ?? '', Validators.required],
      companyName: [this.data?.companyName ?? ''],
      address: [this.data?.address ?? '', Validators.required],
      city: [this.data?.city ?? '', Validators.required],
      postalCode: [this.data?.postalCode ?? '', Validators.required],
      country: [this.data?.country ?? 'Italia', Validators.required],
      fiscalCode: [this.data?.fiscalCode ?? ''],
      vatNumber: [this.data?.vatNumber ?? ''],
      sdiCode: [this.data?.sdiCode ?? ''],
      pecEmail: [this.data?.pecEmail ?? ''],
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
