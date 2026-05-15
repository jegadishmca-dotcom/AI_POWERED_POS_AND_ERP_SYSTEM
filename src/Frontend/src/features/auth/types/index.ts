export interface User {
  id: string;
  username: string;
  fullName: string;
  role: string;
  storeId?: string;
}

export interface AuthResponse {
  accessToken: string;
  user: User;
}
