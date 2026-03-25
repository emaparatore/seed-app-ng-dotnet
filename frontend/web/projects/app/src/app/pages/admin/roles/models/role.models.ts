export interface AdminRole {
  id: string;
  name: string;
  description: string | null;
  isSystemRole: boolean;
  userCount: number;
  createdAt: string;
}

export interface AdminRoleDetail extends AdminRole {
  permissions: string[];
}

export interface Permission {
  id: string;
  name: string;
  description: string;
  category: string;
}

export interface CreateRoleRequest {
  name: string;
  description: string;
  permissionNames: string[];
}

export interface UpdateRoleRequest {
  name: string;
  description: string;
  permissionNames: string[];
}
