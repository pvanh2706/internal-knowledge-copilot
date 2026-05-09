import { apiRequest } from './http'

export type WikiStatus = 'Draft' | 'Published' | 'Rejected' | 'Archived'
export type VisibilityScope = 'Folder' | 'Company'

export interface WikiDraftListItem {
  id: string
  sourceDocumentId: string
  sourceDocumentVersionId: string
  title: string
  sourceDocumentTitle: string
  folderPath: string
  language: string
  status: WikiStatus
  createdAt: string
  updatedAt: string
}

export interface WikiDraftDetail extends WikiDraftListItem {
  content: string
  rejectReason?: string | null
  reviewedAt?: string | null
}

export interface WikiPage {
  id: string
  sourceDraftId: string
  title: string
  content: string
  language: string
  visibilityScope: VisibilityScope
  folderId?: string | null
  folderPath?: string | null
  publishedAt: string
}

export function generateWikiDraft(payload: { documentId: string; documentVersionId: string }, token: string) {
  return apiRequest<WikiDraftDetail>(
    '/wiki/generate',
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function getWikiDrafts(token: string) {
  return apiRequest<WikiDraftListItem[]>('/wiki/drafts', {}, token)
}

export function getWikiDraft(id: string, token: string) {
  return apiRequest<WikiDraftDetail>(`/wiki/drafts/${id}`, {}, token)
}

export function publishWikiDraft(id: string, payload: { visibilityScope: VisibilityScope; folderId?: string | null; isCompanyPublicConfirmed: boolean }, token: string) {
  return apiRequest<WikiPage>(
    `/wiki/drafts/${id}/publish`,
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function rejectWikiDraft(id: string, reason: string, token: string) {
  return apiRequest<WikiDraftDetail>(
    `/wiki/drafts/${id}/reject`,
    {
      method: 'POST',
      body: JSON.stringify({ reason }),
    },
    token,
  )
}
