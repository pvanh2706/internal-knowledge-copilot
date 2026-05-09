import { apiRequest } from './http'

export interface FolderTreeItem {
  id: string
  parentId?: string
  name: string
  path: string
  children: FolderTreeItem[]
}

export interface FolderPermission {
  teamId: string
  teamName: string
  canView: boolean
}

export interface FolderDetail {
  id: string
  parentId?: string
  name: string
  path: string
  teamPermissions: FolderPermission[]
}

export function getFolderTree(token: string) {
  return apiRequest<FolderTreeItem[]>('/folders/tree', {}, token)
}

export function getFolderDetail(id: string, token: string) {
  return apiRequest<FolderDetail>(`/folders/${id}`, {}, token)
}

export function createFolder(payload: { parentId?: string; name: string }, token: string) {
  return apiRequest<FolderDetail>(
    '/folders',
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function updateFolder(id: string, payload: { parentId?: string; name: string }, token: string) {
  return apiRequest<void>(
    `/folders/${id}`,
    {
      method: 'PATCH',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function deleteFolder(id: string, token: string) {
  return apiRequest<void>(
    `/folders/${id}`,
    {
      method: 'DELETE',
    },
    token,
  )
}

export function updateFolderPermissions(
  id: string,
  teamPermissions: Array<{ teamId: string; canView: boolean }>,
  token: string,
) {
  return apiRequest<void>(
    `/folders/${id}/permissions`,
    {
      method: 'PUT',
      body: JSON.stringify({ teamPermissions }),
    },
    token,
  )
}
