import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { HasPermissionDirective, PERMISSIONS, PermissionService } from 'shared-auth';
import { AdminRolesService } from '../roles.service';
import { AdminRoleDetail, Permission, UpdateRoleRequest } from '../models/role.models';

interface PermissionGroup {
  category: string;
  permissions: Permission[];
}

@Component({
  selector: 'app-role-detail',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatTooltipModule,
    HasPermissionDirective,
  ],
  templateUrl: './role-detail.html',
  styleUrl: './role-detail.scss',
})
export class RoleDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly rolesService = inject(AdminRolesService);
  private readonly permissionService = inject(PermissionService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly permissions = PERMISSIONS;
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly role = signal<AdminRoleDetail | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly allPermissions = signal<Permission[]>([]);
  protected readonly selectedPermissions = signal<Set<string>>(new Set());

  protected readonly permissionGroups = computed<PermissionGroup[]>(() => {
    const perms = this.allPermissions();
    const groups = new Map<string, Permission[]>();
    for (const p of perms) {
      const list = groups.get(p.category) ?? [];
      list.push(p);
      groups.set(p.category, list);
    }
    return Array.from(groups.entries())
      .map(([category, permissions]) => ({ category, permissions }))
      .sort((a, b) => a.category.localeCompare(b.category));
  });

  protected readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    description: [''],
  });

  private roleId = '';

  ngOnInit(): void {
    this.roleId = this.route.snapshot.params['id'];
    this.loadRole();
    this.loadPermissions();
  }

  protected hasPermission(permission: string): boolean {
    return this.permissionService.hasPermission(permission);
  }

  protected isReadonly(): boolean {
    const r = this.role();
    return !!r && r.name === 'SuperAdmin';
  }

  protected isPermissionSelected(permissionName: string): boolean {
    return this.selectedPermissions().has(permissionName);
  }

  protected togglePermission(permissionName: string): void {
    if (this.isReadonly()) return;
    const selected = new Set(this.selectedPermissions());
    if (selected.has(permissionName)) {
      selected.delete(permissionName);
    } else {
      selected.add(permissionName);
    }
    this.selectedPermissions.set(selected);
  }

  protected isGroupAllSelected(group: PermissionGroup): boolean {
    return group.permissions.every((p) => this.selectedPermissions().has(p.name));
  }

  protected isGroupPartiallySelected(group: PermissionGroup): boolean {
    const selected = this.selectedPermissions();
    const some = group.permissions.some((p) => selected.has(p.name));
    const all = group.permissions.every((p) => selected.has(p.name));
    return some && !all;
  }

  protected toggleGroup(group: PermissionGroup): void {
    if (this.isReadonly()) return;
    const selected = new Set(this.selectedPermissions());
    const allSelected = this.isGroupAllSelected(group);
    for (const p of group.permissions) {
      if (allSelected) {
        selected.delete(p.name);
      } else {
        selected.add(p.name);
      }
    }
    this.selectedPermissions.set(selected);
  }

  protected saveChanges(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const request: UpdateRoleRequest = {
      name: this.form.controls.name.value,
      description: this.form.controls.description.value,
      permissionNames: Array.from(this.selectedPermissions()),
    };

    this.rolesService.updateRole(this.roleId, request).subscribe({
      next: () => {
        this.saving.set(false);
        this.snackBar.open('Ruolo aggiornato con successo', 'Chiudi', { duration: 3000 });
        this.loadRole();
      },
      error: (err) => {
        this.saving.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Errore durante il salvataggio', 'Chiudi', { duration: 5000 });
      },
    });
  }

  protected goBack(): void {
    this.router.navigate(['/admin/roles']);
  }

  protected reload(): void {
    this.loadRole();
  }

  private loadRole(): void {
    this.loading.set(true);
    this.error.set(null);
    this.rolesService.getRoleById(this.roleId).subscribe({
      next: (role) => {
        this.role.set(role);
        this.form.patchValue({
          name: role.name,
          description: role.description ?? '',
        });
        this.selectedPermissions.set(new Set(role.permissions));
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.errors?.[0] ?? 'Errore nel caricamento del ruolo.');
        this.loading.set(false);
      },
    });
  }

  private loadPermissions(): void {
    this.rolesService.getPermissions().subscribe({
      next: (permissions) => this.allPermissions.set(permissions),
    });
  }
}
