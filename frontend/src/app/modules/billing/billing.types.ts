export interface Plan {
  id: string;
  code: string;
  name: string;
  description: string | null;
  monthlyPriceCents: number;
  currency: string;
  maxSheets: number;
  maxUsers: number;
  maxShopfloors: number;
  /** -1 = unlimited */
  retentionDays: number;
  sortOrder: number;
  isActive: boolean;
}

export interface Usage {
  sheetsUsed: number;
  sheetsLimit: number;
  usersUsed: number;
  usersLimit: number;
  shopfloorsUsed: number;
  shopfloorsLimit: number;
}

export interface Subscription {
  id: string;
  plan: Plan;
  status: 'Trial' | 'Active' | 'PastDue' | 'Canceled' | 'Expired';
  trialEndsAtUtc: string | null;
  currentPeriodEndsAtUtc: string | null;
  canceledAtUtc: string | null;
  usage: Usage;
}
