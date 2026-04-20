export const PERMISSIONS = {
  Users: {
    Read: 'Users.Read',
    Create: 'Users.Create',
    Update: 'Users.Update',
    Delete: 'Users.Delete',
    ToggleStatus: 'Users.ToggleStatus',
    AssignRoles: 'Users.AssignRoles',
  },
  Roles: {
    Read: 'Roles.Read',
    Create: 'Roles.Create',
    Update: 'Roles.Update',
    Delete: 'Roles.Delete',
  },
  AuditLog: {
    Read: 'AuditLog.Read',
    Export: 'AuditLog.Export',
  },
  Settings: {
    Read: 'Settings.Read',
    Manage: 'Settings.Manage',
  },
  Dashboard: {
    ViewStats: 'Dashboard.ViewStats',
  },
  SystemHealth: {
    Read: 'SystemHealth.Read',
  },
  Plans: {
    Read: 'Plans.Read',
    Create: 'Plans.Create',
    Update: 'Plans.Update',
  },
  Subscriptions: {
    Read: 'Subscriptions.Read',
    Manage: 'Subscriptions.Manage',
  },
} as const;
