export interface AuditLogEntry {
  id: string;
  timestamp: string;
  userId: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  details: string | null;
  ipAddress: string | null;
  userAgent: string | null;
}

export interface GetAuditLogParams {
  pageNumber?: number;
  pageSize?: number;
  actionFilter?: string;
  userId?: string;
  dateFrom?: string;
  dateTo?: string;
  searchTerm?: string;
  sortDescending?: boolean;
}

export const AUDIT_ACTIONS = [
  'UserCreated',
  'UserUpdated',
  'UserDeleted',
  'UserStatusChanged',
  'UserRolesChanged',
  'RoleCreated',
  'RoleUpdated',
  'RoleDeleted',
  'LoginSuccess',
  'LoginFailed',
  'Logout',
  'PasswordChanged',
  'PasswordReset',
  'PasswordResetRequested',
  'SettingsChanged',
  'SystemSeeding',
  'AccountDeleted',
  'EmailConfirmed',
] as const;
