import type { KnowledgeSourceKind } from './knowledgeSources'
import { apiRequest } from './http'

export type AiRecommendationStatus = 'Ready' | 'Dismissed' | 'FeedbackReceived'
export type AiRecommendationFeedbackValue = 'Helpful' | 'NotHelpful' | 'NeedsReview'

export interface WorkflowRecommendationSource {
  sourceType: KnowledgeSourceKind | 'Document' | 'Wiki' | 'ExternalObject' | 'Correction'
  sourceId: string
  applicationId?: string | null
  knowledgeSourceId?: string | null
  documentId?: string | null
  wikiPageId?: string | null
  externalObjectType?: string | null
  externalObjectId?: string | null
  title: string
  folderPath: string
  sectionTitle?: string | null
  sectionIndex?: number | null
  excerpt: string
  rank: number
}

export interface WorkflowRecommendation {
  id: string
  tenantId: string
  applicationId: string
  domainEventId: string
  workflowDefinitionId: string
  objectType: string
  externalObjectId: string
  title: string
  summary: string
  recommendedNextSteps: string[]
  risks: string[]
  clarificationQuestions: string[]
  suggestedTasks: string[]
  warnings: string[]
  wonLostSignals: string[]
  reasoningLabel: string
  sources: WorkflowRecommendationSource[]
  status: AiRecommendationStatus
  feedbackValue?: AiRecommendationFeedbackValue | null
  feedbackNote?: string | null
  createdAt: string
  updatedAt: string
}

export interface RecommendationQuery {
  applicationId?: string
  objectType?: string
  externalObjectId?: string
}

export interface RecommendationFeedbackPayload {
  value: AiRecommendationFeedbackValue
  note?: string | null
}

export function getWorkflowRecommendations(token: string, query: RecommendationQuery = {}) {
  const params = new URLSearchParams()
  if (query.applicationId) params.set('applicationId', query.applicationId)
  if (query.objectType) params.set('objectType', query.objectType)
  if (query.externalObjectId) params.set('externalObjectId', query.externalObjectId)
  const suffix = params.toString() ? `?${params}` : ''
  return apiRequest<WorkflowRecommendation[]>(`/workflow-copilot/recommendations${suffix}`, {}, token)
}

export function submitRecommendationFeedback(id: string, payload: RecommendationFeedbackPayload, token: string) {
  return apiRequest<WorkflowRecommendation>(
    `/workflow-copilot/recommendations/${id}/feedback`,
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}
