import { API_BASE_URL, apiRequest } from './http'

export type DocumentStatus = 'PendingReview' | 'Approved' | 'Rejected' | 'Archived' | 'Deleted'
export type DocumentVersionStatus = 'PendingReview' | 'Approved' | 'Rejected' | 'Processing' | 'Indexed' | 'ProcessingFailed'

export interface DocumentListItem {
  id: string
  folderId: string
  folderPath: string
  title: string
  description?: string
  status: DocumentStatus
  currentVersionId?: string
  currentVersionNumber?: number
  latestVersionNumber: number
  latestVersionStatus: DocumentVersionStatus
  pendingVersionCount: number
  createdBy: string
  createdAt: string
  updatedAt: string
}

export interface DocumentVersion {
  id: string
  versionNumber: number
  originalFileName: string
  fileSizeBytes: number
  contentType?: string
  status: DocumentVersionStatus
  rejectReason?: string
  hasNormalizedText: boolean
  sectionCount?: number | null
  documentSummary?: string | null
  processingWarnings: string[]
  uploadedBy: string
  reviewedBy?: string
  reviewedAt?: string
  createdAt: string
}

export interface DocumentDetail {
  id: string
  folderId: string
  folderPath: string
  title: string
  description?: string
  status: DocumentStatus
  currentVersionId?: string
  versions: DocumentVersion[]
}

export function getDocuments(token: string, filters: { folderId?: string; status?: DocumentStatus; keyword?: string } = {}) {
  const search = new URLSearchParams()
  if (filters.folderId) search.set('folderId', filters.folderId)
  if (filters.status) search.set('status', filters.status)
  if (filters.keyword) search.set('keyword', filters.keyword)
  const query = search.toString()

  return apiRequest<DocumentListItem[]>(`/documents${query ? `?${query}` : ''}`, {}, token)
}

export function getDocument(id: string, token: string) {
  return apiRequest<DocumentDetail>(`/documents/${id}`, {}, token)
}

export function uploadDocument(payload: { folderId: string; title: string; description?: string; file: File }, token: string) {
  const formData = new FormData()
  formData.set('folderId', payload.folderId)
  formData.set('title', payload.title)
  if (payload.description) formData.set('description', payload.description)
  formData.set('file', payload.file)

  return apiRequest<DocumentDetail>('/documents', { method: 'POST', body: formData }, token)
}

export function uploadDocumentVersion(id: string, file: File, token: string) {
  const formData = new FormData()
  formData.set('file', file)

  return apiRequest<DocumentDetail>(`/documents/${id}/versions`, { method: 'POST', body: formData }, token)
}

export function approveDocument(id: string, versionId: string, token: string) {
  return apiRequest<void>(`/documents/${id}/approve`, { method: 'POST', body: JSON.stringify({ versionId }) }, token)
}

export function rejectDocument(id: string, versionId: string, reason: string, token: string) {
  return apiRequest<void>(`/documents/${id}/reject`, { method: 'POST', body: JSON.stringify({ versionId, reason }) }, token)
}

export async function downloadDocument(id: string, token: string, versionId?: string) {
  const search = versionId ? `?versionId=${encodeURIComponent(versionId)}` : ''
  const response = await fetch(`${API_BASE_URL}/documents/${id}/download${search}`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  })

  if (!response.ok) {
    throw new Error('Khong the tai file.')
  }

  const blob = await response.blob()
  const disposition = response.headers.get('content-disposition') ?? ''
  const fileName = disposition.match(/filename="?([^"]+)"?/i)?.[1] ?? 'document'
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = decodeURIComponent(fileName)
  link.click()
  URL.revokeObjectURL(url)
}
