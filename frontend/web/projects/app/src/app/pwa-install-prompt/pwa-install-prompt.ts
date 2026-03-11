import { Component, afterNextRender, inject } from '@angular/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

const DISMISSED_KEY = 'pwa-install-dismissed';

@Component({
  selector: 'app-pwa-install-prompt',
  standalone: true,
  imports: [MatSnackBarModule],
  template: '',
})
export class PwaInstallPrompt {
  private readonly snackBar = inject(MatSnackBar);
  private deferredPrompt: BeforeInstallPromptEvent | null = null;

  constructor() {
    afterNextRender(() => {
      if (localStorage.getItem(DISMISSED_KEY)) {
        return;
      }

      window.addEventListener('beforeinstallprompt', (event) => {
        event.preventDefault();
        this.deferredPrompt = event as BeforeInstallPromptEvent;
        this.showInstallBanner();
      });
    });
  }

  private showInstallBanner(): void {
    const snackBarRef = this.snackBar.open('Install Seed on your device?', 'Install', {
      duration: 15000,
      horizontalPosition: 'center',
      verticalPosition: 'bottom',
    });

    snackBarRef.onAction().subscribe(() => {
      this.deferredPrompt?.prompt();
      this.deferredPrompt = null;
    });

    snackBarRef.afterDismissed().subscribe((info) => {
      if (!info.dismissedByAction) {
        localStorage.setItem(DISMISSED_KEY, 'true');
      }
    });
  }
}

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<{ outcome: 'accepted' | 'dismissed' }>;
}
