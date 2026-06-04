import { apiRequest } from './http'

export type IntegrationAuthMode = 'None' | 'InternalApiKey'
export type IntegrationConnectionStatus = 'Active' | 'Disabled' | 'Archived'

export interface IntegrationConnection {
  id: string
  tenantId: string
  applicationId: string
  applicationCode: string
  name: string
  baseUrl: string
  authMode: IntegrationAuthMode
  status: IntegrationConnectionStatus
  secretReference: string
  secretConfigured: boolean
  secretRotatedAt?: string | null
  metadataJson?: string | null
  createdAt: string
  updatedAt: string
}

export interface CreateIntegrationConnectionPayload {
  applicationId: string
  name: string
  baseUrl: string
  authMode: IntegrationAuthMode
  secretReference: string
  secretValue?: string | null
  status?: IntegrationConnectionStatus
  metadataJson?: string | null
}

export function getIntegrationConnections(token: string, applicationId?: string) {
  const query = applicationId ? `?applicationId=${encodeURIComponent(applicationId)}` : ''
  return apiRequest<IntegrationConnection[]>(`/integrations/connections${query}`, {}, token)
}

export function createIntegrationConnection(payload: CreateIntegrationConnectionPayload, token: string) {
  return apiRequest<IntegrationConnection>(
    '/integrations/connections',
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}
