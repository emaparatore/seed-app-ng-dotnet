import { Component, afterNextRender, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from 'shared-auth';

@Component({
  selector: 'app-confirm-email',
  imports: [MatCardModule, MatButtonModule, MatProgressSpinnerModule, MatIconModule, RouterLink],
  templateUrl: './confirm-email.html',
  styleUrl: './confirm-email.scss',
})
export class ConfirmEmail {
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly status = signal<'loading' | 'success' | 'error'>('loading');
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly resendStatus = signal<'idle' | 'loading' | 'success' | 'error'>('idle');
  private readonly email = signal<string | null>(null);

  constructor() {
    afterNextRender(() => {
      const params = this.route.snapshot.queryParams;
      const email = params['email'];
      const token = params['token'];
      this.email.set(email ?? null);

      if (!email || !token) {
        this.status.set('error');
        this.errorMessage.set('Invalid verification link. Please check your email and try again.');
        return;
      }

      this.authService.confirmEmail(email, token).subscribe({
        next: () => {
          this.status.set('success');
          setTimeout(() => this.router.navigate(['/']), 2000);
        },
        error: (err) => {
          this.status.set('error');
          this.errorMessage.set(err.error?.errors?.[0] ?? 'Verification failed. The link may have expired.');
        },
      });
    });
  }

  protected resendEmail(): void {
    const email = this.email();
    if (!email) return;
    this.resendStatus.set('loading');
    this.authService.resendConfirmationEmail({ email }).subscribe({
      next: () => this.resendStatus.set('success'),
      error: () => this.resendStatus.set('error'),
    });
  }
}
