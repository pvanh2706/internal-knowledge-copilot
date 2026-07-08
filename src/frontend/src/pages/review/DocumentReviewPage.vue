<script setup lang="ts">
import { h, onMounted, ref } from 'vue'
import type { ColumnDef } from '@tanstack/vue-table'
import { CheckIcon, ClipboardCheckIcon, XIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import DataTable from '@/components/app/DataTable.vue'
import EmptyState from '@/components/app/EmptyState.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import type { DocumentDetail, DocumentListItem } from '@/api/documents'
import { approveDocument, getDocument, getDocuments, rejectDocument } from '@/api/documents'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useToast } from '@/composables/useToast'

const authStore = useAuthStore()
const toast = useToast()
const { confirm } = useConfirm()

const documents = ref<DocumentListItem[]>([])
const selectedDocument = ref<DocumentDetail | null>(null)
const isLoading = ref(false)
const isSaving = ref(false)
const loadError = ref('')
// Lý do reject nhập riêng cho từng version, không dùng chung 1 ô
const rejectReasons = ref<Record<string, string>>({})

const columns: ColumnDef<DocumentListItem>[] = [
  { accessorKey: 'title', header: 'Tên' },
  { accessorKey: 'folderPath', header: 'Thư mục' },
  {
    accessorKey: 'pendingVersionCount',
    header: 'Chờ duyệt',
    cell: ({ row }) => h('span', { class: 'tabular-nums' }, String(row.original.pendingVersionCount)),
  },
]

async function loadQueue() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    documents.value = await getDocuments(authStore.accessToken, { status: 'PendingReview' })
  } catch (error) {
    loadError.value = toMessage(error, 'Không thể tải hàng đợi duyệt.')
  } finally {
    isLoading.value = false
  }
}

async function selectDocument(document: DocumentListItem) {
  if (!authStore.accessToken) return
  try {
    selectedDocument.value = await getDocument(document.id, authStore.accessToken)
    rejectReasons.value = {}
  } catch (error) {
    toast.error(toMessage(error, 'Không thể tải chi tiết tài liệu.'))
  }
}

async function approve(versionId: string) {
  if (!authStore.accessToken || !selectedDocument.value || isSaving.value) return
  isSaving.value = true

  try {
    await approveDocument(selectedDocument.value.id, versionId, authStore.accessToken)
    toast.success('Đã duyệt phiên bản.')
    await loadQueue()
    selectedDocument.value = await getDocument(selectedDocument.value.id, authStore.accessToken)
  } catch (error) {
    toast.error(toMessage(error, 'Không thể duyệt phiên bản.'))
  } finally {
    isSaving.value = false
  }
}

async function reject(versionId: string, versionNumber: number) {
  if (!authStore.accessToken || !selectedDocument.value || isSaving.value) return

  const confirmed = await confirm({
    title: `Từ chối phiên bản ${versionNumber}?`,
    description: 'Người tải lên sẽ thấy lý do từ chối và phải tải phiên bản mới nếu muốn tiếp tục.',
    confirmText: 'Từ chối',
    destructive: true,
  })
  if (!confirmed) return

  isSaving.value = true
  try {
    await rejectDocument(
      selectedDocument.value.id,
      versionId,
      rejectReasons.value[versionId] ?? '',
      authStore.accessToken,
    )
    toast.success('Đã từ chối phiên bản.')
    await loadQueue()
    selectedDocument.value = await getDocument(selectedDocument.value.id, authStore.accessToken)
  } catch (error) {
    toast.error(toMessage(error, 'Không thể từ chối phiên bản.'))
  } finally {
    isSaving.value = false
  }
}

function toMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback
}

onMounted(loadQueue)
</script>

<template>
  <div>
    <PageHeader title="Duyệt tài liệu" description="Duyệt hoặc từ chối các phiên bản đang chờ review." />

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadQueue" />

    <div class="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.9fr)]">
      <DataTable
        :columns="columns"
        :data="documents"
        :is-loading="isLoading"
        clickable-rows
        empty-title="Không có tài liệu chờ duyệt"
        empty-description="Hàng đợi trống — mọi phiên bản đã được xử lý."
        @row-click="selectDocument"
      />

      <EmptyState
        v-if="!selectedDocument"
        :icon="ClipboardCheckIcon"
        title="Chọn một tài liệu để duyệt"
        description="Bấm vào dòng trong bảng để xem các phiên bản chờ duyệt."
      />

      <Card v-else>
        <CardHeader>
          <CardTitle class="text-lg">{{ selectedDocument.title }}</CardTitle>
        </CardHeader>
        <CardContent class="grid gap-4">
          <div
            v-for="version in selectedDocument.versions.filter((item) => item.status === 'PendingReview')"
            :key="version.id"
            class="grid gap-3 rounded-lg border p-4"
          >
            <div class="flex flex-wrap items-center justify-between gap-2">
              <p class="text-sm font-medium">Phiên bản {{ version.versionNumber }}</p>
              <StatusBadge :status="version.status" />
            </div>
            <p class="text-sm text-muted-foreground">{{ version.originalFileName }}</p>

            <div class="grid gap-1.5">
              <Label :for="`reject-reason-${version.id}`">Lý do từ chối (nếu từ chối)</Label>
              <Textarea
                :id="`reject-reason-${version.id}`"
                v-model="rejectReasons[version.id]"
                placeholder="Ví dụ: nội dung trùng lặp, sai thư mục..."
                rows="3"
              />
            </div>

            <div class="flex flex-wrap gap-2">
              <Button size="sm" type="button" :disabled="isSaving" @click="approve(version.id)">
                <CheckIcon class="size-4" />
                Duyệt
              </Button>
              <Button
                size="sm"
                variant="destructive"
                type="button"
                :disabled="isSaving"
                @click="reject(version.id, version.versionNumber)"
              >
                <XIcon class="size-4" />
                Từ chối
              </Button>
            </div>
          </div>

          <EmptyState
            v-if="!selectedDocument.versions.some((item) => item.status === 'PendingReview')"
            title="Tài liệu này không còn phiên bản chờ duyệt"
          />
        </CardContent>
      </Card>
    </div>
  </div>
</template>
