import { apiRequest } from './http'

export type AiScopeType = 'All' | 'Folder' | 'Document'
export type KnowledgeSourceType = 'Document' | 'Wiki'

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
  excerpt: string
}

export interface AskQuestionResponse {
  interactionId: string
  answer: string
  needsClarification: boolean
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
