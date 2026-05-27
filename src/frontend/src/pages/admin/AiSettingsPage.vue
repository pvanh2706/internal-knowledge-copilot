<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  getAiProviderSettings,
  testAiProviderSettings,
  updateAiProviderSettings,
  type AiProviderSettings,
  type TestAiProviderSettingsResponse,
} from '../../api/aiSettings'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const isLoading = ref(false)
const isSaving = ref(false)
const isTesting = ref(false)
const showAdvanced = ref(false)
const useSharedKeyForEmbedding = ref(true)
const errorMessage = ref('')
const successMessage = ref('')
const testResult = ref<TestAiProviderSettingsResponse | null>(null)
const hasSavedApiKey = ref(false)
const hasSavedEmbeddingApiKey = ref(false)
const updatedText = ref('-')

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
  updatedText.value = settings.updatedAt ? new Date(settings.updatedAt).toLocaleString() : '-'
}

async function loadSettings() {
  if (!authStore.accessToken) return
  errorMessage.value = ''
  isLoading.value = true

  try {
    applySettings(await getAiProviderSettings(authStore.accessToken))
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tải cấu hình AI.'
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
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể lưu cấu hình AI.'
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
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể test AI provider.'
  } finally {
    isTesting.value = false
  }
}

onMounted(loadSettings)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Cấu hình AI</h2>
      <p>Chọn nhà cung cấp và nhập API key. Các thông số model, endpoint và embedding đã có preset mặc định.</p>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <div class="stat-grid">
      <div class="stat-card">
        <span>Provider</span>
        <strong>{{ providerStatus }}</strong>
      </div>
      <div class="stat-card">
        <span>API key</span>
        <strong>{{ hasSavedApiKey ? 'Đã lưu' : 'Chưa có' }}</strong>
      </div>
      <div class="stat-card">
        <span>Embedding</span>
        <strong>{{ hasEffectiveEmbeddingKey || isMockProvider ? 'Sẵn sàng' : 'Chưa có key' }}</strong>
      </div>
      <div class="stat-card">
        <span>Cập nhật</span>
        <small>{{ updatedText }}</small>
      </div>
    </div>

    <form class="stack-form" @submit.prevent="saveSettings(false)">
      <section class="settings-section">
        <label>
          Nhà cung cấp
          <select v-model="form.name" @change="onProviderChange">
            <option value="mock">Mock local</option>
            <option value="openai">OpenAI</option>
            <option value="anthropic">Anthropic / Claude</option>
          </select>
        </label>

        <label v-if="isExternalProvider">
          {{ isAnthropicProvider ? 'Anthropic API key' : 'OpenAI API key' }}
          <input
            v-model="form.apiKey"
            type="text"
            autocomplete="off"
            :required="!hasSavedApiKey"
            :placeholder="hasSavedApiKey ? 'API key đang lưu' : isAnthropicProvider ? 'Dán API key của Anthropic' : 'Dán API key của OpenAI'"
          />
        </label>

        <label v-if="isExternalProvider && hasSavedApiKey" class="checkbox-line">
          <input v-model="form.clearApiKey" type="checkbox" />
          Xóa API key đang lưu
        </label>

        <p v-if="isMockProvider" class="settings-note">
          Mock local không gọi LLM bên ngoài. Chế độ này chỉ phù hợp để demo luồng, câu trả lời sẽ là bản ghép từ các đoạn tìm kiếm.
        </p>
        <p v-else class="settings-note">
          Preset {{ isAnthropicProvider ? 'Anthropic' : 'OpenAI' }} dùng endpoint {{ form.chatEndpointMode }}, model {{ form.chatModel }}.
          {{ isAnthropicProvider ? 'Embedding đang dùng mock để không cần thêm key khác.' : 'Embedding sẽ dùng chung API key ở trên.' }}
        </p>
      </section>

      <label class="checkbox-line">
        <input v-model="showAdvanced" type="checkbox" />
        Hiển thị cấu hình nâng cao
      </label>

      <section v-if="showAdvanced" class="settings-section">
        <h3>Nâng cao</h3>

        <div class="form-grid">
          <label>
            Base URL
            <input v-model.trim="form.baseUrl" type="url" />
          </label>

          <label>
            Chat endpoint
            <select v-model="form.chatEndpointMode" :disabled="isMockProvider">
              <option value="responses">Responses API</option>
              <option value="chat-completions">Chat Completions</option>
              <option value="messages">Anthropic Messages</option>
            </select>
          </label>

          <label>
            Chat model
            <input v-model.trim="form.chatModel" type="text" :disabled="isMockProvider" />
          </label>

          <label>
            Fast model
            <input v-model.trim="form.fastModel" type="text" :disabled="isMockProvider" />
          </label>

          <label>
            Reasoning effort
            <select v-model="form.reasoningEffort" :disabled="isMockProvider">
              <option value="low">low</option>
              <option value="medium">medium</option>
              <option value="high">high</option>
              <option value="xhigh">xhigh</option>
            </select>
          </label>

          <label>
            Timeout seconds
            <input v-model.number="form.timeoutSeconds" type="number" min="1" max="600" />
          </label>
        </div>

        <label v-if="isOpenAiProvider" class="checkbox-line">
          <input v-model="useSharedKeyForEmbedding" type="checkbox" />
          Dùng chung OpenAI API key cho embedding
        </label>

        <div class="form-grid">
          <label>
            Embedding provider
            <select v-model="form.embeddingProviderName" :disabled="isMockProvider || isAnthropicProvider">
              <option value="openai">OpenAI</option>
              <option value="mock">Mock</option>
            </select>
          </label>

          <label>
            Embedding model
            <input v-model.trim="form.embeddingModel" type="text" :disabled="isMockProvider || form.embeddingProviderName === 'mock'" />
          </label>

          <label>
            Embedding Base URL
            <input v-model.trim="form.embeddingBaseUrl" type="url" :disabled="isMockProvider || form.embeddingProviderName === 'mock'" />
          </label>

          <label>
            Embedding dimension
            <input
              v-model.number="form.embeddingDimension"
              type="number"
              min="1"
              max="20000"
              :disabled="isMockProvider || form.embeddingProviderName === 'mock'"
            />
          </label>
        </div>

        <label v-if="isOpenAiProvider && !useSharedKeyForEmbedding">
          Embedding API key
          <input
            v-model="form.embeddingApiKey"
            type="password"
            autocomplete="off"
            :placeholder="hasSavedEmbeddingApiKey ? 'Để trống nếu vẫn dùng key đã lưu' : 'Dán embedding API key'"
          />
        </label>
      </section>

      <div class="button-row">
        <button type="submit" :disabled="isSaving || isLoading || !canSave">
          {{ isSaving ? 'Đang lưu...' : 'Lưu cấu hình' }}
        </button>
        <button type="button" :disabled="isSaving || isTesting || isLoading || !canSave" @click="saveSettings(true)">
          {{ isSaving || isTesting ? 'Đang test...' : 'Lưu và test' }}
        </button>
        <button type="button" :disabled="isTesting || isLoading" @click="submitTest">
          {{ isTesting ? 'Đang test...' : 'Test cấu hình đã lưu' }}
        </button>
        <button type="button" :disabled="isLoading" @click="loadSettings">Làm mới</button>
      </div>
    </form>

    <section v-if="testResult" class="answer-panel">
      <h3>Kết quả test</h3>
      <p><strong>Trạng thái:</strong> {{ testResult.success ? 'OK' : 'Thất bại' }}</p>
      <p><strong>Provider:</strong> {{ testResult.providerName }}</p>
      <p>{{ testResult.message }}</p>
    </section>

    <section class="answer-panel">
      <h3>Lưu ý</h3>
      <p>
        Nếu các tài liệu cũ được index bằng mock embedding, sau khi chuyển sang OpenAI embedding nên chạy Knowledge Index rebuild để truy xuất ổn định hơn.
      </p>
    </section>
  </section>
</template>
