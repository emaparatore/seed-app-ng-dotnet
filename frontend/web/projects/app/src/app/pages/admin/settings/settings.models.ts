export interface SystemSetting {
  key: string;
  value: string;
  type: string;
  category: string;
  description: string;
  modifiedBy: string | null;
  modifiedAt: string | null;
}

export interface UpdateSettingItem {
  key: string;
  value: string;
}

export interface SettingsGroup {
  category: string;
  settings: SystemSetting[];
}
