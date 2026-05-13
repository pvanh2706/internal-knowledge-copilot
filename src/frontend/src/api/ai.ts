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

export interface RetrievalQueryUnderstanding {
  rewrittenQuestion: string
  normalizedQuestion: string
  keywords: string[]
}

export interface RetrievalFilter {
  sourceTypes: string[]
  statuses: string[]
  includeCompanyVisible: boolean
  visibleFolderCount: number
  filteredFolderCount: number
  documentId?: string | null
}

export interface RetrievalCandidateStats {
  vectorCandidateCount: number
  keywordCandidateCount: number
  mergedCandidateCount: number
  allowedCandidateCount: number
  finalContextCount: number
}

export interface RetrievalCandidate {
  candidateId: string
  retrievalSource: string
  sourceType: string
  sourceId: string
  title: string
  folderPath: string
  sectionTitle?: string | null
  sectionIndex?: number | null
  distance?: number | null
  score: number
  matchedKeywords: string[]
  scoreReasons: string[]
  passedPermissionFilter: boolean
  selectedForContext: boolean
  decision: string
  excerpt: string
}

export interface RetrievalExplainResponse {
  question: string
  scopeType: AiScopeType
  queryUnderstanding: RetrievalQueryUnderstanding
  filter: RetrievalFilter
  candidateStats: RetrievalCandidateStats
  finalContext: RetrievalCandidate[]
  candidates: RetrievalCandidate[]
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

export function explainRetrieval(payload: AskQuestionPayload, token: string) {
  return apiRequest<RetrievalExplainResponse>(
    '/ai/retrieval/explain',
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}
