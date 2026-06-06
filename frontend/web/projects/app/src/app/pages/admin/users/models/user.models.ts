export interface AdminUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  roles: string[];
  createdAt: string;
}

export interface AdminUserDetail {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  roles: string[];
  createdAt: string;
  updatedAt: string;
  mustChangePassword: boolean;
  emailConfirmed: boolean;
  subscription: AdminUserSubscription | null;
}

export interface AdminUserSubscription {
  currentPlan: string;
  subscriptionStatus: string;
  trialEndsAt: string | null;
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface CreateUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  password: string;
  roleNames: string[];
}

export interface UpdateUserRequest {
  firstName: string;
  lastName: string;
  email: string;
}

export interface ToggleStatusRequest {
  isActive: boolean;
}

export interface AssignRolesRequest {
  roleNames: string[];
}

export interface AdminRole {
  id: string;
  name: string;
  description: string | null;
  isSystemRole: boolean;
  userCount: number;
  createdAt: string;
}

export interface GetUsersParams {
  pageNumber?: number;
  pageSize?: number;
  searchTerm?: string;
  roleFilter?: string;
  statusFilter?: boolean;
  dateFrom?: string;
  dateTo?: string;
  sortBy?: string;
  sortDescending?: boolean;
}
