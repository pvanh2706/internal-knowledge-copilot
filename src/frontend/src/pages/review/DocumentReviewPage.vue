<script setup lang="ts">
import { computed, h, nextTick, onMounted, ref, watch } from 'vue'
import type { ColumnDef } from '@tanstack/vue-table'
import { useMediaQuery } from '@vueuse/core'
import { CheckIcon, ClipboardCheckIcon, DownloadIcon, RefreshCwIcon, XIcon } from '@lucide/vue'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import DataTable from '@/components/app/DataTable.vue'
import DetailPanel from '@/components/app/DetailPanel.vue'
import EmptyState from '@/components/app/EmptyState.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import FileTypeIcon from '@/components/app/FileTypeIcon.vue'
import InlineNotice from '@/components/app/InlineNotice.vue'
import MetaGrid from '@/components/app/MetaGrid.vue'
import MetaItem from '@/components/app/MetaItem.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import PanelCard from '@/components/app/PanelCard.vue'
import SearchInput from '@/components/app/SearchInput.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import { statusLabel } from '@/components/app/status'
import type { DocumentDetail, DocumentListItem, DocumentStatus, DocumentVersion } from '@/api/documents'
import { approveDocument, downloadDocument, getDocument, getDocuments, rejectDocument } from '@/api/documents'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useToast } from '@/composables/useToast'
import { errorMessage } from '@/lib/errors'
import { formatDate, formatDateTime, formatFileSize } from '@/lib/format'

// UX-04 Duyệt tài liệu (B1): hàng đợi bên trái, không gian reviewer bên phải.
// Mọi quyết định đi qua approveDocument/rejectDocument — quyền reviewer luôn do máy chủ kiểm tra.
const authStore = useAuthStore()
const toast = useToast()
const { confirm } = useConfirm()
const isStacked = useMediaQuery('(max-width: 1100px)')

const documents = ref<DocumentListItem[]>([])
const selectedId = ref<string | null>(null)
const selectedDocument = ref<DocumentDetail | null>(null)
const isLoading = ref(false)
const isDetailLoading = ref(false)
const isSaving = ref(false)
const loadError = ref('')
const detailPanelRef = ref<HTMLElement | null>(null)

// Mặc định là hàng đợi chờ duyệt như trước; các trạng thái khác chỉ để tra cứu (không có thao tác)
const filters = ref({ status: 'PendingReview' as 'all' | DocumentStatus, keyword: '' })

const rejectTarget = ref<DocumentVersion | null>(null)
const rejectReason = ref('')
const rejectAttempted = ref(false)
const rejectError = ref('')
const isRejectDialogOpen = computed({
  get: () => rejectTarget.value !== null,
  set: (open) => {
    if (!open && !isSaving.value) rejectTarget.value = null
  },
})

const isPendingQueue = computed(() => filters.value.status === 'PendingReview')
const pendingVersionTotal = computed(() =>
  documents.value.reduce((total, document) => total + document.pendingVersionCount, 0),
)

const emptyState = computed(() => {
  if (loadError.value) return { title: 'Chưa tải được hàng đợi', description: 'Xem thông báo lỗi phía trên và thử lại.' }
  if (filters.value.keyword.trim()) return { title: 'Không tìm thấy mục phù hợp', description: 'Thử đổi từ khóa tìm kiếm.' }
  if (isPendingQueue.value) return { title: 'Không có tài liệu chờ duyệt', description: 'Hàng đợi trống — mọi phiên bản đã được xử lý.' }
  return { title: 'Không có tài liệu ở trạng thái này', description: undefined }
})

const pendingVersions = computed(
  () => selectedDocument.value?.versions.filter((version) => version.status === 'PendingReview') ?? [],
)
const otherVersions = computed(
  () => selectedDocument.value?.versions.filter((version) => version.status !== 'PendingReview') ?? [],
)

const columns: ColumnDef<DocumentListItem>[] = [
  {
    accessorKey: 'title',
    header: 'Tài liệu',
    meta: { cellClass: 'min-w-52 whitespace-normal' },
    cell: ({ row }) =>
      h('div', { class: 'min-w-0' }, [
        h('p', { class: 'text-xs font-bold text-foreground break-words' }, row.original.title),
        h(
          'p',
          { class: 'mt-0.5 text-[10px] text-muted-foreground' },
          `${row.original.folderPath} · ${formatDate(row.original.updatedAt)}`,
        ),
      ]),
  },
  { accessorKey: 'createdBy', header: 'Người gửi', meta: { cellClass: 'whitespace-normal' } },
  {
    accessorKey: 'status',
    header: 'Trạng thái',
    // Tài liệu đã duyệt nhưng có phiên bản mới chờ duyệt vẫn nằm trong hàng đợi — hiện số phiên bản chờ
    cell: ({ row }) =>
      row.original.pendingVersionCount > 0
        ? h(StatusBadge, { status: 'PendingReview', label: `${row.original.pendingVersionCount} chờ duyệt` })
        : h(StatusBadge, { status: row.original.status }),
  },
]

