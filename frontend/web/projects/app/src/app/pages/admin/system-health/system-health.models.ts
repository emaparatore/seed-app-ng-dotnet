export interface ComponentStatus {
  status: string;
  description: string | null;
}

export interface Uptime {
  totalSeconds: number;
  formatted: string;
}

export interface Memory {
  workingSetMegabytes: number;
  gcAllocatedMegabytes: number;
}

export interface SystemHealth {
  database: ComponentStatus;
  email: ComponentStatus;
  paymentsWebhook: PaymentsWebhookStatus;
  version: string;
  environment: string;
  uptime: Uptime;
  memory: Memory;
}

export interface PaymentsWebhookStatus {
  status: string;
  description: string;
  lastWebhookReceivedAt: string | null;
  lastFailureAt: string | null;
  recentFailuresCount: number;
  pendingCheckoutsCount: number;
  stalePendingCheckoutsCount: number;
}
