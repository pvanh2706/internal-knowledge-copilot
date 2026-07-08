<script setup lang="ts">
import { computed, h, onMounted, ref } from 'vue'
import type { ColumnDef } from '@tanstack/vue-table'
import { DownloadIcon, FileTextIcon, SearchIcon, SparklesIcon, UploadIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import DataTable from '@/components/app/DataTable.vue'
import EmptyState from '@/components/app/EmptyState.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import FileUpload from '@/components/app/FileUpload.vue'
import FilterBar from '@/components/app/FilterBar.vue'
import FolderSelect from '@/components/app/FolderSelect.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import type { DocumentDetail, DocumentListItem, DocumentStatus } from '@/api/documents'
import { downloadDocument, getDocument, getDocuments, uploadDocument, uploadDocumentVersion } from '@/api/documents'
import { ApiError } from '@/api/http'
import { generateWikiDraft } from '@/api/wiki'
import { useAuthStore } from '@/stores/auth'
import { useFolderTree } from '@/composables/useFolderTree'
import { useToast } from '@/composables/useToast'

const ACCEPTED_FILES = '.pdf,.docx,.md,.markdown,.txt'

const authStore = useAuthStore()
const toast = useToast()
const { flatFolders, loadFolders } = useFolderTree()

const documents = ref<DocumentListItem[]>([])
const selectedDocument = ref<DocumentDetail | null>(null)
const isLoading = ref(false)
const isSaving = ref(false)
const loadError = ref('')
const uploadError = ref('')
const isUploadDialogOpen = ref(false)
const uploadFile = ref<File | null>(null)
const versionFile = ref<File | null>(null)

const filters = ref({ status: 'all' as 'all' | DocumentStatus, keyword: '', folderId: '' })
const uploadForm = ref({ folderId: '', title: '', description: '' })

const canGenerateWiki = computed(() => {
  if (!selectedDocument.value || (!authStore.isReviewer && !authStore.isAdmin)) return false
  return (
    selectedDocument.value.status === 'Approved' &&
    selectedDocument.value.versions.some(
      (version) => version.id === selectedDocument.value?.currentVersionId && version.status === 'Indexed',
    )
  )
})

const columns: ColumnDef<DocumentListItem>[] = [
  { accessorKey: 'title', header: 'Tên' },
  { accessorKey: 'folderPath', header: 'Thư mục' },
  {
    accessorKey: 'status',
    header: 'Trạng thái',
    cell: ({ row }) => h(StatusBadge, { status: row.original.status }),
  },
  {
    accessorKey: 'latestVersionNumber',
    header: 'Phiên bản',
    cell: ({ row }) => `${row.original.currentVersionNumber ?? '—'} / ${row.original.latestVersionNumber}`,
  },
  {
    accessorKey: 'latestVersionStatus',
    header: 'Lập chỉ mục',
    cell: ({ row }) => h(StatusBadge, { status: row.original.latestVersionStatus }),
  },
]

async function loadData() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    const [documentList] = await Promise.all([
      getDocuments(authStore.accessToken, {
        folderId: filters.value.folderId || undefined,
        status: filters.value.status === 'all' ? undefined : filters.value.status,
        keyword: filters.value.keyword || undefined,
      }),
      loadFolders(authStore.accessToken),
    ])
    documents.value = documentList
  } catch (error) {
    loadError.value = toMessage(error, 'Không thể tải danh sách tài liệu.')
  } finally {
    isLoading.value = false
  }
}

async function selectDocument(document: DocumentListItem) {
  if (!authStore.accessToken) return
  try {
    selectedDocument.value = await getDocument(document.id, authStore.accessToken)
  } catch (error) {
    toast.error(toMessage(error, 'Không thể tải chi tiết tài liệu.'))
  }
}

function openUploadDialog() {
  uploadForm.value = { folderId: '', title: '', description: '' }
  uploadFile.value = null
  uploadError.value = ''
  isUploadDialogOpen.value = true
}

