import { apiRequest } from './http'

export type AiActionApprovalMode = 'Manual' | 'Rule'
export type AiActionRequestStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Executing'
  | 'Succeeded'
  | 'Failed'
  | 'Cancelled'

export interface AiActionRequest {
  id: string
  tenantId: string
  applicationId: string
  recommendationId: string
  actionType: string
  targetObjectType: string
  targetExternalObjectId: string
  payloadJson: string
  normalizedPayloadJson?: string | null
  approvalMode: AiActionApprovalMode
  status: AiActionRequestStatus
  idempotencyKey: string
  requestedByUserId?: string | null
  approvedByUserId?: string | null
  rejectedByUserId?: string | null
  executedByUserId?: string | null
  rejectionReason?: string | null
  cancellationReason?: string | null
  validationResultJson?: string | null
  ruleDecisionJson?: string | null
  externalExecutionId?: string | null
  executionResultJson?: string | null
  executionError?: string | null
  createdAt: string
  updatedAt: string
  approvedAt?: string | null
  rejectedAt?: string | null
  executingStartedAt?: string | null
  executedAt?: string | null
  cancelledAt?: string | null
}

export interface ActionQuery {
  applicationId?: string
  status?: AiActionRequestStatus
  recommendationId?: string
  objectType?: string
  externalObjectId?: string
}

export interface CreateAiActionPayload {
  actionType: string
  targetObjectType?: string | null
  targetExternalObjectId?: string | null
  payloadJson: string
  approvalMode?: AiActionApprovalMode
  idempotencyKey?: string | null
  createAsDraft?: boolean
}

export function getActionRequests(token: string, query: ActionQuery = {}) {
  const params = new URLSearchParams()
  if (query.applicationId) params.set('applicationId', query.applicationId)
  if (query.status) params.set('status', query.status)
  if (query.recommendationId) params.set('recommendationId', query.recommendationId)
  if (query.objectType) params.set('objectType', query.objectType)
  if (query.externalObjectId) params.set('externalObjectId', query.externalObjectId)
  const suffix = params.toString() ? `?${params}` : ''
  return apiRequest<AiActionRequest[]>(`/action-approvals${suffix}`, {}, token)
}

export function createActionRequest(recommendationId: string, payload: CreateAiActionPayload, token: string) {
  return apiRequest<AiActionRequest>(
    `/action-approvals/recommendations/${recommendationId}/actions`,
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function approveActionRequest(id: string, note: string | null, token: string) {
  return apiRequest<AiActionRequest>(
    `/action-approvals/${id}/approve`,
    {
      method: 'POST',
      body: JSON.stringify({ note }),
    },
    token,
  )
}

export function rejectActionRequest(id: string, reason: string, token: string) {
  return apiRequest<AiActionRequest>(
    `/action-approvals/${id}/reject`,
    {
      method: 'POST',
      body: JSON.stringify({ reason }),
    },
    token,
  )
}

export function executeActionRequest(id: string, token: string) {
  return apiRequest<AiActionRequest>(
    `/action-approvals/${id}/execute`,
    {
      method: 'POST',
    },
    token,
  )
}
