import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { AuthService } from 'shared-auth';

@Component({
  selector: 'app-profile',
  imports: [RouterLink, MatButtonModule, MatCardModule],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class Profile implements OnInit {
  private readonly authService = inject(AuthService);

  protected readonly user = this.authService.currentUser;
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.authService.getProfile().subscribe({
      next: () => this.loading.set(false),
      error: () => {
        this.error.set('Unable to load user details.');
        this.loading.set(false);
      },
    });
  }
}
