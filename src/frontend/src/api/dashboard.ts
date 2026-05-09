import { apiRequest } from './http'

export interface NamedCount {
  name: string
  count: number
}

export interface TopCitedSource {
  sourceType: string
  title: string
  folderPath: string
  count: number
}

export interface DashboardSummary {
  documentCounts: NamedCount[]
  wikiCounts: NamedCount[]
  aiQuestionCount: number
  feedbackCorrectCount: number
  feedbackIncorrectCount: number
  incorrectFeedbackPendingCount: number
  topCitedSources: TopCitedSource[]
}

export function getDashboardSummary(token: string) {
  return apiRequest<DashboardSummary>('/dashboard/summary', {}, token)
}