async function submitUpload() {
  if (!authStore.accessToken || !uploadFile.value || isSaving.value) return
  uploadError.value = ''
  isSaving.value = true

  try {
    selectedDocument.value = await uploadDocument(
      { ...uploadForm.value, file: uploadFile.value },
      authStore.accessToken,
    )
    isUploadDialogOpen.value = false
    toast.success('Đã tải tài liệu lên, đang chờ reviewer duyệt.')
    await loadData()
  } catch (error) {
    uploadError.value = toMessage(error, 'Không thể tải tài liệu lên.')
  } finally {
    isSaving.value = false
  }
}

async function submitUploadVersion() {
  if (!authStore.accessToken || !selectedDocument.value || !versionFile.value || isSaving.value) return
  isSaving.value = true

  try {
    selectedDocument.value = await uploadDocumentVersion(
      selectedDocument.value.id,
      versionFile.value,
      authStore.accessToken,
    )
    versionFile.value = null
    toast.success('Đã tải phiên bản mới lên, đang chờ reviewer duyệt.')
    await loadData()
  } catch (error) {
    toast.error(toMessage(error, 'Không thể tải phiên bản mới lên.'))
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
    toast.error(toMessage(error, 'Không thể tải file.'))
  }
}

async function submitGenerateWikiDraft() {
  if (!authStore.accessToken || !selectedDocument.value?.currentVersionId) return
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
    toast.error(toMessage(error, 'Không thể tạo wiki draft.'))
  }
}

function toMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback
}

onMounted(loadData)
</script>

