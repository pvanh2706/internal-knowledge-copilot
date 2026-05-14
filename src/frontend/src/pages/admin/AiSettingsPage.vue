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
const errorMessage = ref('')
const successMessage = ref('')
const testResult = ref<TestAiProviderSettingsResponse | null>(null)
const hasSavedApiKey = ref(false)
const hasSavedEmbeddingApiKey = ref(false)

const form = ref({
  name: 'mock',
  baseUrl: 'https://api.openai.com/v1',
  apiKey: '',
  clearApiKey: false,
  apiKeyHeaderName: 'Authorization',
  chatEndpointMode: 'responses',
  chatModel: 'gpt-5.5',
  fastModel: 'gpt-5.5',
  embeddingProviderName: 'openai-compatible',
  embeddingBaseUrl: 'https://api.openai.com/v1',
  embeddingApiKey: '',
  clearEmbeddingApiKey: false,
  embeddingApiKeyHeaderName: 'Authorization',
  embeddingModel: 'text-embedding-3-large',
  embeddingDimension: 3072,
  reasoningEffort: 'medium',
  temperature: 0.2 as number | null,
  maxOutputTokens: 2500,
  timeoutSeconds: 60,
})

const isMockProvider = computed(() => form.value.name === 'mock')
const isClaudeProvider = computed(() => form.value.name === 'anthropic' || form.value.name === 'claude')
const isMockEmbeddingProvider = computed(() => form.value.embeddingProviderName === 'mock')
const updatedText = ref('-')

