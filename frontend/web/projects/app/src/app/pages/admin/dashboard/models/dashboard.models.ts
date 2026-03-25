export interface DashboardStats {
  totalUsers: number;
  activeUsers: number;
  inactiveUsers: number;
  registrationsLast7Days: number;
  registrationsLast30Days: number;
  registrationTrend: DailyRegistration[];
  usersByRole: RoleDistribution[];
  recentActivity: RecentActivity[];
}

export interface DailyRegistration {
  date: string;
  count: number;
}

export interface RoleDistribution {
  roleName: string;
  userCount: number;
}

export interface RecentActivity {
  id: string;
  timestamp: string;
  action: string;
  entityType: string;
  userId: string | null;
}
