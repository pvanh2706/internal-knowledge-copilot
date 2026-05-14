<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  getKnowledgeIndexSummary,
  rebuildKnowledgeIndex,
  type KnowledgeIndexSummary,
  type RebuildKnowledgeIndexResponse,
} from '../../api/knowledgeIndex'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const summary = ref<KnowledgeIndexSummary | null>(null)
const rebuildResult = ref<RebuildKnowledgeIndexResponse | null>(null)
const isLoading = ref(false)
const isRebuilding = ref(false)
const errorMessage = ref('')
const successMessage = ref('')
const form = ref({
  resetVectorStore: false,
  batchSize: 50,
})

const sourceCountText = computed(() => {
  if (!summary.value || summary.value.ledgerSourceCounts.length === 0) return '-'
  return summary.value.ledgerSourceCounts.map((item) => `${item.sourceType}: ${item.count}`).join(', ')
})

async function loadSummary() {
  if (!authStore.accessToken) return
  errorMessage.value = ''
  isLoading.value = true

  try {
    summary.value = await getKnowledgeIndexSummary(authStore.accessToken)
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tải thông tin index.'
  } finally {
    isLoading.value = false
  }
}

async function submitRebuild() {
  if (!authStore.accessToken || isRebuilding.value) return
  errorMessage.value = ''
  successMessage.value = ''
  rebuildResult.value = null
  isRebuilding.value = true

  try {
    rebuildResult.value = await rebuildKnowledgeIndex(
      {
        resetVectorStore: form.value.resetVectorStore,
        batchSize: form.value.batchSize,
      },
      authStore.accessToken,
    )
    successMessage.value = 'Đã rebuild knowledge index.'
    await loadSummary()
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể rebuild index.'
  } finally {
    isRebuilding.value = false
  }
}

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleString() : '-'
}

onMounted(loadSummary)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Kho tri thức</h2>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <div class="stat-grid">
      <div class="stat-card">
        <span>Chunk ledger</span>
        <strong>{{ summary?.ledgerChunkCount ?? '-' }}</strong>
      </div>
      <div class="stat-card">
        <span>Chunk keyword</span>
        <strong>{{ summary?.keywordIndexChunkCount ?? '-' }}</strong>
      </div>
      <div class="stat-card">
        <span>Loại nguồn</span>
        <small>{{ sourceCountText }}</small>
      </div>
    </div>

    <form class="stack-form" @submit.prevent="submitRebuild">
      <label class="checkbox-line">
        <input v-model="form.resetVectorStore" type="checkbox" />
        Reset vector collection trước khi chạy lại
      </label>

      <label>
        Kích thước batch
        <input v-model.number="form.batchSize" type="number" min="1" max="200" />
      </label>

      <div class="button-row">
        <button type="submit" :disabled="isRebuilding || isLoading">
          {{ isRebuilding ? 'Đang rebuild...' : 'Rebuild index' }}
        </button>
        <button type="button" :disabled="isLoading" @click="loadSummary">
          Làm mới
        </button>
      </div>
    </form>

    <section v-if="rebuildResult" class="answer-panel">
      <h3>Lần rebuild gần nhất</h3>
      <p><strong>Chunk ledger:</strong> {{ rebuildResult.totalLedgerChunks }}</p>
      <p><strong>Chunk đã rebuild:</strong> {{ rebuildResult.rebuiltChunks }}</p>
      <p><strong>Số batch:</strong> {{ rebuildResult.batchCount }}</p>
      <p><strong>Reset vector:</strong> {{ rebuildResult.resetVectorStore ? 'yes' : 'no' }}</p>
      <p><strong>Bắt đầu:</strong> {{ formatDate(rebuildResult.startedAt) }}</p>
      <p><strong>Kết thúc:</strong> {{ formatDate(rebuildResult.finishedAt) }}</p>
    </section>
  </section>
</template>
