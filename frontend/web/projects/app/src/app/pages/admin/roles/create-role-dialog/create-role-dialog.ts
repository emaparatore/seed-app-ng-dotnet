import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AdminRolesService } from '../roles.service';
import { AdminRole, AdminRoleDetail, Permission } from '../models/role.models';

export interface CreateRoleDialogData {
  existingRoles: AdminRole[];
}

interface PermissionGroup {
  category: string;
  permissions: Permission[];
}

@Component({
  selector: 'app-create-role-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatCheckboxModule,
    MatTooltipModule,
  ],
  templateUrl: './create-role-dialog.html',
  styleUrl: './create-role-dialog.scss',
})
export class CreateRoleDialog implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly rolesService = inject(AdminRolesService);
  private readonly dialogRef = inject(MatDialogRef<CreateRoleDialog>);
  readonly data = inject<CreateRoleDialogData>(MAT_DIALOG_DATA);

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
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

  ngOnInit(): void {
    this.loadPermissions();
  }

  protected isPermissionSelected(permissionName: string): boolean {
    return this.selectedPermissions().has(permissionName);
  }

  protected togglePermission(permissionName: string): void {
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

  protected duplicateFrom(roleId: string): void {
    if (!roleId) {
      this.selectedPermissions.set(new Set());
      return;
    }
    this.rolesService.getRoleById(roleId).subscribe({
      next: (role: AdminRoleDetail) => {
        this.selectedPermissions.set(new Set(role.permissions));
      },
    });
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const request = {
      name: this.form.controls.name.value,
      description: this.form.controls.description.value,
      permissionNames: Array.from(this.selectedPermissions()),
    };

    this.rolesService.createRole(request).subscribe({
      next: (result) => {
        this.saving.set(false);
        this.dialogRef.close(result);
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(err.error?.errors?.[0] ?? 'Errore durante la creazione del ruolo.');
      },
    });
  }

  protected onCancel(): void {
    this.dialogRef.close(null);
  }

  private loadPermissions(): void {
    this.rolesService.getPermissions().subscribe({
      next: (permissions) => this.allPermissions.set(permissions),
    });
  }
}
