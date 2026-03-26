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
  version: string;
  environment: string;
  uptime: Uptime;
  memory: Memory;
}