let listRequestId = 0

async function loadQueue() {
  if (!authStore.accessToken) return
  const requestId = ++listRequestId
  loadError.value = ''
  isLoading.value = true

  try {
    const documentList = await getDocuments(authStore.accessToken, {
      status: filters.value.status === 'all' ? undefined : filters.value.status,
    })
    if (requestId === listRequestId) documents.value = documentList
  } catch (error) {
    if (requestId === listRequestId) loadError.value = errorMessage(error, 'Không thể tải hàng đợi duyệt.')
  } finally {
    if (requestId === listRequestId) isLoading.value = false
  }
}

watch(() => filters.value.status, () => void loadQueue())

async function fetchDetail(documentId: string) {
  if (!authStore.accessToken) return null
  const detail = await getDocument(documentId, authStore.accessToken)
  if (selectedId.value === documentId) selectedDocument.value = detail
  return detail
}

async function selectDocument(document: DocumentListItem) {
  selectedId.value = document.id
  isDetailLoading.value = true
  if (isStacked.value) {
    await nextTick()
    detailPanelRef.value?.scrollIntoView({ block: 'start' })
  }

  try {
    await fetchDetail(document.id)
  } catch (error) {
    if (selectedId.value === document.id) selectedDocument.value = null
    toast.error(errorMessage(error, 'Không thể tải chi tiết tài liệu.'))
  } finally {
    if (selectedId.value === document.id) isDetailLoading.value = false
  }
}

function clearSelection() {
  selectedId.value = null
  selectedDocument.value = null
  isDetailLoading.value = false
}

async function refreshAll() {
  await loadQueue()
  if (!selectedId.value) return
  try {
    await fetchDetail(selectedId.value)
  } catch {
    clearSelection()
  }
}

// Đọc lại trạng thái mới nhất trước khi gửi quyết định: nếu phiên bản đã được người khác xử lý
// thì dừng và làm mới thay vì ghi đè (máy chủ hiện không chặn duyệt lại phiên bản đã xử lý)
async function ensureStillPending(version: DocumentVersion) {
  if (!selectedDocument.value) return false
  const latest = await fetchDetail(selectedDocument.value.id)
  const latestVersion = latest?.versions.find((item) => item.id === version.id)
  if (latestVersion?.status === 'PendingReview') return true
  toast.warning('Phiên bản này đã được xử lý hoặc không còn tồn tại. Hàng đợi đã được làm mới.')
  await loadQueue()
  return false
}

async function approve(version: DocumentVersion) {
  if (!authStore.accessToken || !selectedDocument.value || isSaving.value) return
  const document = selectedDocument.value

  const confirmed = await confirm({
    title: 'Xác nhận duyệt phiên bản',
    description: `Phê duyệt “${document.title}” · phiên bản ${version.versionNumber}? Phiên bản này sẽ trở thành bản hiện tại và được đưa vào xử lý, lập chỉ mục.`,
    confirmText: 'Phê duyệt',
  })
  if (!confirmed) return

  isSaving.value = true
  try {
    if (!(await ensureStillPending(version))) return
    await approveDocument(document.id, version.id, authStore.accessToken)
    toast.success('Đã duyệt phiên bản.')
    await refreshAll()
  } catch (error) {
    toast.error(errorMessage(error, 'Không thể duyệt phiên bản.'))
    await refreshAll()
  } finally {
    isSaving.value = false
  }
}

function openRejectDialog(version: DocumentVersion) {
  rejectTarget.value = version
  rejectReason.value = ''
  rejectAttempted.value = false
  rejectError.value = ''
}

async function submitReject() {
  const version = rejectTarget.value
  if (!authStore.accessToken || !selectedDocument.value || !version || isSaving.value) return
  rejectAttempted.value = true
  // Lý do bắt buộc — khớp ràng buộc reject_reason_required của máy chủ
  if (!rejectReason.value.trim()) return
  const document = selectedDocument.value
  rejectError.value = ''
  isSaving.value = true

  try {
    if (!(await ensureStillPending(version))) {
      rejectTarget.value = null
      return
    }
    await rejectDocument(document.id, version.id, rejectReason.value.trim(), authStore.accessToken)
    rejectTarget.value = null
    toast.success('Đã từ chối phiên bản.')
    await refreshAll()
  } catch (error) {
    rejectError.value = errorMessage(error, 'Không thể từ chối phiên bản.')
    void refreshAll()
  } finally {
    isSaving.value = false
  }
}

