<script setup lang="ts">
import { computed, h, nextTick, onMounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import type { ColumnDef } from '@tanstack/vue-table'
import { useDebounceFn, useMediaQuery } from '@vueuse/core'
import { DownloadIcon, FileTextIcon, PlusIcon, ShieldCheckIcon, SparklesIcon, UploadIcon } from '@lucide/vue'
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
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import DataTable from '@/components/app/DataTable.vue'
import DetailPanel from '@/components/app/DetailPanel.vue'
import EmptyState from '@/components/app/EmptyState.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import FileTypeIcon from '@/components/app/FileTypeIcon.vue'
import FileUpload from '@/components/app/FileUpload.vue'
import FolderSelect from '@/components/app/FolderSelect.vue'
import InlineNotice from '@/components/app/InlineNotice.vue'
import MetaGrid from '@/components/app/MetaGrid.vue'
import MetaItem from '@/components/app/MetaItem.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import PanelCard from '@/components/app/PanelCard.vue'
import SearchInput from '@/components/app/SearchInput.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import { statusLabel } from '@/components/app/status'
import type { DocumentDetail, DocumentListItem, DocumentStatus, DocumentVersion } from '@/api/documents'
import { downloadDocument, getDocument, getDocuments, uploadDocument, uploadDocumentVersion } from '@/api/documents'
import { generateWikiDraft } from '@/api/wiki'
import { useAuthStore } from '@/stores/auth'
import { useFolderTree } from '@/composables/useFolderTree'
import { useToast } from '@/composables/useToast'
import { errorMessage } from '@/lib/errors'
import { formatDate, formatFileSize } from '@/lib/format'

const ACCEPTED_FILES = '.pdf,.docx,.md,.markdown,.txt'

const authStore = useAuthStore()
const toast = useToast()
const { flatFolders, loadFolders } = useFolderTree()
// Dưới 1100px bảng và panel chi tiết xếp chồng — chọn dòng thì cuộn tới panel chi tiết
const isStacked = useMediaQuery('(max-width: 1100px)')

const documents = ref<DocumentListItem[]>([])
const selectedId = ref<string | null>(null)
const selectedDocument = ref<DocumentDetail | null>(null)
const isLoading = ref(false)
const isDetailLoading = ref(false)
const isSaving = ref(false)
const isGeneratingWiki = ref(false)
const loadError = ref('')
const detailError = ref('')
const detailPanelRef = ref<HTMLElement | null>(null)

const filters = ref({ status: 'all' as 'all' | DocumentStatus, keyword: '', folderId: '' })

const isUploadDialogOpen = ref(false)
const uploadForm = ref({ folderId: '', title: '', description: '' })
const uploadFile = ref<File | null>(null)
const uploadError = ref('')
const uploadAttempted = ref(false)

const isVersionDialogOpen = ref(false)
const versionFile = ref<File | null>(null)
const versionError = ref('')

const hasActiveFilters = computed(
  () => filters.value.status !== 'all' || Boolean(filters.value.keyword.trim()) || Boolean(filters.value.folderId),
)

// Phân biệt: lỗi tải, lọc không ra kết quả, và thật sự chưa có tài liệu
const emptyState = computed(() => {
  if (loadError.value) return { title: 'Chưa tải được danh sách tài liệu', description: 'Xem thông báo lỗi phía trên và thử lại.' }
  if (hasActiveFilters.value) return { title: 'Không tìm thấy tài liệu phù hợp', description: 'Thử bỏ bớt bộ lọc hoặc đổi từ khóa.' }
  return { title: 'Chưa có tài liệu nào', description: 'Bấm “Tải tài liệu” để thêm tài liệu đầu tiên.' }
})

const selectedListItem = computed(() => documents.value.find((item) => item.id === selectedDocument.value?.id))

const currentVersion = computed(() =>
  selectedDocument.value?.versions.find((version) => version.id === selectedDocument.value?.currentVersionId),
)

const canGenerateWiki = computed(() => {
  if (!selectedDocument.value || (!authStore.isReviewer && !authStore.isAdmin)) return false
  return selectedDocument.value.status === 'Approved' && currentVersion.value?.status === 'Indexed'
})

// Lối tắt sang hàng đợi duyệt chỉ cho người có quyền vào route /review (guard router vẫn là nơi chặn)
const canOpenReviewQueue = computed(
  () =>
    (authStore.isReviewer || authStore.isAdmin) &&
    Boolean(selectedDocument.value?.versions.some((version) => version.status === 'PendingReview')),
)

const uploadErrors = computed(() => ({
  title: uploadForm.value.title.trim() ? '' : 'Vui lòng nhập tên tài liệu.',
  folderId: uploadForm.value.folderId ? '' : 'Vui lòng chọn thư mục.',
  file: uploadFile.value ? '' : 'Vui lòng chọn file cần tải lên.',
}))

const columns: ColumnDef<DocumentListItem>[] = [
  {
    accessorKey: 'title',
    header: 'Tên tài liệu',
    meta: { cellClass: 'min-w-56 whitespace-normal' },
    cell: ({ row }) =>
      h('div', { class: 'flex items-center gap-2.5' }, [
        h(FileTypeIcon),
        h('div', { class: 'min-w-0' }, [
          h('p', { class: 'text-xs font-bold text-foreground break-words' }, row.original.title),
          // Gộp 2 cột cũ "Phiên bản" + "Lập chỉ mục" thành dòng phụ để giữ bố cục 4 cột của B1
          h(
            'p',
            { class: 'mt-0.5 text-[10px] text-muted-foreground' },
            `Phiên bản ${row.original.currentVersionNumber ?? '—'}/${row.original.latestVersionNumber} · ${statusLabel(row.original.latestVersionStatus)}`,
          ),
        ]),
      ]),
  },
  { accessorKey: 'folderPath', header: 'Thư mục', meta: { cellClass: 'whitespace-normal break-words' } },
  {
    accessorKey: 'status',
    header: 'Trạng thái',
    cell: ({ row }) => h(StatusBadge, { status: row.original.status }),
  },
  {
    accessorKey: 'updatedAt',
    header: 'Cập nhật',
    cell: ({ row }) => h('span', { class: 'tabular-nums' }, formatDate(row.original.updatedAt)),
  },
]

// Chống race khi đổi bộ lọc liên tục: chỉ áp kết quả của request mới nhất
let listRequestId = 0

async function loadDocuments() {
  if (!authStore.accessToken) return
  const requestId = ++listRequestId
  loadError.value = ''
  isLoading.value = true

  try {
    const documentList = await getDocuments(authStore.accessToken, {
      folderId: filters.value.folderId || undefined,
      status: filters.value.status === 'all' ? undefined : filters.value.status,
      keyword: filters.value.keyword.trim() || undefined,
    })
    if (requestId === listRequestId) documents.value = documentList
  } catch (error) {
    if (requestId === listRequestId) loadError.value = errorMessage(error, 'Không thể tải danh sách tài liệu.')
  } finally {
    if (requestId === listRequestId) isLoading.value = false
  }
}

async function loadFolderOptions() {
  if (!authStore.accessToken) return
  try {
    await loadFolders(authStore.accessToken)
  } catch (error) {
    toast.error(errorMessage(error, 'Không thể tải danh sách thư mục.'))
  }
}

// Lọc phía máy chủ như trước, nhưng áp dụng ngay khi đổi bộ lọc (B1 không có nút "Lọc")
const reloadForKeyword = useDebounceFn(loadDocuments, 350)
watch(() => [filters.value.status, filters.value.folderId], () => void loadDocuments())
watch(() => filters.value.keyword, () => void reloadForKeyword())

async function selectDocument(document: DocumentListItem) {
  if (!authStore.accessToken) return
  selectedId.value = document.id
  detailError.value = ''
  isDetailLoading.value = true
  if (isStacked.value) {
    await nextTick()
    detailPanelRef.value?.scrollIntoView({ block: 'start' })
  }

  try {
    const detail = await getDocument(document.id, authStore.accessToken)
    if (selectedId.value === document.id) selectedDocument.value = detail
  } catch (error) {
    if (selectedId.value === document.id) {
      selectedDocument.value = null
      detailError.value = errorMessage(error, 'Không thể tải chi tiết tài liệu.')
    }
    toast.error(errorMessage(error, 'Không thể tải chi tiết tài liệu.'))
  } finally {
    if (selectedId.value === document.id) isDetailLoading.value = false
  }
}

function clearSelection() {
  selectedId.value = null
  selectedDocument.value = null
  detailError.value = ''
  isDetailLoading.value = false
}

function showDocument(detail: DocumentDetail) {
  selectedId.value = detail.id
  selectedDocument.value = detail
  detailError.value = ''
}

function openUploadDialog() {
  uploadForm.value = { folderId: '', title: '', description: '' }
  uploadFile.value = null
  uploadError.value = ''
  uploadAttempted.value = false
  isUploadDialogOpen.value = true
}

async function submitUpload() {
  if (!authStore.accessToken || isSaving.value) return
  uploadAttempted.value = true
  const errors = uploadErrors.value
  if (errors.title || errors.folderId || errors.file || !uploadFile.value) return
  uploadError.value = ''
  isSaving.value = true

  try {
    const detail = await uploadDocument(
      { ...uploadForm.value, file: uploadFile.value },
      authStore.accessToken,
    )
    showDocument(detail)
    isUploadDialogOpen.value = false
    toast.success('Đã tải tài liệu lên, đang chờ reviewer duyệt.')
    await loadDocuments()
  } catch (error) {
    uploadError.value = errorMessage(error, 'Không thể tải tài liệu lên.')
  } finally {
    isSaving.value = false
  }
}

function openVersionDialog() {
  versionFile.value = null
  versionError.value = ''
  isVersionDialogOpen.value = true
}

async function submitUploadVersion() {
  if (!authStore.accessToken || !selectedDocument.value || !versionFile.value || isSaving.value) return
  versionError.value = ''
  isSaving.value = true

  try {
    const detail = await uploadDocumentVersion(
      selectedDocument.value.id,
      versionFile.value,
      authStore.accessToken,
    )
    showDocument(detail)
    versionFile.value = null
    isVersionDialogOpen.value = false
    toast.success('Đã tải phiên bản mới lên, đang chờ reviewer duyệt.')
    await loadDocuments()
  } catch (error) {
    versionError.value = errorMessage(error, 'Không thể tải phiên bản mới lên.')
  } finally {
    isSaving.value = false
  }
}

async function submitDownload(versionId?: string) {
  if (!authStore.accessToken || !selectedDocument.value) return
  try {
    await downloadDocument(selectedDocument.value.id, authStore.accessToken, versionId)
    toast.success('Đã bắt đầu tải file.')
  } catch (error) {
    toast.error(errorMessage(error, 'Không thể tải file.'))
  }
}

async function submitGenerateWikiDraft() {
  if (!authStore.accessToken || !selectedDocument.value?.currentVersionId || isGeneratingWiki.value) return
  isGeneratingWiki.value = true
  try {
    await generateWikiDraft(
      {
        documentId: selectedDocument.value.id,
        documentVersionId: selectedDocument.value.currentVersionId,
      },
      authStore.accessToken,
    )
    toast.success('Đã tạo wiki draft từ tài liệu hiện tại.')
  } catch (error) {
    toast.error(errorMessage(error, 'Không thể tạo wiki draft.'))
  } finally {
    isGeneratingWiki.value = false
  }
}

function extractionLines(version: DocumentVersion): string[] {
  return [
    version.hasNormalizedText ? `${version.sectionCount ?? 0} mục đã trích xuất` : '',
    [version.language, version.documentType, version.sensitivity].filter(Boolean).join(' / '),
    version.documentSummary ?? '',
    version.keyTopics.length ? `Chủ đề: ${version.keyTopics.join(', ')}` : '',
    version.entities.length ? `Thực thể: ${version.entities.join(', ')}` : '',
  ].filter(Boolean)
}

onMounted(() => {
  void loadFolderOptions()
  void loadDocuments()
})
</script>

<template>
  <div class="mx-auto max-w-[1200px]">
    <PageHeader
      eyebrow="Knowledge management"
      title="Quản lý tài liệu"
      description="Tải lên, quản lý phiên bản và theo dõi quá trình duyệt, lập chỉ mục."
    >
      <template #actions>
        <Button class="h-10" @click="openUploadDialog">
          <UploadIcon class="size-4" />
          Tải tài liệu
        </Button>
      </template>
    </PageHeader>

    <form class="mb-4 flex flex-wrap items-center gap-2.5" role="search" @submit.prevent="loadDocuments">
      <SearchInput
        v-model="filters.keyword"
        input-id="doc-filter-keyword"
        label="Tìm kiếm tài liệu"
        placeholder="Tìm tài liệu theo tên hoặc mô tả..."
      />
      <Select v-model="filters.status">
        <SelectTrigger id="doc-filter-status" aria-label="Lọc trạng thái" class="h-10! w-44 bg-card max-[750px]:flex-1">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">Tất cả trạng thái</SelectItem>
          <SelectItem value="PendingReview">Chờ duyệt</SelectItem>
          <SelectItem value="Approved">Đã duyệt</SelectItem>
          <SelectItem value="Rejected">Từ chối</SelectItem>
        </SelectContent>
      </Select>
      <Label for="doc-filter-folder" class="sr-only">Lọc thư mục</Label>
      <div class="w-48 max-[750px]:flex-1 [&_[data-slot=select-trigger]]:h-10 [&_[data-slot=select-trigger]]:bg-card">
        <FolderSelect
          v-model="filters.folderId"
          trigger-id="doc-filter-folder"
          :folders="flatFolders"
          none-label="Tất cả thư mục"
        />
      </div>
    </form>

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadDocuments" />

    <div class="grid items-start gap-4 grid-cols-[minmax(0,1.45fr)_minmax(260px,1fr)] max-[1100px]:grid-cols-1">
      <PanelCard title="Danh sách tài liệu" title-id="doc-list-title">
        <template #actions>
          <Badge variant="soft">{{ documents.length }} tài liệu</Badge>
        </template>
        <DataTable
          :columns="columns"
          :data="documents"
          :is-loading="isLoading"
          clickable-rows
          bare
          table-class="min-w-[520px]"
          count-label="tài liệu"
          :row-key="(row) => row.id"
          :selected-row-key="selectedId"
          :row-label="(row) => `Xem chi tiết ${row.title}`"
          :empty-title="emptyState.title"
          :empty-description="emptyState.description"
          @row-click="selectDocument"
        />
      </PanelCard>

      <div ref="detailPanelRef" class="scroll-mt-4">
        <DetailPanel
          label="Chi tiết tài liệu"
          aria-label="Chi tiết tài liệu"
          :title="selectedDocument?.title ?? (isDetailLoading ? 'Đang tải...' : 'Chọn tài liệu')"
          :description="selectedDocument ? (selectedDocument.description || selectedDocument.folderPath) : 'Nhấn vào một dòng để xem chi tiết.'"
          :closable="Boolean(selectedId)"
          close-label="Bỏ chọn tài liệu"
          @close="clearSelection"
        >
          <div v-if="isDetailLoading && !selectedDocument" class="grid gap-2.5" aria-busy="true">
            <Skeleton class="h-14 w-full" />
            <Skeleton class="h-14 w-full" />
            <Skeleton class="h-24 w-full" />
          </div>

          <ErrorState v-else-if="detailError" :message="detailError" hide-retry />

          <EmptyState
            v-else-if="!selectedDocument"
            :icon="FileTextIcon"
            title="Chưa chọn tài liệu"
            description="Chọn một dòng để xem trạng thái và các phiên bản."
          />

          <div v-else :aria-busy="isDetailLoading">
            <MetaGrid>
              <MetaItem label="Thư mục">{{ selectedDocument.folderPath }}</MetaItem>
              <MetaItem label="Trạng thái"><StatusBadge :status="selectedDocument.status" /></MetaItem>
              <MetaItem label="Phiên bản hiện tại">
                {{ currentVersion?.versionNumber ?? '—' }} / {{ selectedDocument.versions.length }}
              </MetaItem>
              <MetaItem label="Cập nhật">{{ formatDate(selectedListItem?.updatedAt) }}</MetaItem>
            </MetaGrid>

            <div class="mt-4 mb-2.5 flex items-center justify-between gap-2">
              <h3 class="text-sm font-bold">Lịch sử phiên bản</h3>
              <Button variant="outline" size="sm" type="button" class="text-xs" @click="openVersionDialog">
                <PlusIcon class="size-4" />
                Phiên bản mới
              </Button>
            </div>

            <ul class="grid gap-2" aria-label="Các phiên bản">
              <li v-for="version in selectedDocument.versions" :key="version.id" class="rounded-lg border p-3">
                <div class="flex items-start justify-between gap-2.5">
                  <div class="flex min-w-0 items-start gap-2.5">
                    <FileTypeIcon :file-name="version.originalFileName" />
                    <div class="min-w-0">
                      <p class="flex flex-wrap items-center gap-1.5 text-xs font-bold">
                        Phiên bản {{ version.versionNumber }}
                        <Badge v-if="version.id === selectedDocument.currentVersionId" variant="soft" class="text-[10px]">Hiện tại</Badge>
                      </p>
                      <button
                        type="button"
                        class="max-w-full truncate rounded-sm text-left text-[11px] text-primary-soft-foreground underline-offset-4 outline-none hover:underline focus-visible:ring-2 focus-visible:ring-ring"
                        :aria-label="`Tải file ${version.originalFileName} (phiên bản ${version.versionNumber})`"
                        @click="submitDownload(version.id)"
                      >
                        {{ version.originalFileName }}
                      </button>
                      <p class="text-[10px] text-muted-foreground">
                        {{ formatFileSize(version.fileSizeBytes) }} · {{ version.uploadedBy }} · {{ formatDate(version.createdAt) }}
                      </p>
                    </div>
                  </div>
                  <StatusBadge :status="version.status" />
                </div>
                <p v-if="version.rejectReason" class="mt-2 text-[11px] text-destructive">Lý do từ chối: {{ version.rejectReason }}</p>
                <div
                  v-if="extractionLines(version).length || version.processingWarnings.length || version.qualityWarnings.length"
                  class="mt-2 grid gap-0.5 border-t pt-2 text-[11px] text-muted-foreground"
                >
                  <span v-for="line in extractionLines(version)" :key="line">{{ line }}</span>
                  <span v-if="version.processingWarnings.length" class="text-warning">{{ version.processingWarnings.join(', ') }}</span>
                  <span v-if="version.qualityWarnings.length" class="text-warning">{{ version.qualityWarnings.join(', ') }}</span>
                </div>
              </li>
            </ul>

            <div class="my-4 h-px bg-border"></div>
            <div class="flex flex-wrap gap-2">
              <Button variant="outline" size="sm" type="button" class="text-xs" @click="submitDownload()">
                <DownloadIcon class="size-4" />
                Tải bản hiện tại
              </Button>
              <Button
                v-if="canGenerateWiki"
                variant="subtle"
                size="sm"
                type="button"
                class="text-xs"
                :disabled="isGeneratingWiki"
                @click="submitGenerateWikiDraft"
              >
                <SparklesIcon class="size-4" />
                {{ isGeneratingWiki ? 'Đang tạo...' : 'Tạo wiki draft' }}
              </Button>
              <Button v-if="canOpenReviewQueue" as-child variant="subtle" size="sm" class="text-xs">
                <RouterLink :to="{ name: 'review' }">
                  <ShieldCheckIcon class="size-4" />
                  Xem hàng đợi duyệt
                </RouterLink>
              </Button>
            </div>
          </div>
        </DetailPanel>
      </div>
    </div>

    <Dialog v-model:open="isUploadDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Tải tài liệu lên</DialogTitle>
          <DialogDescription>Tài liệu mới sẽ ở trạng thái chờ duyệt cho đến khi reviewer phê duyệt.</DialogDescription>
        </DialogHeader>
        <form id="upload-document-form" class="grid gap-3.5" novalidate @submit.prevent="submitUpload">
          <div class="grid gap-1.5">
            <Label for="upload-title">Tên tài liệu <span class="text-destructive" aria-hidden="true">*</span></Label>
            <Input
              id="upload-title"
              v-model="uploadForm.title"
              type="text"
              placeholder="Nhập tên tài liệu"
              :aria-invalid="uploadAttempted && Boolean(uploadErrors.title)"
              :aria-describedby="uploadAttempted && uploadErrors.title ? 'upload-title-error' : undefined"
            />
            <p v-if="uploadAttempted && uploadErrors.title" id="upload-title-error" class="text-xs text-destructive">{{ uploadErrors.title }}</p>
          </div>
          <div class="grid gap-1.5">
            <Label for="upload-folder">Thư mục <span class="text-destructive" aria-hidden="true">*</span></Label>
            <FolderSelect v-model="uploadForm.folderId" trigger-id="upload-folder" :folders="flatFolders" />
            <p v-if="uploadAttempted && uploadErrors.folderId" class="text-xs text-destructive">{{ uploadErrors.folderId }}</p>
          </div>
          <div class="grid gap-1.5">
            <Label for="upload-description">Mô tả</Label>
            <Input id="upload-description" v-model="uploadForm.description" type="text" />
          </div>
          <div class="grid gap-1.5">
            <Label for="upload-file">File <span class="text-destructive" aria-hidden="true">*</span></Label>
            <FileUpload v-model="uploadFile" input-id="upload-file" :accept="ACCEPTED_FILES" />
            <p v-if="uploadAttempted && uploadErrors.file" class="text-xs text-destructive">{{ uploadErrors.file }}</p>
          </div>
          <InlineNotice tone="info">Quyền tải lên, phiên bản và trạng thái xử lý do máy chủ quyết định.</InlineNotice>
          <p v-if="uploadError" class="text-sm text-destructive" role="alert">{{ uploadError }}</p>
        </form>
        <DialogFooter>
          <Button variant="outline" type="button" :disabled="isSaving" @click="isUploadDialogOpen = false">Hủy</Button>
          <Button type="submit" form="upload-document-form" :disabled="isSaving">
            {{ isSaving ? 'Đang tải lên...' : 'Tải lên' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <Dialog v-model:open="isVersionDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Tải lên phiên bản mới</DialogTitle>
          <DialogDescription>
            Phiên bản mới của “{{ selectedDocument?.title }}” sẽ ở trạng thái chờ duyệt cho đến khi reviewer phê duyệt.
          </DialogDescription>
        </DialogHeader>
        <form id="upload-version-form" class="grid gap-3.5" @submit.prevent="submitUploadVersion">
          <div class="grid gap-1.5">
            <Label for="version-file">File phiên bản mới</Label>
            <FileUpload v-model="versionFile" input-id="version-file" :accept="ACCEPTED_FILES" />
          </div>
          <p v-if="versionError" class="text-sm text-destructive" role="alert">{{ versionError }}</p>
        </form>
        <DialogFooter>
          <Button variant="outline" type="button" :disabled="isSaving" @click="isVersionDialogOpen = false">Hủy</Button>
          <Button type="submit" form="upload-version-form" :disabled="!versionFile || isSaving">
            {{ isSaving ? 'Đang tải lên...' : 'Tải phiên bản lên' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
