import { apiRequest } from './http'

export interface AiProviderSettings {
  name: string
  baseUrl: string
  apiKey?: string | null
  hasApiKey: boolean
  apiKeyHeaderName: string
  chatEndpointMode: string
  chatModel: string
  fastModel: string
  embeddingProviderName: string
  embeddingBaseUrl: string
  embeddingApiKey?: string | null
  hasEmbeddingApiKey: boolean
  embeddingApiKeyHeaderName: string
  embeddingModel: string
  embeddingDimension: number
  reasoningEffort: string
  temperature?: number | null
  maxOutputTokens: number
  timeoutSeconds: number
  updatedAt?: string | null
}

export interface UpdateAiProviderSettingsPayload {
  name: string
  baseUrl: string
  apiKey?: string | null
  clearApiKey: boolean
  apiKeyHeaderName: string
  chatEndpointMode: string
  chatModel: string
  fastModel: string
  embeddingProviderName: string
  embeddingBaseUrl: string
  embeddingApiKey?: string | null
  clearEmbeddingApiKey: boolean
  embeddingApiKeyHeaderName: string
  embeddingModel: string
  embeddingDimension: number
  reasoningEffort: string
  temperature?: number | null
  maxOutputTokens: number
  timeoutSeconds: number
}

export interface TestAiProviderSettingsResponse {
  success: boolean
  providerName: string
  message: string
}

export interface AiProviderConfigurationStatus {
  providerName: string
  hasLlmApiKey: boolean
  usesMockLlmProvider: boolean
}

export function getAiProviderSettings(token: string) {
  return apiRequest<AiProviderSettings>('/admin/ai-settings', {}, token)
}

export function getAiProviderConfigurationStatus(token: string) {
  return apiRequest<AiProviderConfigurationStatus>('/ai-settings/status', {}, token)
}

export function updateAiProviderSettings(payload: UpdateAiProviderSettingsPayload, token: string) {
  return apiRequest<AiProviderSettings>(
    '/admin/ai-settings',
    {
      method: 'PUT',
      body: JSON.stringify(payload),
    },
    token,
  )
}

export function testAiProviderSettings(token: string) {
  return apiRequest<TestAiProviderSettingsResponse>(
    '/admin/ai-settings/test',
    {
      method: 'POST',
    },
    token,
  )
}
