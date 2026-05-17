import { apiRequest } from './http'

export interface DataResetStatus {
  enabled: boolean
  confirmationPhrase: string
  keepsUsersTeamsAndAiSettings: boolean
}

export interface DataResetResult {
  completedAt: string
  databaseRowsDeleted: number
  storageItemsDeleted: number
  vectorStoreReset: boolean
  usersTeamsAndAiSettingsPreserved: boolean
}

export async function getDataResetStatus(token: string) {
  return apiRequest<DataResetStatus>('/admin/data-reset', {}, token)
}

export async function resetData(
  token: string,
  payload: {
    confirmationPhrase: string
    resetStorage: boolean
    resetVectorStore: boolean
  },
) {
  return apiRequest<DataResetResult>(
    '/admin/data-reset',
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}
