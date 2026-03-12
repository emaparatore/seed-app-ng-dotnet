import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from 'shared-auth';

@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.scss',
})
export class ResetPassword {
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  protected readonly error = signal<string | null>(null);
  protected readonly success = signal<string | null>(null);
  protected readonly loading = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    token: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
  });

  onSubmit(): void {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);

    this.authService.resetPassword(this.form.getRawValue()).subscribe({
      next: (response) => {
        this.success.set(response.message);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Failed to reset password. Please try again.');
        this.loading.set(false);
      },
    });
  }
}
