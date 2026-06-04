import { apiRequest } from './http'

export type TenantStatus = 'Active' | 'Suspended' | 'Archived'

export interface Tenant {
  id: string
  code: string
  name: string
  status: TenantStatus
  createdAt: string
  updatedAt: string
}

export interface CreateTenantPayload {
  name: string
  code: string
  status?: TenantStatus
}

export interface UpdateTenantPayload {
  name?: string
  status?: TenantStatus
}

export function getTenants(token: string) {
  return apiRequest<Tenant[]>('/admin/tenants', {}, token)
}

export function createTenant(payload: CreateTenantPayload, token: string) {
  return apiRequest<Tenant>(
    '/admin/tenants',
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function updateTenant(id: string, payload: UpdateTenantPayload, token: string) {
  return apiRequest<Tenant>(
    `/admin/tenants/${id}`,
    {
      method: 'PATCH',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function deleteTenant(id: string, token: string) {
  return apiRequest<void>(
    `/admin/tenants/${id}`,
    {
      method: 'DELETE',
    },
    token,
  )
}
