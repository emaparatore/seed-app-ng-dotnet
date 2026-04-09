import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { AuthService } from 'shared-auth';
import { ConfirmDeleteDialog } from './confirm-delete-dialog';

@Component({
  selector: 'app-profile',
  imports: [RouterLink, MatButtonModule, MatCardModule],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class Profile implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);

  protected readonly user = this.authService.currentUser;
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly deleting = signal(false);
  protected readonly exporting = signal(false);

  ngOnInit(): void {
    this.authService.getProfile().subscribe({
      next: () => this.loading.set(false),
      error: () => {
        this.error.set('Unable to load user details.');
        this.loading.set(false);
      },
    });
  }

  exportMyData(): void {
    this.exporting.set(true);
    this.error.set(null);

    this.authService.exportMyData().subscribe({
      next: (data: object) => {
        const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = 'my-data-export.json';
        anchor.click();
        URL.revokeObjectURL(url);
        this.exporting.set(false);
      },
      error: () => {
        this.error.set('Failed to export data.');
        this.exporting.set(false);
      },
    });
  }

  deleteAccount(): void {
    this.dialog
      .open(ConfirmDeleteDialog, { width: '480px', disableClose: true })
      .afterClosed()
      .subscribe((password: string | undefined) => {
        if (!password) return;

        this.deleting.set(true);
        this.error.set(null);

        this.authService.deleteAccount(password).subscribe({
          error: (err: { error?: { errors?: string[] } }) => {
            this.error.set(err.error?.errors?.[0] ?? 'Failed to delete account.');
            this.deleting.set(false);
          },
        });
      });
  }
}