async function submitDownload(version: DocumentVersion) {
  if (!authStore.accessToken || !selectedDocument.value) return
  try {
    await downloadDocument(selectedDocument.value.id, authStore.accessToken, version.id)
    toast.success('Đã bắt đầu tải file.')
  } catch (error) {
    toast.error(errorMessage(error, 'Không thể tải file.'))
  }
}

onMounted(loadQueue)
</script>

<template>
  <div class="mx-auto max-w-[1200px]">
    <PageHeader
      eyebrow="Quality & review"
      title="Duyệt tài liệu"
      description="Kiểm tra phiên bản chờ duyệt, chấp thuận hoặc từ chối kèm lý do."
    >
      <template #actions>
        <Badge v-if="isPendingQueue && !isLoading && !loadError" variant="soft" class="bg-warning/10 text-warning">
          {{ pendingVersionTotal }} đang chờ
        </Badge>
        <Button variant="outline" class="h-10" :disabled="isLoading" @click="refreshAll">
          <RefreshCwIcon class="size-4" />
          Làm mới
        </Button>
      </template>
    </PageHeader>

    <div class="mb-4 flex flex-wrap items-center gap-2.5" role="search">
      <SearchInput
        v-model="filters.keyword"
        input-id="review-search"
        label="Tìm tài liệu trong hàng đợi"
        placeholder="Tìm theo tên tài liệu hoặc người gửi..."
      />
      <Select v-model="filters.status">
        <SelectTrigger id="review-filter-status" aria-label="Lọc hàng đợi" class="h-10! w-44 bg-card max-[750px]:flex-1">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="PendingReview">Chờ duyệt</SelectItem>
          <SelectItem value="all">Tất cả trạng thái</SelectItem>
          <SelectItem value="Approved">Đã duyệt</SelectItem>
          <SelectItem value="Rejected">Đã từ chối</SelectItem>
        </SelectContent>
      </Select>
    </div>

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadQueue" />

    <div class="grid items-start gap-4 grid-cols-[minmax(0,1.12fr)_minmax(330px,1fr)] max-[1100px]:grid-cols-1">
      <PanelCard :title="isPendingQueue ? 'Hàng đợi duyệt phiên bản' : 'Tài liệu theo trạng thái'">
        <template #actions>
          <Badge variant="muted">{{ documents.length }} mục</Badge>
        </template>
        <DataTable
          :columns="columns"
          :data="documents"
          :is-loading="isLoading"
          :global-filter="filters.keyword"
          clickable-rows
          bare
          table-class="min-w-[520px]"
          count-label="mục"
          :row-key="(row) => row.id"
          :selected-row-key="selectedId"
          :row-label="(row) => `Xem phiên bản cần duyệt của ${row.title}`"
          :empty-title="emptyState.title"
          :empty-description="emptyState.description"
          @row-click="selectDocument"
        />
      </PanelCard>

      <div ref="detailPanelRef" class="scroll-mt-4">
        <DetailPanel
          label="Reviewer workspace"
          aria-label="Chi tiết duyệt tài liệu"
          :title="selectedDocument?.title ?? (isDetailLoading ? 'Đang tải...' : 'Chọn phiên bản')"
          :description="selectedDocument ? (selectedDocument.description || selectedDocument.folderPath) : 'Chọn dòng trong danh sách để xem nội dung cần duyệt.'"
          :closable="Boolean(selectedId)"
          close-label="Bỏ chọn tài liệu"
          @close="clearSelection"
        >
          <template v-if="selectedDocument" #meta>
            <span class="text-[10px] text-muted-foreground">{{ pendingVersions.length }} phiên bản chờ duyệt</span>
          </template>

          <div v-if="isDetailLoading && !selectedDocument" class="grid gap-2.5" aria-busy="true">
            <Skeleton class="h-36 w-full" />
            <Skeleton class="h-14 w-full" />
            <Skeleton class="h-14 w-full" />
          </div>

          <EmptyState
            v-else-if="!selectedDocument"
            :icon="ClipboardCheckIcon"
            title="Chưa chọn tài liệu"
            description="Bấm vào dòng trong hàng đợi để xem các phiên bản chờ duyệt."
          />

          <div v-else class="grid gap-4" :aria-busy="isDetailLoading || isSaving">
            <section
              v-for="version in pendingVersions"
              :key="version.id"
              class="rounded-xl border p-3.5"
              :aria-label="`Phiên bản ${version.versionNumber} chờ duyệt`"
            >
              <div class="flex items-center justify-between gap-2">
                <h3 class="text-sm font-bold">Phiên bản {{ version.versionNumber }}</h3>
                <StatusBadge :status="version.status" />
              </div>

              <div class="my-3 grid justify-items-center gap-1.5 rounded-[10px] border bg-linear-135 from-primary-soft/70 to-surface px-4 py-5 text-center">
                <FileTypeIcon :file-name="version.originalFileName" />
                <p class="max-w-full truncate text-xs font-bold text-foreground/80">{{ version.originalFileName }}</p>
                <p class="text-[10px] text-muted-foreground">
                  {{ formatFileSize(version.fileSizeBytes) }} · gửi {{ formatDate(version.createdAt) }}
                </p>
                <Button variant="outline" size="sm" type="button" class="mt-1 text-xs" @click="submitDownload(version)">
                  <DownloadIcon class="size-4" />
                  Tải file để kiểm tra
                </Button>
              </div>

              <MetaGrid>
                <MetaItem label="Người gửi">{{ version.uploadedBy }}</MetaItem>
                <MetaItem label="Trạng thái">{{ statusLabel(version.status) }}</MetaItem>
                <MetaItem label="Ngày gửi">{{ formatDateTime(version.createdAt) }}</MetaItem>
                <MetaItem label="Phiên bản">{{ version.versionNumber }}</MetaItem>
              </MetaGrid>

              <InlineNotice tone="warning" class="my-3">
                Reviewer cần kiểm tra nội dung file trước khi quyết định. Quyết định được ghi nhận và kiểm tra quyền ở máy chủ.
              </InlineNotice>

              <div class="flex flex-wrap justify-end gap-2">
                <Button variant="outline" type="button" :disabled="isSaving" @click="openRejectDialog(version)">
                  <XIcon class="size-4" />
                  Từ chối
                </Button>
                <Button type="button" :disabled="isSaving" @click="approve(version)">
                  <CheckIcon class="size-4" />
                  Phê duyệt
                </Button>
              </div>
            </section>

            <EmptyState
              v-if="pendingVersions.length === 0"
              :icon="CheckIcon"
              title="Tài liệu này không còn phiên bản chờ duyệt"
              description="Các phiên bản đã được xử lý được liệt kê bên dưới."
            />

            <div v-if="otherVersions.length">
              <h3 class="mb-2 text-xs font-bold text-muted-foreground">Các phiên bản đã xử lý</h3>
              <ul class="grid gap-2">
                <li v-for="version in otherVersions" :key="version.id" class="rounded-lg border px-3 py-2.5">
                  <div class="flex items-center justify-between gap-2">
                    <div class="flex min-w-0 items-center gap-2.5">
                      <FileTypeIcon :file-name="version.originalFileName" />
                      <div class="min-w-0">
                        <p class="text-xs font-bold">Phiên bản {{ version.versionNumber }}</p>
                        <p class="truncate text-[10px] text-muted-foreground">
                          {{ version.originalFileName }}<template v-if="version.reviewedBy"> · duyệt bởi {{ version.reviewedBy }}</template>
                        </p>
                      </div>
                    </div>
                    <StatusBadge :status="version.status" />
                  </div>
                  <p v-if="version.rejectReason" class="mt-1.5 text-[11px] text-destructive">Lý do từ chối: {{ version.rejectReason }}</p>
                </li>
              </ul>
            </div>
          </div>
        </DetailPanel>
      </div>
    </div>

    <Dialog v-model:open="isRejectDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Từ chối phiên bản</DialogTitle>
          <DialogDescription>
            Từ chối “{{ selectedDocument?.title }}” · phiên bản {{ rejectTarget?.versionNumber }}. Người tải lên sẽ thấy lý do và
            phải tải phiên bản mới nếu muốn tiếp tục.
          </DialogDescription>
        </DialogHeader>
        <form id="reject-version-form" class="grid gap-3.5" novalidate @submit.prevent="submitReject">
          <div class="grid gap-1.5">
            <Label for="reject-reason">Lý do từ chối <span class="text-destructive" aria-hidden="true">*</span></Label>
            <Textarea
              id="reject-reason"
              v-model="rejectReason"
              rows="4"
              class="min-h-24"
              placeholder="Nêu lý do cần chỉnh sửa, ví dụ: nội dung trùng lặp, sai thư mục..."
              :aria-invalid="rejectAttempted && !rejectReason.trim()"
              :aria-describedby="rejectAttempted && !rejectReason.trim() ? 'reject-reason-error' : undefined"
            />
            <p v-if="rejectAttempted && !rejectReason.trim()" id="reject-reason-error" class="text-xs text-destructive" role="alert">
              Vui lòng nhập lý do từ chối.
            </p>
          </div>
          <p v-if="rejectError" class="text-sm text-destructive" role="alert">{{ rejectError }}</p>
        </form>
        <DialogFooter>
          <Button variant="outline" type="button" :disabled="isSaving" @click="isRejectDialogOpen = false">Hủy</Button>
          <Button variant="destructive" type="submit" form="reject-version-form" :disabled="isSaving">
            {{ isSaving ? 'Đang gửi...' : 'Từ chối' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
