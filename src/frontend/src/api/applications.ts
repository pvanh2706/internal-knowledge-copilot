import { apiRequest } from './http'

export type ApplicationType = 'Internal' | 'Crm' | 'Sales' | 'Support' | 'Other'
export type ApplicationStatus = 'Active' | 'Disabled' | 'Archived'

export interface Application {
  id: string
  tenantId: string
  tenantCode: string
  code: string
  name: string
  applicationType: ApplicationType
  baseUrl?: string | null
  status: ApplicationStatus
  createdAt: string
  updatedAt: string
}

export interface CreateApplicationPayload {
  tenantId: string
  code: string
  name: string
  applicationType: ApplicationType
  baseUrl?: string | null
  status?: ApplicationStatus
}

export interface UpdateApplicationPayload {
  name?: string
  applicationType?: ApplicationType
  baseUrl?: string | null
  status?: ApplicationStatus
}

export function getApplications(token: string, tenantId?: string) {
  const query = tenantId ? `?tenantId=${encodeURIComponent(tenantId)}` : ''
  return apiRequest<Application[]>(`/admin/applications${query}`, {}, token)
}

export function createApplication(payload: CreateApplicationPayload, token: string) {
  return apiRequest<Application>(
    '/admin/applications',
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function updateApplication(id: string, payload: UpdateApplicationPayload, token: string) {
  return apiRequest<Application>(
    `/admin/applications/${id}`,
    {
      method: 'PATCH',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function deleteApplication(id: string, token: string) {
  return apiRequest<void>(
    `/admin/applications/${id}`,
    {
      method: 'DELETE',
    },
    token,
  )
}
