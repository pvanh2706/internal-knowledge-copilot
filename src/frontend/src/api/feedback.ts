import { apiRequest } from './http'
import type { KnowledgeSourceType } from './ai'

export type AiFeedbackValue = 'Correct' | 'Incorrect'
export type FeedbackReviewStatus = 'New' | 'InReview' | 'Resolved'

export interface FeedbackSource {
  sourceType: KnowledgeSourceType
  title: string
  folderPath: string
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
