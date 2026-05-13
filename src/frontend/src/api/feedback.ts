import { apiRequest } from './http'
import type { KnowledgeSourceType } from './ai'
import type { VisibilityScope } from './wiki'

export type AiFeedbackValue = 'Correct' | 'Incorrect'
export type FeedbackReviewStatus = 'New' | 'InReview' | 'Resolved'
export type AiQualityIssueStatus = 'New' | 'Classified' | 'InReview' | 'Resolved'
export type KnowledgeCorrectionStatus = 'Draft' | 'Approved' | 'Rejected'

export interface FeedbackSource {
  sourceType: KnowledgeSourceType
  title: string
  folderPath: string
  sectionTitle?: string | null
  excerpt: string
  rank: number
}

export interface FeedbackItem {
  id: string
  aiInteractionId: string
  userDisplayName: string
  question: string
  answer: string
  note?: string | null
  reviewStatus: FeedbackReviewStatus
  reviewerNote?: string | null
  createdAt: string
  updatedAt: string
  sources: FeedbackSource[]
}

export interface FeedbackResponse {
  id: string
  aiInteractionId: string
  value: AiFeedbackValue
  note?: string | null
  reviewStatus: FeedbackReviewStatus
  reviewerNote?: string | null
  createdAt: string
  updatedAt: string
}

export interface QualityIssue {
  id: string
  feedbackId: string
  aiInteractionId: string
  question: string
  answer: string
  userNote?: string | null
  status: AiQualityIssueStatus
  failureType?: string | null
  severity?: string | null
  rootCauseHypothesis?: string | null
  recommendedActions: string[]
  createdAt: string
  updatedAt: string
  corrections: KnowledgeCorrection[]
}

export interface KnowledgeCorrection {
  id: string
  qualityIssueId: string
  question: string
  correctionText: string
  visibilityScope: VisibilityScope
  folderId?: string | null
  documentId?: string | null
  status: KnowledgeCorrectionStatus
  rejectReason?: string | null
  createdAt: string
  updatedAt: string
  approvedAt?: string | null
}

export function submitAiFeedback(interactionId: string, payload: { value: AiFeedbackValue; note?: string }, token: string) {
  return apiRequest<FeedbackResponse>(
    `/ai/interactions/${interactionId}/feedback`,
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function getIncorrectFeedback(token: string) {
  return apiRequest<FeedbackItem[]>('/feedback/incorrect', {}, token)
}

export function updateFeedbackReviewStatus(id: string, payload: { status: FeedbackReviewStatus; reviewerNote?: string }, token: string) {
  return apiRequest<FeedbackResponse>(
    `/feedback/${id}/review-status`,
    {
      method: 'PATCH',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function getQualityIssues(token: string) {
  return apiRequest<QualityIssue[]>('/feedback/quality-issues', {}, token)
}

export function createCorrection(issueId: string, payload: { correctionText: string; visibilityScope: VisibilityScope; folderId?: string | null; isCompanyPublicConfirmed: boolean }, token: string) {
  return apiRequest<KnowledgeCorrection>(
    `/feedback/quality-issues/${issueId}/corrections`,
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function approveCorrection(correctionId: string, token: string) {
  return apiRequest<KnowledgeCorrection>(`/feedback/corrections/${correctionId}/approve`, { method: 'POST' }, token)
}

export function rejectCorrection(correctionId: string, reason: string, token: string) {
  return apiRequest<KnowledgeCorrection>(
    `/feedback/corrections/${correctionId}/reject`,
    {
      method: 'POST',
      body: JSON.stringify({ reason }),
    },
    token,
  )
}
