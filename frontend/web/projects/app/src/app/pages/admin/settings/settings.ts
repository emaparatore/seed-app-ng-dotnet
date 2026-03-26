import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { PERMISSIONS, PermissionService } from 'shared-auth';
import { ConfirmDialog } from '../users/confirm-dialog/confirm-dialog';
import { SettingsService } from './settings.service';
import { SettingsGroup, SystemSetting, UpdateSettingItem } from './settings.models';

@Component({
  selector: 'app-settings',
  imports: [
    DatePipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatProgressBarModule,
  ],
  template: `
    <div class="settings-page">
      <div class="page-header">
        <h1>Impostazioni di Sistema</h1>
        @if (canManage() && hasChanges()) {
          <button mat-flat-button (click)="save()" [disabled]="saving()">
            <mat-icon>save</mat-icon>
            Salva modifiche
          </button>
        }
      </div>

      @if (saving()) {
        <mat-progress-bar mode="indeterminate" />
      }

      @if (loading()) {
        <div class="skeleton-container">
          @for (i of [1, 2, 3, 4]; track i) {
            <div class="skeleton-card">
              <div class="skeleton-title"></div>
              @for (j of [1, 2]; track j) {
                <div class="skeleton-row">
                  <div class="skeleton-label"></div>
                  <div class="skeleton-input"></div>
                </div>
              }
            </div>
          }
        </div>
      } @else if (error()) {
        <mat-card class="error-card">
          <mat-card-content>
            <div class="error-content">
              <mat-icon>error_outline</mat-icon>
              <p>{{ error() }}</p>
              <button mat-flat-button (click)="reload()">
                <mat-icon>refresh</mat-icon>
                Riprova
              </button>
            </div>
          </mat-card-content>
        </mat-card>
      } @else {
        @for (group of settingsGroups(); track group.category) {
          <mat-card class="settings-card">
            <mat-card-header>
              <mat-card-title>{{ group.category }}</mat-card-title>
            </mat-card-header>
            <mat-card-content>
              @for (setting of group.settings; track setting.key) {
                <div class="setting-item">
                  <div class="setting-control">
                    @switch (setting.type) {
                      @case ('bool') {
                        <mat-slide-toggle
                          [checked]="currentValues().get(setting.key) === 'true'"
                          [disabled]="!canManage() || saving()"
                          (change)="onValueChange(setting.key, $event.checked ? 'true' : 'false')"
                        >
                          {{ setting.description }}
                        </mat-slide-toggle>
                      }
                      @case ('int') {
                        <mat-form-field appearance="outline" class="setting-field">
                          <mat-label>{{ setting.description }}</mat-label>
                          <input
                            matInput
                            type="number"
                            [value]="currentValues().get(setting.key)"
                            [disabled]="!canManage() || saving()"
                            (input)="onValueChange(setting.key, $any($event.target).value)"
                          />
                        </mat-form-field>
                      }
                      @default {
                        <mat-form-field appearance="outline" class="setting-field">
                          <mat-label>{{ setting.description }}</mat-label>
                          <input
                            matInput
                            type="text"
                            [value]="currentValues().get(setting.key)"
                            [disabled]="!canManage() || saving()"
                            (input)="onValueChange(setting.key, $any($event.target).value)"
                          />
                        </mat-form-field>
                      }
                    }
                  </div>
                  @if (setting.modifiedAt) {
                    <div class="setting-meta">
                      Ultima modifica: {{ setting.modifiedAt | date: 'dd/MM/yyyy HH:mm' }}
                      @if (setting.modifiedBy) {
                        da {{ setting.modifiedBy }}
                      }
                    </div>
                  }
                </div>
              }
            </mat-card-content>
          </mat-card>
        }
      }
    </div>
  `,
  styles: `
    .settings-page {
      max-width: 800px;
    }

    .page-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 24px;

      h1 {
        margin: 0;
        font-size: 24px;
        font-weight: 400;
      }

      button mat-icon {
        margin-right: 8px;
      }
    }

    mat-progress-bar {
      margin-bottom: 16px;
    }

    .settings-card {
      margin-bottom: 16px;
    }

    mat-card-header {
      margin-bottom: 8px;
    }

    .setting-item {
      padding: 12px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant, rgba(0, 0, 0, 0.12));

      &:last-child {
        border-bottom: none;
      }
    }

    .setting-control {
      display: flex;
      align-items: center;
    }

    .setting-field {
      width: 100%;
    }

    .setting-meta {
      margin-top: 4px;
      font-size: 12px;
      color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
    }

    .error-card {
      .error-content {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 16px;
        padding: 24px;
        text-align: center;

        mat-icon {
          font-size: 48px;
          width: 48px;
          height: 48px;
          color: var(--mat-sys-error, #b3261e);
        }

        p {
          margin: 0;
          color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
        }
      }
    }

    .skeleton-container {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .skeleton-card {
      background: var(--mat-sys-surface-container, #f3edf7);
      border-radius: 12px;
      padding: 24px;
      animation: pulse 1.5s ease-in-out infinite;
    }

    .skeleton-title {
      width: 120px;
      height: 20px;
      border-radius: 4px;
      background: var(--mat-sys-surface-container-high, #ece6f0);
      margin-bottom: 20px;
    }

    .skeleton-row {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin-bottom: 16px;

      &:last-child {
        margin-bottom: 0;
      }
    }

    .skeleton-label {
      width: 200px;
      height: 14px;
      border-radius: 4px;
      background: var(--mat-sys-surface-container-high, #ece6f0);
    }

    .skeleton-input {
      width: 100%;
      height: 40px;
      border-radius: 4px;
      background: var(--mat-sys-surface-container-high, #ece6f0);
    }

    @keyframes pulse {
      0%,
      100% {
        opacity: 1;
      }
      50% {
        opacity: 0.5;
      }
    }
  `,
})
export class SettingsComponent implements OnInit {
  private readonly settingsService = inject(SettingsService);
  private readonly permissionService = inject(PermissionService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly settings = signal<SystemSetting[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly currentValues = signal<Map<string, string>>(new Map());

  private originalValues = new Map<string, string>();

  protected readonly canManage = computed(() => this.permissionService.hasPermission(PERMISSIONS.Settings.Manage));

  protected readonly settingsGroups = computed<SettingsGroup[]>(() => {
    const items = this.settings();
    const groups = new Map<string, SystemSetting[]>();
    for (const s of items) {
      const list = groups.get(s.category) ?? [];
      list.push(s);
      groups.set(s.category, list);
    }
    return Array.from(groups.entries())
      .map(([category, settings]) => ({ category, settings }))
      .sort((a, b) => a.category.localeCompare(b.category));
  });

  protected readonly hasChanges = computed(() => {
    const current = this.currentValues();
    for (const [key, value] of this.originalValues) {
      if (current.get(key) !== value) return true;
    }
    return false;
  });

  ngOnInit(): void {
    this.loadSettings();
  }

  protected onValueChange(key: string, value: string): void {
    const updated = new Map(this.currentValues());
    updated.set(key, value);
    this.currentValues.set(updated);
  }

  protected save(): void {
    const changedItems: UpdateSettingItem[] = [];
    const current = this.currentValues();
    for (const [key, value] of this.originalValues) {
      const newValue = current.get(key);
      if (newValue !== undefined && newValue !== value) {
        changedItems.push({ key, value: newValue });
      }
    }

    if (changedItems.length === 0) return;

    const dialogRef = this.dialog.open(ConfirmDialog, {
      width: '400px',
      data: {
        title: 'Salva impostazioni',
        message: `Sei sicuro di voler salvare le modifiche a ${changedItems.length} ${changedItems.length === 1 ? 'impostazione' : 'impostazioni'}?`,
        confirmText: 'Salva',
        cancelText: 'Annulla',
      },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.saving.set(true);
        this.settingsService.updateSettings(changedItems).subscribe({
          next: () => {
            this.saving.set(false);
            this.snackBar.open('Impostazioni salvate con successo', 'Chiudi', { duration: 3000 });
            this.loadSettings();
          },
          error: (err) => {
            this.saving.set(false);
            this.snackBar.open(err.error?.errors?.[0] ?? 'Errore durante il salvataggio', 'Chiudi', { duration: 5000 });
          },
        });
      }
    });
  }

  protected reload(): void {
    this.loadSettings();
  }

  private loadSettings(): void {
    this.loading.set(true);
    this.error.set(null);
    this.settingsService.getSettings().subscribe({
      next: (settings) => {
        this.settings.set(settings);
        const values = new Map<string, string>();
        for (const s of settings) {
          values.set(s.key, s.value);
        }
        this.originalValues = new Map(values);
        this.currentValues.set(values);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore nel caricamento delle impostazioni.');
        this.loading.set(false);
      },
    });
  }
}
