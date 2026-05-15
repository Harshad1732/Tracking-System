export interface UserDto {
  id: string;
  email: string;
  fullName?: string | null;
  role: string;
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
