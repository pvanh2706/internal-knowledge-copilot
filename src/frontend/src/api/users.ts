import type { UserRole } from './auth'
import { apiRequest } from './http'

export interface UserListItem {
  id: string
  email: string
  displayName: string
  role: UserRole
  primaryTeamId?: string
  primaryTeamName?: string
  mustChangePassword: boolean
  isActive: boolean
}

export interface CreateUserPayload {
  email: string
  displayName: string
  role: UserRole
  primaryTeamId?: string
  initialPassword: string
}

export function getUsers(token: string) {
  return apiRequest<UserListItem[]>('/users', {}, token)
}

export function createUser(payload: CreateUserPayload, token: string) {
  return apiRequest<UserListItem>(
    '/users',
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}
