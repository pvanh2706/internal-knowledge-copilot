import { apiRequest } from './http'

export interface AuditLogItem {
  id: string
  actorUserId?: string | null
  actorDisplayName?: string | null
  action: string
  entityType: string
  entityId?: string | null
  metadataJson?: string | null
  createdAt: string
}

export function getAuditLogs(token: string, filters: { action?: string; entityType?: string } = {}) {
  const query = new URLSearchParams()
  if (filters.action) query.set('action', filters.action)
  if (filters.entityType) query.set('entityType', filters.entityType)

  return apiRequest<AuditLogItem[]>(`/audit-logs${query.size ? `?${query}` : ''}`, {}, token)
}