function applySettings(settings: AiProviderSettings) {
  hasSavedApiKey.value = settings.hasApiKey
  hasSavedEmbeddingApiKey.value = settings.hasEmbeddingApiKey
  form.value = {
    name: settings.name,
    baseUrl: settings.baseUrl,
    apiKey: '',
    clearApiKey: false,
    apiKeyHeaderName: settings.apiKeyHeaderName,
    chatEndpointMode: settings.chatEndpointMode,
    chatModel: settings.chatModel,
    fastModel: settings.fastModel,
    embeddingProviderName: settings.embeddingProviderName,
    embeddingBaseUrl: settings.embeddingBaseUrl,
    embeddingApiKey: '',
    clearEmbeddingApiKey: false,
    embeddingApiKeyHeaderName: settings.embeddingApiKeyHeaderName,
    embeddingModel: settings.embeddingModel,
    embeddingDimension: settings.embeddingDimension,
    reasoningEffort: settings.reasoningEffort,
    temperature: settings.temperature ?? null,
    maxOutputTokens: settings.maxOutputTokens,
    timeoutSeconds: settings.timeoutSeconds,
  }
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

async function submitSettings() {
  if (!authStore.accessToken || isSaving.value) return
  errorMessage.value = ''
  successMessage.value = ''
  testResult.value = null
  isSaving.value = true

  try {
    const updated = await updateAiProviderSettings(
      {
        ...form.value,
        apiKey: form.value.apiKey.trim() || null,
        embeddingApiKey: form.value.embeddingApiKey.trim() || null,
        temperature: form.value.temperature === null || Number.isNaN(form.value.temperature) ? null : form.value.temperature,
      },
      authStore.accessToken,
    )
    applySettings(updated)
    successMessage.value = 'Đã lưu cấu hình AI provider.'
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể lưu cấu hình AI.'
  } finally {
    isSaving.value = false
  }
}

async function submitTest() {
  if (!authStore.accessToken || isTesting.value) return
  errorMessage.value = ''
  successMessage.value = ''
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
      <p>Cấu hình LLM và embedding provider cho ingestion, Q&A, wiki và evaluation.</p>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <div class="stat-grid">
      <div class="stat-card">
        <span>Provider</span>
        <strong>{{ form.name }}</strong>
      </div>
      <div class="stat-card">
        <span>API key</span>
        <strong>{{ hasSavedApiKey ? 'Đã lưu' : 'Chưa có' }}</strong>
      </div>
      <div class="stat-card">
        <span>Embedding key</span>
        <strong>{{ hasSavedEmbeddingApiKey ? 'Đã lưu' : 'Chưa có' }}</strong>
      </div>
      <div class="stat-card">
        <span>Cập nhật</span>
        <small>{{ updatedText }}</small>
      </div>
    </div>

    <form class="stack-form" @submit.prevent="submitSettings">
      <label>
        Provider
        <select v-model="form.name">
          <option value="mock">mock</option>
          <option value="openai">openai</option>
          <option value="openai-compatible">openai-compatible</option>
          <option value="azure-openai">azure-openai</option>
          <option value="anthropic">anthropic / Claude</option>
          <option value="claude">claude</option>
          <option value="local">local</option>
        </select>
      </label>

      <label>
        Base URL
        <input v-model.trim="form.baseUrl" type="url" placeholder="https://api.openai.com/v1" />
      </label>

      <label>
        API key
        <input
          v-model="form.apiKey"
          type="password"
          autocomplete="off"
          :placeholder="hasSavedApiKey ? 'Nhập key mới nếu muốn thay đổi' : 'Nhập API key'"
          :disabled="isMockProvider"
        />
      </label>

      <label class="checkbox-line">
        <input v-model="form.clearApiKey" type="checkbox" :disabled="isMockProvider" />
        Xóa API key đang lưu
      </label>

      <label>
        API key header
        <select v-model="form.apiKeyHeaderName" :disabled="isMockProvider || isClaudeProvider">
          <option value="Authorization">Authorization Bearer</option>
          <option value="api-key">api-key</option>
          <option value="x-api-key">x-api-key</option>
        </select>
      </label>

      <label>
        Chat endpoint
        <select v-model="form.chatEndpointMode" :disabled="isMockProvider || isClaudeProvider">
          <option value="responses">responses</option>
          <option value="chat-completions">chat-completions</option>
          <option value="messages">messages</option>
        </select>
      </label>

      <div class="form-grid">
        <label>
          Chat model
          <input v-model.trim="form.chatModel" type="text" :disabled="isMockProvider" />
        </label>

        <label>
          Fast model
          <input v-model.trim="form.fastModel" type="text" :disabled="isMockProvider" />
        </label>
      </div>

      <section class="answer-panel">
        <h3>Embedding provider</h3>

        <label>
          Embedding provider
          <select v-model="form.embeddingProviderName">
            <option value="mock">mock</option>
            <option value="openai">openai</option>
            <option value="openai-compatible">openai-compatible</option>
            <option value="azure-openai">azure-openai</option>
            <option value="local">local</option>
          </select>
        </label>

        <label>
          Embedding Base URL
          <input v-model.trim="form.embeddingBaseUrl" type="url" placeholder="https://api.openai.com/v1" />
        </label>

        <label>
          Embedding API key
          <input
            v-model="form.embeddingApiKey"
            type="password"
            autocomplete="off"
            :placeholder="hasSavedEmbeddingApiKey ? 'Nhập key mới nếu muốn thay đổi' : 'Nhập embedding API key'"
            :disabled="isMockEmbeddingProvider"
          />
        </label>

        <label class="checkbox-line">
          <input v-model="form.clearEmbeddingApiKey" type="checkbox" :disabled="isMockEmbeddingProvider" />
          Xóa embedding API key đang lưu
        </label>

        <label>
          Embedding API key header
          <select v-model="form.embeddingApiKeyHeaderName" :disabled="isMockEmbeddingProvider">
            <option value="Authorization">Authorization Bearer</option>
            <option value="api-key">api-key</option>
          </select>
        </label>

        <label>
          Embedding model
          <input v-model.trim="form.embeddingModel" type="text" :disabled="isMockEmbeddingProvider" />
        </label>

        <label>
          Embedding dimension
          <input v-model.number="form.embeddingDimension" type="number" min="1" max="20000" :disabled="isMockEmbeddingProvider" />
        </label>
      </section>

      <div class="form-grid">

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
          Temperature
          <input v-model.number="form.temperature" type="number" min="0" max="2" step="0.1" :disabled="isMockProvider" />
        </label>

        <label>
          Max output tokens
          <input v-model.number="form.maxOutputTokens" type="number" min="1" max="100000" :disabled="isMockProvider" />
        </label>

        <label>
          Timeout seconds
          <input v-model.number="form.timeoutSeconds" type="number" min="1" max="600" />
        </label>
      </div>

      <div class="button-row">
        <button type="submit" :disabled="isSaving || isLoading">
          {{ isSaving ? 'Đang lưu...' : 'Lưu cấu hình' }}
        </button>
        <button type="button" :disabled="isTesting || isLoading" @click="submitTest">
          {{ isTesting ? 'Đang test...' : 'Test kết nối' }}
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
      <h3>Lưu ý khi đổi embedding</h3>
      <p>
        Khi đổi từ mock sang embedding thật hoặc đổi embedding dimension, nên dùng Chroma collection mới và chạy lại
        Knowledge Index rebuild để tránh trộn vector khác dimension.
      </p>
      <p>
        Có thể dùng Claude cho chat và OpenAI-compatible cho embedding. Nếu embedding provider là mock, retrieval vẫn
        chạy được nhưng không phải semantic embedding thật.
      </p>
    </section>
  </section>
</template>
