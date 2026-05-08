import { apiRequest } from './http'

export type UserRole = 'Admin' | 'User' | 'Reviewer'

export interface AuthUser {
  id: string
  displayName: string
  role: UserRole
}

export interface LoginResponse {
  accessToken: string
  mustChangePassword: boolean
  user: AuthUser
}

export function login(email: string, password: string) {
  return apiRequest<LoginResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
}

export function changePassword(currentPassword: string, newPassword: string, token: string) {
  return apiRequest<void>(
    '/auth/change-password',
    {
      method: 'POST',
      body: JSON.stringify({ currentPassword, newPassword }),
    },
    token,
  )
}
