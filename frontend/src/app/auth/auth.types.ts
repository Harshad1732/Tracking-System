/** One (resource, action) pair the caller can perform in the current context. */
export interface PermissionGrantDto {
  resource: string;
  action: string;
}

/** Canonical resource codes — must match backend Tracker.Services.Resources. */
export const RESOURCES = {
  Sheets:     'Sheets',
  Batches:    'Batches',
  Customers:  'Customers',
  Employees:  'Employees',
  Plants:     'Plants',
  Shopfloors: 'Shopfloors',
  Processes:  'Processes',
  Users:      'Users',
  Roles:      'Roles',
  Reports:    'Reports',
  Workspace:  'Workspace'
} as const;
export type ResourceCode = typeof RESOURCES[keyof typeof RESOURCES];

/** Canonical action codes — must match backend Tracker.Services.Actions. */
export const ACTIONS = {
  View:   'View',
  Add:    'Add',
  Edit:   'Edit',
  Delete: 'Delete'
} as const;
export type ActionCode = typeof ACTIONS[keyof typeof ACTIONS];

export interface UserDto {
  id: string;
  email: string;
  fullName?: string | null;
  /** All role names the user holds in the current tenant+plant context. */
  roles: string[];
  /** True if any of the user's effective roles has IsSystemAdmin set. */
  isSystemAdmin: boolean;
  isPlatformAdmin: boolean;
  /** Union of all (resource, action) pairs granted by the user's role assignments. */
  permissions: PermissionGrantDto[];
  /** If set, the user's default plant. */
  lockedPlantId: string | null;
  /** The plant the current access token is bound to. */
  currentPlantId: string;
}

export interface TenantDto {
  id: string;
  name: string;
  slug: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
  user: UserDto;
  tenant: TenantDto;
}

/** Helper: does the grant list include this (resource, action)? */
export function hasPermission(grants: ReadonlyArray<PermissionGrantDto>,
                              resource: string, action: string): boolean {
  for (const g of grants) {
    if (g.resource === resource && g.action === action) return true;
  }
  return false;
}
