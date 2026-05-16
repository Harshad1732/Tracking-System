export interface PlatformTenant {
  id: string;
  name: string;
  slug: string;
  isActive: boolean;
  userCount: number;
  shopfloorCount: number;
  sheetCount: number;
  planCode: string | null;
  subscriptionStatus: string | null;
  createdAtUtc: string;
}
