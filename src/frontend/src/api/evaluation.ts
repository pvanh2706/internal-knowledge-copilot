import { apiRequest } from './http'
import type { AiScopeType } from './ai'

export interface EvaluationCase {
  id: string
  sourceFeedbackId?: string | null
  question: string
  expectedAnswer: string
  expectedKeywords: string[]
  scopeType: AiScopeType
  folderId?: string | null
  documentId?: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface EvaluationRunResult {
  id: string
  evaluationCaseId: string
  aiInteractionId?: string | null
  question: string
  actualAnswer: string
  passed: boolean
  score: number
  failureReason?: string | null
  createdAt: string
}

export interface EvaluationRun {
  id: string
  name?: string | null
  totalCases: number
  passedCases: number
  failedCases: number
  passRate: number
  createdAt: string
  finishedAt?: string | null
  results: EvaluationRunResult[]
}

export interface CreateEvaluationCasePayload {
  expectedAnswer: string
  expectedKeywords?: string[]
  scopeType?: AiScopeType | null
  folderId?: string | null
  documentId?: string | null
  isActive?: boolean
}

export function getEvaluationCases(token: string) {
  return apiRequest<EvaluationCase[]>('/evaluation/cases', {}, token)
}

export function createEvaluationCaseFromFeedback(feedbackId: string, payload: CreateEvaluationCasePayload, token: string) {
  return apiRequest<EvaluationCase>(
    `/evaluation/feedback/${feedbackId}/cases`,
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function getEvaluationRuns(token: string) {
  return apiRequest<EvaluationRun[]>('/evaluation/runs', {}, token)
}

export function runEvaluation(payload: { caseId?: string | null; name?: string | null }, token: string) {
  return apiRequest<EvaluationRun>(
    '/evaluation/runs',
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}
