<script setup lang="ts">
import { computed, h, onMounted, ref } from 'vue'
import type { ColumnDef } from '@tanstack/vue-table'
import { SearchIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import CitationCard from '@/components/app/CitationCard.vue'
import DataTable from '@/components/app/DataTable.vue'
import EmptyState from '@/components/app/EmptyState.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import ScopeSelector from '@/components/app/ScopeSelector.vue'
import StatCard from '@/components/app/StatCard.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import { explainRetrieval, type AiScopeType, type RetrievalCandidate, type RetrievalExplainResponse } from '@/api/ai'
import type { DocumentListItem } from '@/api/documents'
import { getDocuments } from '@/api/documents'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import { useFolderTree } from '@/composables/useFolderTree'

const authStore = useAuthStore()
const { flatFolders, loadFolders } = useFolderTree()

const documents = ref<DocumentListItem[]>([])
const explain = ref<RetrievalExplainResponse | null>(null)
const isLoading = ref(false)
const errorMessage = ref('')

const form = ref({
  question: '',
  scopeType: 'All' as AiScopeType,
  folderId: '',
  documentId: '',
})

const indexedDocuments = computed(() =>
  documents.value.filter((document) => document.latestVersionStatus === 'Indexed'),
)

const canSubmit = computed(() => {
  if (!form.value.question.trim()) return false
  if (form.value.scopeType === 'Folder') return Boolean(form.value.folderId)
  if (form.value.scopeType === 'Document') return Boolean(form.value.documentId)
  return true
})

const rejectedCandidates = computed(
  () => explain.value?.candidates.filter((candidate) => !candidate.passedPermissionFilter) ?? [],
)

const candidateColumns: ColumnDef<RetrievalCandidate>[] = [
  {
    accessorKey: 'title',
    header: 'Nguồn',
    cell: ({ row }) =>
      h('div', { class: 'grid gap-0.5' }, [
        h('span', { class: 'text-sm font-medium' }, `${row.original.sourceType} — ${row.original.title}`),
        h(
          'span',
          { class: 'font-mono text-xs text-muted-foreground' },
          `${row.original.retrievalSource} / ${[row.original.folderPath, row.original.sectionTitle].filter(Boolean).join(' / ') || '—'}`,
        ),
      ]),
  },
  {
    accessorKey: 'score',
    header: 'Điểm',
    cell: ({ row }) => h('span', { class: 'font-mono text-xs tabular-nums' }, row.original.score.toFixed(2)),
  },
  {
    accessorKey: 'matchedKeywords',
    header: 'Từ khóa khớp',
    enableSorting: false,
    cell: ({ row }) => row.original.matchedKeywords.join(', ') || '—',
  },
  {
    accessorKey: 'decision',
    header: 'Quyết định',
    enableSorting: false,
    cell: ({ row }) =>
      h(StatusBadge, {
        status: row.original.selectedForContext ? 'Active' : 'Disabled',
        label: row.original.decision,
      }),
  },
]

async function loadScopeData() {
  if (!authStore.accessToken) return
  try {
    const [documentList] = await Promise.all([
      getDocuments(authStore.accessToken, { status: 'Approved' }),
      loadFolders(authStore.accessToken),
    ])
    documents.value = documentList
  } catch {
    // Scope mặc định "Tất cả" vẫn dùng được nếu tải dữ liệu scope thất bại
  }
}

async function submitExplain() {
  if (!authStore.accessToken || !canSubmit.value || isLoading.value) return
  errorMessage.value = ''
  explain.value = null
  isLoading.value = true

  try {
    explain.value = await explainRetrieval(
      {
        question: form.value.question,
        scopeType: form.value.scopeType,
        folderId: form.value.scopeType === 'Folder' ? form.value.folderId : null,
        documentId: form.value.scopeType === 'Document' ? form.value.documentId : null,
      },
      authStore.accessToken,
    )
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể phân tích truy xuất.'
  } finally {
    isLoading.value = false
  }
}

function candidateMeta(candidate: RetrievalCandidate) {
  const distance = typeof candidate.distance === 'number' ? candidate.distance.toFixed(3) : '—'
  return `${candidate.retrievalSource} · score ${candidate.score.toFixed(2)} · distance ${distance}`
}

onMounted(loadScopeData)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader
      title="Giải thích truy xuất"
      description="Kiểm tra pipeline truy xuất: truy vấn được hiểu thế nào, ứng viên nào được chọn vào context."
    />

    <form
      class="mb-6 grid gap-3 rounded-xl border bg-card p-4 text-card-foreground focus-within:ring-2 focus-within:ring-ring/40"
      @submit.prevent="submitExplain"
    >
      <Textarea
        v-model="form.question"
        rows="3"
        required
        placeholder="Nhập câu hỏi cần kiểm tra..."
        aria-label="Câu hỏi cần phân tích"
        class="border-0 shadow-none focus-visible:ring-0"
      />
      <div class="flex items-center justify-between gap-3">
        <ScopeSelector
          v-model:scope-type="form.scopeType"
          v-model:folder-id="form.folderId"
          v-model:document-id="form.documentId"
          :folders="flatFolders"
          :documents="indexedDocuments"
        />
        <Button type="submit" :disabled="isLoading || !canSubmit">
          <SearchIcon class="size-4" />
          {{ isLoading ? 'Đang phân tích...' : 'Phân tích' }}
        </Button>
      </div>
    </form>

    <ErrorState v-if="errorMessage" :message="errorMessage" class="mb-4" @retry="submitExplain" />

    <div v-if="isLoading" class="grid gap-4">
      <div class="grid grid-cols-2 gap-4 md:grid-cols-3 xl:grid-cols-6">
        <Skeleton v-for="n in 6" :key="n" class="h-24 rounded-lg" />
      </div>
      <Skeleton class="h-48 rounded-lg" />
    </div>

    <div v-else-if="explain" class="grid gap-6">
      <Card>
        <CardHeader>
          <CardTitle class="text-lg">Truy vấn</CardTitle>
        </CardHeader>
        <CardContent class="grid gap-2 text-sm">
          <p>
            <span class="font-medium">Chuẩn hóa:</span>
            {{ explain.queryUnderstanding.normalizedQuestion || '—' }}
          </p>
          <p>
            <span class="font-medium">Từ khóa:</span>
            {{ explain.queryUnderstanding.keywords.join(', ') || '—' }}
          </p>
          <p class="text-muted-foreground">
            <span class="font-medium text-foreground">Bộ lọc:</span>
            {{ explain.filter.sourceTypes.join(', ') }} / {{ explain.filter.statuses.join(', ') }} ·
            thư mục được xem {{ explain.filter.visibleFolderCount }} ·
            thư mục sau lọc {{ explain.filter.filteredFolderCount }}
          </p>
        </CardContent>
      </Card>

      <div class="grid grid-cols-2 gap-4 md:grid-cols-3 xl:grid-cols-6">
        <StatCard label="Vector" :value="explain.candidateStats.vectorCandidateCount" />
        <StatCard label="Từ khóa" :value="explain.candidateStats.keywordCandidateCount" />
        <StatCard label="Đã gộp" :value="explain.candidateStats.mergedCandidateCount" />
        <StatCard label="Hợp lệ" :value="explain.candidateStats.allowedCandidateCount" />
        <StatCard label="Context cuối" :value="explain.candidateStats.finalContextCount" />
        <StatCard label="Bị loại" :value="rejectedCandidates.length" />
      </div>

      <div>
        <p class="mb-2 text-sm font-medium">Context cuối</p>
        <EmptyState v-if="explain.finalContext.length === 0" title="Không có context phù hợp" />
        <div v-else class="grid gap-3 sm:grid-cols-2">
          <CitationCard
            v-for="(candidate, index) in explain.finalContext"
            :key="candidate.candidateId"
            :index="index + 1"
            :source-type="candidate.sourceType"
            :title="candidate.title"
            :folder-path="candidate.folderPath"
            :section-title="candidate.sectionTitle"
            :excerpt="candidate.excerpt"
            :meta="candidateMeta(candidate)"
          />
        </div>
      </div>

      <div>
        <p class="mb-2 text-sm font-medium">Toàn bộ ứng viên</p>
        <DataTable
          :columns="candidateColumns"
          :data="explain.candidates"
          empty-title="Không có ứng viên nào"
        />
      </div>
    </div>

    <EmptyState
      v-else-if="!errorMessage"
      :icon="SearchIcon"
      title="Nhập câu hỏi để phân tích pipeline truy xuất"
      description="Công cụ dành cho reviewer kiểm tra vì sao AI chọn (hoặc bỏ) một nguồn."
    />
  </div>
</template>
