import { apiRequest } from './http'

export interface KnowledgeIndexSourceCount {
  sourceType: string
  count: number
}

export interface KnowledgeIndexSummary {
  ledgerChunkCount: number
  keywordIndexChunkCount: number
  ledgerSourceCounts: KnowledgeIndexSourceCount[]
}

export interface RebuildKnowledgeIndexPayload {
  resetVectorStore: boolean
  batchSize: number
}

export interface RebuildKnowledgeIndexResponse {
  totalLedgerChunks: number
  rebuiltChunks: number
  batchCount: number
  resetVectorStore: boolean
  sourceCounts: KnowledgeIndexSourceCount[]
  startedAt: string
  finishedAt: string
}

export function getKnowledgeIndexSummary(token: string) {
  return apiRequest<KnowledgeIndexSummary>('/knowledge-index/summary', {}, token)
}

export function rebuildKnowledgeIndex(payload: RebuildKnowledgeIndexPayload, token: string) {
  return apiRequest<RebuildKnowledgeIndexResponse>(
    '/knowledge-index/rebuild',
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}
