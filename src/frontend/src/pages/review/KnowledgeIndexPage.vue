<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { DatabaseIcon, RefreshCwIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import ErrorState from '@/components/app/ErrorState.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatCard from '@/components/app/StatCard.vue'
import {
  getKnowledgeIndexSummary,
  rebuildKnowledgeIndex,
  type KnowledgeIndexSummary,
  type RebuildKnowledgeIndexResponse,
} from '@/api/knowledgeIndex'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useToast } from '@/composables/useToast'
import { formatDateTime } from '@/lib/format'

const authStore = useAuthStore()
const toast = useToast()
const { confirm } = useConfirm()

const summary = ref<KnowledgeIndexSummary | null>(null)
const rebuildResult = ref<RebuildKnowledgeIndexResponse | null>(null)
const isLoading = ref(false)
const isRebuilding = ref(false)
const loadError = ref('')

const form = ref({
  resetVectorStore: false,
  batchSize: 50,
})

const sourceCountText = computed(() => {
  if (!summary.value || summary.value.ledgerSourceCounts.length === 0) return '—'
  return summary.value.ledgerSourceCounts.map((item) => `${item.sourceType}: ${item.count}`).join(', ')
})

async function loadSummary() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    summary.value = await getKnowledgeIndexSummary(authStore.accessToken)
  } catch (error) {
    loadError.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tải thông tin chỉ mục.'
  } finally {
    isLoading.value = false
  }
}

async function submitRebuild() {
  if (!authStore.accessToken || isRebuilding.value) return

  if (form.value.resetVectorStore) {
    const confirmed = await confirm({
      title: 'Reset vector store trước khi chạy lại?',
      description:
        'Toàn bộ collection vector sẽ bị xóa và lập chỉ mục lại từ đầu. Q&A có thể thiếu kết quả trong lúc rebuild.',
      confirmText: 'Reset và rebuild',
      destructive: true,
    })
    if (!confirmed) return
  }

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
    toast.success('Đã rebuild chỉ mục tri thức.')
    await loadSummary()
  } catch (error) {
    toast.error(error instanceof ApiError || error instanceof Error ? error.message : 'Không thể rebuild chỉ mục.')
  } finally {
    isRebuilding.value = false
  }
}

onMounted(loadSummary)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader
      title="Chỉ mục tri thức"
      description="Theo dõi số chunk đã lập chỉ mục và chạy lại pipeline khi cần."
    >
      <template #actions>
        <Button variant="outline" :disabled="isLoading" @click="loadSummary">
          <RefreshCwIcon class="size-4" />
          Làm mới
        </Button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadSummary" />

    <div v-if="isLoading && !summary" class="grid grid-cols-1 gap-4 md:grid-cols-3">
      <Skeleton v-for="n in 3" :key="n" class="h-[104px] rounded-lg" />
    </div>
    <div v-else class="grid grid-cols-1 gap-4 md:grid-cols-3">
      <StatCard label="Chunk trong ledger" :value="summary?.ledgerChunkCount ?? '—'" />
      <StatCard label="Chunk trong keyword index" :value="summary?.keywordIndexChunkCount ?? '—'" />
      <StatCard label="Theo loại nguồn" value="" :hint="sourceCountText" />
    </div>

    <Card class="mt-6">
      <CardHeader>
        <CardTitle class="text-lg">
          <span class="inline-flex items-center gap-2">
            <DatabaseIcon class="size-4 text-muted-foreground" aria-hidden="true" />
            Rebuild chỉ mục
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <form class="grid max-w-md gap-4" @submit.prevent="submitRebuild">
          <label class="flex cursor-pointer items-start gap-2.5 rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm">
            <Checkbox v-model="form.resetVectorStore" class="mt-0.5" />
            <span>
              Reset vector collection trước khi chạy lại
              <span class="block text-xs text-muted-foreground">Xóa toàn bộ vector và lập chỉ mục lại từ đầu — cần xác nhận.</span>
            </span>
          </label>

          <div class="grid gap-1.5">
            <Label for="rebuild-batch-size">Kích thước batch</Label>
            <Input id="rebuild-batch-size" v-model.number="form.batchSize" type="number" min="1" max="200" class="w-32" />
          </div>

          <Button type="submit" class="justify-self-start" :disabled="isRebuilding || isLoading">
            {{ isRebuilding ? 'Đang rebuild...' : 'Rebuild chỉ mục' }}
          </Button>
        </form>
      </CardContent>
    </Card>

    <Card v-if="rebuildResult" class="mt-6">
      <CardHeader>
        <CardTitle class="text-lg">Lần rebuild gần nhất</CardTitle>
      </CardHeader>
      <CardContent>
        <dl class="grid gap-1.5 text-sm">
          <div class="flex gap-2">
            <dt class="w-40 shrink-0 text-muted-foreground">Chunk trong ledger</dt>
            <dd class="tabular-nums">{{ rebuildResult.totalLedgerChunks }}</dd>
          </div>
          <div class="flex gap-2">
            <dt class="w-40 shrink-0 text-muted-foreground">Chunk đã rebuild</dt>
            <dd class="tabular-nums">{{ rebuildResult.rebuiltChunks }}</dd>
          </div>
          <div class="flex gap-2">
            <dt class="w-40 shrink-0 text-muted-foreground">Số batch</dt>
            <dd class="tabular-nums">{{ rebuildResult.batchCount }}</dd>
          </div>
          <div class="flex gap-2">
            <dt class="w-40 shrink-0 text-muted-foreground">Reset vector</dt>
            <dd>{{ rebuildResult.resetVectorStore ? 'Có' : 'Không' }}</dd>
          </div>
          <div class="flex gap-2">
            <dt class="w-40 shrink-0 text-muted-foreground">Bắt đầu</dt>
            <dd>{{ formatDateTime(rebuildResult.startedAt) }}</dd>
          </div>
          <div class="flex gap-2">
            <dt class="w-40 shrink-0 text-muted-foreground">Kết thúc</dt>
            <dd>{{ formatDateTime(rebuildResult.finishedAt) }}</dd>
          </div>
        </dl>
      </CardContent>
    </Card>
  </div>
</template>
