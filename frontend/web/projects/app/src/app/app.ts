import { Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { AuthService, PermissionService } from 'shared-auth';
import { PwaInstallPrompt } from './pwa-install-prompt/pwa-install-prompt';
import { AppFooter } from './footer/footer';
import { ConfigService } from './services/config.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, MatToolbarModule, MatButtonModule, MatIconModule, MatMenuModule, PwaInstallPrompt, AppFooter],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly authService = inject(AuthService);
  protected readonly permissionService = inject(PermissionService);
  protected readonly configService = inject(ConfigService);
}
