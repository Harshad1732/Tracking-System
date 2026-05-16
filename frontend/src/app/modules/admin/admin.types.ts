export interface AdminUserAssignment {
  id: string;
  roleId: string;
  roleName: string;
  isSystemAdmin: boolean;
  /** "Tenant" or "Plant". Tenant scope = workspace-wide; Plant scope = bound to a plant id. */
  scopeType: string;
  scopeId: string | null;
  scopeName: string | null;
}

export interface AdminUser {
  id: string;
  number: number;
  email: string;
  fullName: string | null;
  provider: string | null;
  isActive: boolean;
  hasPassword: boolean;
  defaultPlantId: string | null;
  defaultPlantName: string | null;
  isPlatformAdmin: boolean;
  assignments: AdminUserAssignment[];
  createdAtUtc: string;
}

export interface AssignmentInput {
  roleId: string;
  scopeType: string;
  scopeId: string | null;
}

export interface CreateUserInput {
  email: string;
  fullName: string | null;
  password: string;
  defaultPlantId: string | null;
  assignments: AssignmentInput[];
}

export interface UpdateUserInput {
  fullName: string | null;
  isActive: boolean;
  defaultPlantId: string | null;
  assignments: AssignmentInput[];
}

export interface Workspace {
  id: string;
  name: string;
  slug: string;
  createdAtUtc: string;
  userCount: number;
  shopfloorCount: number;
  plantCount: number;
}
