export interface AdminUser {
  id: string;
  number: number;
  email: string;
  fullName: string | null;
  role: string;
  provider: string | null;
  isActive: boolean;
  hasPassword: boolean;
  createdAtUtc: string;
}

export interface CreateUserInput {
  email: string;
  fullName: string | null;
  role: string;
  password: string;
}

export interface UpdateUserInput {
  fullName: string | null;
  role: string;
  isActive: boolean;
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
