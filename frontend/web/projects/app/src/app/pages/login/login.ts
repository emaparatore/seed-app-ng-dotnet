import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { AuthService } from 'shared-auth';
import { ConsentUpdateDialog } from './consent-update-dialog';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(false);
  protected readonly showResendOption = signal(false);
  protected readonly resendStatus = signal<'idle' | 'loading' | 'success' | 'error'>('idle');

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  onSubmit(): void {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);

    this.authService.login(this.form.getRawValue()).subscribe({
      next: () => {
        if (this.authService.mustChangePassword()) {
          this.router.navigate(['/change-password']);
        } else if (this.authService.consentUpdateRequired()) {
          this.openConsentDialog();
        } else {
          this.router.navigate(['/']);
        }
      },
      error: (err) => {
        const errorMsg = err.error?.errors?.[0] ?? 'Login failed.';
        this.error.set(errorMsg);
        this.showResendOption.set(errorMsg.includes('verify your email'));
        this.resendStatus.set('idle');
        this.loading.set(false);
      },
    });
  }

  private openConsentDialog(): void {
    const dialogRef = this.dialog.open(ConsentUpdateDialog, { disableClose: true });
    dialogRef.afterClosed().subscribe((accepted) => {
      if (accepted) {
        this.authService.acceptUpdatedConsent().subscribe({
          next: () => this.router.navigate(['/']),
          error: () => {
            this.error.set('Failed to update consent. Please try again.');
            this.loading.set(false);
          },
        });
      } else {
        this.authService.logout();
      }
    });
  }

  protected resendConfirmationEmail(): void {
    const email = this.form.controls.email.value;
    if (!email) return;
    this.resendStatus.set('loading');
    this.authService.resendConfirmationEmail({ email }).subscribe({
      next: () => this.resendStatus.set('success'),
      error: () => this.resendStatus.set('error'),
    });
  }
}
