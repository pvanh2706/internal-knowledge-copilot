import { apiRequest } from './http'

export type KnowledgeSourceKind = 'Local' | 'External'
export type KnowledgeSourceSyncMode = 'Manual' | 'Scheduled' | 'EventDriven'
export type KnowledgeSourceStatus = 'Active' | 'Syncing' | 'Failed' | 'Disabled' | 'Archived'
export type ExternalObjectStatus = 'Active' | 'Deleted' | 'Archived'

export interface KnowledgeSource {
  id: string
  tenantId: string
  applicationId: string
  applicationCode: string
  sourceType: KnowledgeSourceKind
  externalSourceId: string
  name: string
  syncMode: KnowledgeSourceSyncMode
  status: KnowledgeSourceStatus
  metadataJson?: string | null
  lastSyncStartedAt?: string | null
  lastSyncCompletedAt?: string | null
  lastSyncStatus?: string | null
  lastSyncError?: string | null
  createdAt: string
  updatedAt: string
}

export interface ExternalObject {
  id: string
  tenantId: string
  applicationId: string
  applicationCode: string
  knowledgeSourceId?: string | null
  objectType: string
  externalObjectId: string
  title: string
  url?: string | null
  metadataJson?: string | null
  contentHash?: string | null
  aclHash?: string | null
  status: ExternalObjectStatus
  lastSyncedAt?: string | null
  contentSyncedAt?: string | null
  aclSyncedAt?: string | null
  createdAt: string
  updatedAt: string
}

export function getKnowledgeSources(token: string, applicationId?: string) {
  const query = applicationId ? `?applicationId=${encodeURIComponent(applicationId)}` : ''
  return apiRequest<KnowledgeSource[]>(`/knowledge-sources${query}`, {}, token)
}

export function getExternalObjects(token: string, applicationId?: string, knowledgeSourceId?: string) {
  const params = new URLSearchParams()
  if (applicationId) params.set('applicationId', applicationId)
  if (knowledgeSourceId) params.set('knowledgeSourceId', knowledgeSourceId)
  const query = params.toString() ? `?${params}` : ''
  return apiRequest<ExternalObject[]>(`/knowledge-sources/external-objects${query}`, {}, token)
}
