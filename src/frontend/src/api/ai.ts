import { apiRequest } from './http'

export type AiScopeType = 'All' | 'Folder' | 'Document'
export type KnowledgeSourceType = 'Document' | 'Wiki' | 'Correction'
export type AiConfidence = 'high' | 'medium' | 'low'

export interface AskQuestionPayload {
  question: string
  scopeType: AiScopeType
  folderId?: string | null
  documentId?: string | null
}

export interface AiCitation {
  sourceType: KnowledgeSourceType
  title: string
  folderPath: string
  sectionTitle?: string | null
  excerpt: string
}

export interface AskQuestionResponse {
  interactionId: string
  answer: string
  needsClarification: boolean
  confidence: AiConfidence
  missingInformation: string[]
  conflicts: string[]
  suggestedFollowUps: string[]
  citations: AiCitation[]
}

export function askQuestion(payload: AskQuestionPayload, token: string) {
  return apiRequest<AskQuestionResponse>(
    '/ai/ask',
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}
