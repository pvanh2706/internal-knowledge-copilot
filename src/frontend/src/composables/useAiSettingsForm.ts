import { computed, ref } from 'vue'
import {
  getAiProviderSettings,
  testAiProviderSettings,
  updateAiProviderSettings,
  type AiProviderSettings,
  type TestAiProviderSettingsResponse,
} from '@/api/aiSettings'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'

// Toàn bộ state + logic preset/payload của trang Cấu hình AI — tách từ AiSettingsPage
// (refactor thuần, không đổi hành vi) để template chỉ còn lo hiển thị.
export function useAiSettingsForm() {
  const authStore = useAuthStore()

  const isLoading = ref(false)
  const isSaving = ref(false)
  const isTesting = ref(false)
  const useSharedKeyForEmbedding = ref(true)
  const errorMessage = ref('')
  const successMessage = ref('')
  const testResult = ref<TestAiProviderSettingsResponse | null>(null)
  const hasSavedApiKey = ref(false)
  const hasSavedEmbeddingApiKey = ref(false)
  const updatedText = ref('—')

  const form = ref({
    name: 'mock',
    baseUrl: 'https://api.openai.com/v1',
    apiKey: '',
    clearApiKey: false,
    apiKeyHeaderName: 'Authorization',
    chatEndpointMode: 'responses',
    chatModel: 'gpt-5.5',
    fastModel: 'gpt-5.5',
    embeddingProviderName: 'mock',
    embeddingBaseUrl: 'https://api.openai.com/v1',
    embeddingApiKey: '',
    clearEmbeddingApiKey: false,
    embeddingApiKeyHeaderName: 'Authorization',
    embeddingModel: 'text-embedding-3-large',
    embeddingDimension: 3072,
    reasoningEffort: 'medium',
    temperature: null as number | null,
    maxOutputTokens: 2500,
    timeoutSeconds: 60,
  })

  const isMockProvider = computed(() => form.value.name === 'mock')
  const isOpenAiProvider = computed(() => form.value.name === 'openai')
  const isAnthropicProvider = computed(() => form.value.name === 'anthropic')
  const isExternalProvider = computed(() => isOpenAiProvider.value || isAnthropicProvider.value)
  const hasEffectiveEmbeddingKey = computed(() => {
    return (
      isAnthropicProvider.value ||
      hasSavedEmbeddingApiKey.value ||
      (isOpenAiProvider.value && hasSavedApiKey.value && useSharedKeyForEmbedding.value)
    )
  })
  const canSave = computed(() => {
    if (isMockProvider.value) return true
    return hasSavedApiKey.value || Boolean(form.value.apiKey.trim())
  })
  const providerStatus = computed(() => {
    if (isOpenAiProvider.value) return 'OpenAI'
    if (isAnthropicProvider.value) return 'Anthropic'
    return 'Mock local'
  })

  function applyOpenAiPreset() {
    form.value.baseUrl = 'https://api.openai.com/v1'
    form.value.apiKeyHeaderName = 'Authorization'
    form.value.chatEndpointMode = 'responses'
    form.value.chatModel = 'gpt-5.5'
    form.value.fastModel = 'gpt-5.5'
    form.value.embeddingProviderName = 'openai'
    form.value.embeddingBaseUrl = 'https://api.openai.com/v1'
    form.value.embeddingApiKeyHeaderName = 'Authorization'
    form.value.embeddingModel = 'text-embedding-3-large'
    form.value.embeddingDimension = 3072
    form.value.reasoningEffort = 'medium'
    form.value.temperature = null
    form.value.maxOutputTokens = 2500
    form.value.timeoutSeconds = 60
    useSharedKeyForEmbedding.value = true
  }

  function applyMockPreset() {
    form.value.baseUrl = 'https://api.openai.com/v1'
    form.value.apiKey = ''
    form.value.clearApiKey = false
    form.value.apiKeyHeaderName = 'Authorization'
    form.value.chatEndpointMode = 'responses'
    form.value.chatModel = 'mock'
    form.value.fastModel = 'mock'
    form.value.embeddingProviderName = 'mock'
    form.value.embeddingApiKey = ''
    form.value.clearEmbeddingApiKey = false
    form.value.embeddingBaseUrl = 'https://api.openai.com/v1'
    form.value.embeddingApiKeyHeaderName = 'Authorization'
    form.value.embeddingModel = 'mock'
    form.value.embeddingDimension = 64
    form.value.reasoningEffort = 'medium'
    form.value.temperature = null
    form.value.maxOutputTokens = 2500
    form.value.timeoutSeconds = 60
    useSharedKeyForEmbedding.value = true
  }

  function applyAnthropicPreset() {
    form.value.baseUrl = 'https://api.anthropic.com/v1'
    form.value.apiKeyHeaderName = 'x-api-key'
    form.value.chatEndpointMode = 'messages'
    form.value.chatModel = 'claude-sonnet-4-5'
    form.value.fastModel = 'claude-sonnet-4-5'
    form.value.embeddingProviderName = 'mock'
    form.value.embeddingBaseUrl = 'https://api.openai.com/v1'
    form.value.embeddingApiKey = ''
    form.value.clearEmbeddingApiKey = false
    form.value.embeddingApiKeyHeaderName = 'Authorization'
    form.value.embeddingModel = 'mock'
    form.value.embeddingDimension = 64
    form.value.reasoningEffort = 'medium'
    form.value.temperature = null
    form.value.maxOutputTokens = 2500
    form.value.timeoutSeconds = 60
    useSharedKeyForEmbedding.value = false
  }

  function onProviderChange() {
    testResult.value = null
    successMessage.value = ''
    if (isOpenAiProvider.value) {
      applyOpenAiPreset()
    } else if (isAnthropicProvider.value) {
      applyAnthropicPreset()
    } else {
      applyMockPreset()
    }
  }

  function applySettings(settings: AiProviderSettings) {
    hasSavedApiKey.value = settings.hasApiKey
    hasSavedEmbeddingApiKey.value = settings.hasEmbeddingApiKey
    form.value = {
      name: settings.name === 'openai' || settings.name === 'anthropic' ? settings.name : 'mock',
      baseUrl: settings.baseUrl,
      apiKey: settings.apiKey ?? '',
      clearApiKey: false,
      apiKeyHeaderName: settings.apiKeyHeaderName,
      chatEndpointMode: settings.chatEndpointMode,
      chatModel: settings.chatModel,
      fastModel: settings.fastModel,
      embeddingProviderName: settings.embeddingProviderName,
      embeddingBaseUrl: settings.embeddingBaseUrl,
      embeddingApiKey: settings.embeddingApiKey ?? '',
      clearEmbeddingApiKey: false,
      embeddingApiKeyHeaderName: settings.embeddingApiKeyHeaderName,
      embeddingModel: settings.embeddingModel,
      embeddingDimension: settings.embeddingDimension,
      reasoningEffort: settings.reasoningEffort,
      temperature: settings.temperature ?? null,
      maxOutputTokens: settings.maxOutputTokens,
      timeoutSeconds: settings.timeoutSeconds,
    }

    if (form.value.name === 'openai' && form.value.embeddingProviderName !== 'openai') {
      form.value.embeddingProviderName = 'openai'
    }

    useSharedKeyForEmbedding.value = form.value.name === 'openai'
    updatedText.value = settings.updatedAt ? new Date(settings.updatedAt).toLocaleString('vi-VN') : '—'
  }

  async function loadSettings() {
    if (!authStore.accessToken) return
    errorMessage.value = ''
    isLoading.value = true

    try {
      applySettings(await getAiProviderSettings(authStore.accessToken))
    } catch (error) {
      errorMessage.value = toMessage(error, 'Không thể tải cấu hình AI.')
    } finally {
      isLoading.value = false
    }
  }

  function buildPayload() {
    const apiKey = form.value.apiKey.trim()
    const embeddingApiKey = form.value.embeddingApiKey.trim()
    const useSharedEmbedding = isOpenAiProvider.value && useSharedKeyForEmbedding.value

    return {
      ...form.value,
      name: isOpenAiProvider.value ? 'openai' : isAnthropicProvider.value ? 'anthropic' : 'mock',
      apiKey: isMockProvider.value || !apiKey ? null : apiKey,
      clearApiKey: isMockProvider.value ? false : form.value.clearApiKey,
      embeddingProviderName: isMockProvider.value || isAnthropicProvider.value ? 'mock' : form.value.embeddingProviderName,
      embeddingApiKey: isMockProvider.value || isAnthropicProvider.value
        ? null
        : useSharedEmbedding
          ? apiKey || null
          : embeddingApiKey || null,
      clearEmbeddingApiKey: isMockProvider.value || isAnthropicProvider.value
        ? false
        : useSharedEmbedding
          ? form.value.clearApiKey
          : form.value.clearEmbeddingApiKey,
      temperature: form.value.temperature === null || Number.isNaN(form.value.temperature) ? null : form.value.temperature,
    }
  }

  async function saveSettings(runTestAfterSave = false) {
    if (!authStore.accessToken || isSaving.value || !canSave.value) return
    errorMessage.value = ''
    successMessage.value = ''
    testResult.value = null
    isSaving.value = true

    try {
      const updated = await updateAiProviderSettings(buildPayload(), authStore.accessToken)
      applySettings(updated)
      successMessage.value = 'Đã lưu cấu hình AI.'

      if (runTestAfterSave) {
        await submitTest()
        successMessage.value = 'Đã lưu cấu hình AI.'
      }
    } catch (error) {
      errorMessage.value = toMessage(error, 'Không thể lưu cấu hình AI.')
    } finally {
      isSaving.value = false
    }
  }

  async function submitTest() {
    if (!authStore.accessToken || isTesting.value) return
    errorMessage.value = ''
    testResult.value = null
    isTesting.value = true

    try {
      testResult.value = await testAiProviderSettings(authStore.accessToken)
    } catch (error) {
      errorMessage.value = toMessage(error, 'Không thể test AI provider.')
    } finally {
      isTesting.value = false
    }
  }

  function toMessage(error: unknown, fallback: string) {
    return error instanceof ApiError || error instanceof Error ? error.message : fallback
  }

  return {
    form,
    isLoading,
    isSaving,
    isTesting,
    useSharedKeyForEmbedding,
    errorMessage,
    successMessage,
    testResult,
    hasSavedApiKey,
    hasSavedEmbeddingApiKey,
    updatedText,
    isMockProvider,
    isOpenAiProvider,
    isAnthropicProvider,
    isExternalProvider,
    hasEffectiveEmbeddingKey,
    canSave,
    providerStatus,
    onProviderChange,
    loadSettings,
    saveSettings,
    submitTest,
  }
}