<template>
  <div>
    <PageHeader title="Tài liệu" description="Tải tài liệu lên, xem trạng thái duyệt và tải file gốc.">
      <template #actions>
        <Button @click="openUploadDialog">
          <UploadIcon class="size-4" />
          Tải tài liệu lên
        </Button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadData" />

    <form @submit.prevent="loadData">
      <FilterBar>
        <div class="grid gap-1.5">
          <Label for="doc-filter-folder">Thư mục</Label>
          <FolderSelect
            v-model="filters.folderId"
            trigger-id="doc-filter-folder"
            :folders="flatFolders"
            none-label="Tất cả thư mục"
          />
        </div>
        <div class="grid gap-1.5">
          <Label for="doc-filter-status">Trạng thái</Label>
          <Select v-model="filters.status">
            <SelectTrigger id="doc-filter-status" class="w-44">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tất cả trạng thái</SelectItem>
              <SelectItem value="PendingReview">Chờ duyệt</SelectItem>
              <SelectItem value="Approved">Đã duyệt</SelectItem>
              <SelectItem value="Rejected">Từ chối</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div class="grid gap-1.5">
          <Label for="doc-filter-keyword">Từ khóa</Label>
          <Input id="doc-filter-keyword" v-model="filters.keyword" type="text" class="w-56" placeholder="Tìm theo tên/mô tả" />
        </div>
        <Button type="submit" variant="outline" :disabled="isLoading">
          <SearchIcon class="size-4" />
          Lọc
        </Button>
      </FilterBar>
    </form>

    <div class="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.9fr)]">
      <DataTable
        :columns="columns"
        :data="documents"
        :is-loading="isLoading"
        clickable-rows
        empty-title="Chưa có tài liệu nào"
        empty-description="Bấm “Tải tài liệu lên” để thêm tài liệu đầu tiên."
        @row-click="selectDocument"
      />

      <EmptyState
        v-if="!selectedDocument"
        :icon="FileTextIcon"
        title="Chọn một tài liệu để xem chi tiết"
        description="Bấm vào dòng trong bảng để xem phiên bản, tải file và thao tác."
      />

      <Card v-else>
        <CardHeader>
          <CardTitle class="text-lg">{{ selectedDocument.title }}</CardTitle>
          <CardDescription class="flex flex-wrap items-center gap-2">
            <span class="font-mono text-xs">{{ selectedDocument.folderPath }}</span>
            <StatusBadge :status="selectedDocument.status" />
          </CardDescription>
        </CardHeader>
        <CardContent class="grid gap-5">
          <div class="flex flex-wrap gap-2">
            <Button variant="outline" size="sm" type="button" @click="submitDownload()">
              <DownloadIcon class="size-4" />
              Tải bản hiện tại
            </Button>
            <Button v-if="canGenerateWiki" variant="outline" size="sm" type="button" @click="submitGenerateWikiDraft">
              <SparklesIcon class="size-4" />
              Tạo wiki draft
            </Button>
          </div>

          <form class="grid gap-2" @submit.prevent="submitUploadVersion">
            <Label>Tải phiên bản mới</Label>
            <FileUpload v-model="versionFile" :accept="ACCEPTED_FILES" />
            <Button type="submit" size="sm" class="justify-self-start" :disabled="!versionFile || isSaving">
              {{ isSaving ? 'Đang tải lên...' : 'Tải phiên bản lên' }}
            </Button>
          </form>

          <div class="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Phiên bản</TableHead>
                  <TableHead>File</TableHead>
                  <TableHead>Trạng thái</TableHead>
                  <TableHead>Trích xuất</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                <TableRow v-for="version in selectedDocument.versions" :key="version.id">
                  <TableCell class="tabular-nums">{{ version.versionNumber }}</TableCell>
                  <TableCell>
                    <button
                      type="button"
                      class="rounded-sm text-left text-primary underline-offset-4 outline-none hover:underline focus-visible:ring-2 focus-visible:ring-ring"
                      @click="submitDownload(version.id)"
                    >
                      {{ version.originalFileName }}
                    </button>
                  </TableCell>
                  <TableCell><StatusBadge :status="version.status" /></TableCell>
                  <TableCell>
                    <div class="grid max-w-72 gap-1 text-xs text-muted-foreground">
                      <span v-if="version.hasNormalizedText">{{ version.sectionCount ?? 0 }} mục</span>
                      <span v-else>—</span>
                      <span v-if="version.language || version.documentType || version.sensitivity">
                        {{ [version.language, version.documentType, version.sensitivity].filter(Boolean).join(' / ') }}
                      </span>
                      <span v-if="version.documentSummary">{{ version.documentSummary }}</span>
                      <span v-if="version.keyTopics.length">Chủ đề: {{ version.keyTopics.join(', ') }}</span>
                      <span v-if="version.entities.length">Thực thể: {{ version.entities.join(', ') }}</span>
                      <span v-if="version.processingWarnings.length" class="text-warning">
                        {{ version.processingWarnings.join(', ') }}
                      </span>
                      <span v-if="version.qualityWarnings.length" class="text-warning">
                        {{ version.qualityWarnings.join(', ') }}
                      </span>
                    </div>
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>

    <Dialog v-model:open="isUploadDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Tải tài liệu lên</DialogTitle>
          <DialogDescription>Tài liệu mới sẽ ở trạng thái chờ duyệt cho đến khi reviewer phê duyệt.</DialogDescription>
        </DialogHeader>
        <form id="upload-document-form" class="grid gap-4" @submit.prevent="submitUpload">
          <div class="grid gap-1.5">
            <Label for="upload-folder">Thư mục</Label>
            <FolderSelect v-model="uploadForm.folderId" trigger-id="upload-folder" :folders="flatFolders" />
          </div>
          <div class="grid gap-1.5">
            <Label for="upload-title">Tên tài liệu</Label>
            <Input id="upload-title" v-model="uploadForm.title" type="text" required />
          </div>
          <div class="grid gap-1.5">
            <Label for="upload-description">Mô tả</Label>
            <Input id="upload-description" v-model="uploadForm.description" type="text" />
          </div>
          <div class="grid gap-1.5">
            <Label>File</Label>
            <FileUpload v-model="uploadFile" :accept="ACCEPTED_FILES" />
          </div>
          <p v-if="uploadError" class="text-sm text-destructive" role="alert">{{ uploadError }}</p>
        </form>
        <DialogFooter>
          <Button variant="outline" type="button" :disabled="isSaving" @click="isUploadDialogOpen = false">Hủy</Button>
          <Button type="submit" form="upload-document-form" :disabled="isSaving || !uploadFile || !uploadForm.folderId">
            {{ isSaving ? 'Đang tải lên...' : 'Tải lên' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
