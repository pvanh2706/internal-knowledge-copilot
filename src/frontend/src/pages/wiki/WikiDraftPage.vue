<script setup lang="ts">
import { h, onMounted, ref } from 'vue'
import type { ColumnDef } from '@tanstack/vue-table'
import { BookOpenIcon, CheckIcon, XIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Separator } from '@/components/ui/separator'
import { Textarea } from '@/components/ui/textarea'
import DataTable from '@/components/app/DataTable.vue'
import EmptyState from '@/components/app/EmptyState.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import type { VisibilityScope, WikiDraftDetail, WikiDraftListItem } from '@/api/wiki'
import { getWikiDraft, getWikiDrafts, publishWikiDraft, rejectWikiDraft } from '@/api/wiki'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useToast } from '@/composables/useToast'

const authStore = useAuthStore()
const toast = useToast()
const { confirm } = useConfirm()

const drafts = ref<WikiDraftListItem[]>([])
const selectedDraft = ref<WikiDraftDetail | null>(null)
const isLoading = ref(false)
const isSaving = ref(false)
const loadError = ref('')
const rejectReason = ref('')

const publishForm = ref({
  visibilityScope: 'Folder' as VisibilityScope,
  isCompanyPublicConfirmed: false,
})

const columns: ColumnDef<WikiDraftListItem>[] = [
  { accessorKey: 'title', header: 'Tiêu đề' },
  { accessorKey: 'folderPath', header: 'Thư mục' },
  {
    accessorKey: 'status',
    header: 'Trạng thái',
    cell: ({ row }) => h(StatusBadge, { status: row.original.status }),
  },
]

async function loadData() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    drafts.value = await getWikiDrafts(authStore.accessToken)
    if (selectedDraft.value) {
      selectedDraft.value = await getWikiDraft(selectedDraft.value.id, authStore.accessToken).catch(() => null)
    }
  } catch (error) {
    loadError.value = toMessage(error, 'Không thể tải danh sách wiki draft.')
  } finally {
    isLoading.value = false
  }
}

async function selectDraft(draft: WikiDraftListItem) {
  if (!authStore.accessToken) return
  try {
    selectedDraft.value = await getWikiDraft(draft.id, authStore.accessToken)
    publishForm.value = { visibilityScope: 'Folder', isCompanyPublicConfirmed: false }
    rejectReason.value = ''
  } catch (error) {
    toast.error(toMessage(error, 'Không thể tải chi tiết wiki draft.'))
  }
}

async function submitPublish() {
  if (!authStore.accessToken || !selectedDraft.value || isSaving.value) return
  isSaving.value = true

  try {
    await publishWikiDraft(
      selectedDraft.value.id,
      {
        visibilityScope: publishForm.value.visibilityScope,
        folderId: null,
        isCompanyPublicConfirmed: publishForm.value.isCompanyPublicConfirmed,
      },
      authStore.accessToken,
    )
    toast.success('Đã xuất bản wiki và lập chỉ mục vào vector DB.')
    await loadData()
  } catch (error) {
    toast.error(toMessage(error, 'Không thể xuất bản wiki.'))
  } finally {
    isSaving.value = false
  }
}

async function submitReject() {
  if (!authStore.accessToken || !selectedDraft.value || isSaving.value) return

  const confirmed = await confirm({
    title: `Từ chối wiki draft "${selectedDraft.value.title}"?`,
    description: 'Draft bị từ chối sẽ không được đưa vào Q&A; muốn dùng lại phải tạo draft mới.',
    confirmText: 'Từ chối',
    destructive: true,
  })
  if (!confirmed) return

  isSaving.value = true
  try {
    selectedDraft.value = await rejectWikiDraft(selectedDraft.value.id, rejectReason.value, authStore.accessToken)
    toast.success('Đã từ chối wiki draft.')
    await loadData()
  } catch (error) {
    toast.error(toMessage(error, 'Không thể từ chối wiki draft.'))
  } finally {
    isSaving.value = false
  }
}

function toMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback
}

onMounted(loadData)
</script>

<template>
  <div>
    <PageHeader
      title="Wiki"
      description="Reviewer kiểm tra wiki draft, xuất bản hoặc từ chối trước khi đưa vào Q&A."
    />

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadData" />

    <div class="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.9fr)]">
      <DataTable
        :columns="columns"
        :data="drafts"
        :is-loading="isLoading"
        clickable-rows
        empty-title="Chưa có wiki draft nào"
        empty-description="Tạo wiki draft từ trang Tài liệu (tài liệu đã duyệt và lập chỉ mục)."
        @row-click="selectDraft"
      />

      <EmptyState
        v-if="!selectedDraft"
        :icon="BookOpenIcon"
        title="Chọn một wiki draft để xem nội dung"
        description="Bấm vào dòng trong bảng để đọc nội dung và xuất bản hoặc từ chối."
      />

      <Card v-else>
        <CardHeader>
          <CardTitle class="text-lg">{{ selectedDraft.title }}</CardTitle>
          <CardDescription class="flex flex-wrap items-center gap-2">
            <span class="font-mono text-xs">{{ selectedDraft.folderPath }}</span>
            <StatusBadge :status="selectedDraft.status" />
          </CardDescription>
        </CardHeader>
        <CardContent class="grid gap-4">
          <div v-if="selectedDraft.relatedDocuments.length" class="grid gap-2">
            <p class="text-sm font-medium">Tài liệu liên quan</p>
            <div
              v-for="document in selectedDraft.relatedDocuments"
              :key="document.documentId"
              class="grid gap-0.5 rounded-md border p-3 text-sm"
            >
              <p class="font-medium">{{ document.title }}</p>
              <p class="font-mono text-xs text-muted-foreground">{{ document.folderPath || '—' }}</p>
              <p class="text-muted-foreground">{{ document.reason }}</p>
            </div>
          </div>

          <div
            v-if="selectedDraft.missingInformation.length"
            class="grid gap-1 border-l-2 border-warning bg-warning/10 p-3 text-sm"
          >
            <p class="font-medium">Thông tin còn thiếu</p>
            <ul class="grid list-disc gap-0.5 pl-5 text-muted-foreground">
              <li v-for="item in selectedDraft.missingInformation" :key="item">{{ item }}</li>
            </ul>
          </div>

          <div class="max-h-[520px] overflow-auto whitespace-pre-wrap rounded-md border bg-muted/50 p-4 text-base leading-7">{{ selectedDraft.content }}</div>

          <template v-if="selectedDraft.status === 'Draft'">
            <Separator />

            <form class="grid gap-3" @submit.prevent="submitPublish">
              <div class="grid gap-1.5">
                <Label for="publish-scope">Phạm vi xuất bản</Label>
                <Select v-model="publishForm.visibilityScope">
                  <SelectTrigger id="publish-scope" class="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Folder">Theo thư mục nguồn</SelectItem>
                    <SelectItem value="Company">Toàn công ty</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <label
                v-if="publishForm.visibilityScope === 'Company'"
                class="flex cursor-pointer items-start gap-2.5 rounded-md border border-warning/30 bg-warning/10 p-3 text-sm"
              >
                <Checkbox v-model="publishForm.isCompanyPublicConfirmed" class="mt-0.5" />
                Tôi xác nhận nội dung này được phép công khai cho toàn bộ nhân viên công ty.
              </label>
              <Button
                type="submit"
                size="sm"
                class="justify-self-start"
                :disabled="isSaving || (publishForm.visibilityScope === 'Company' && !publishForm.isCompanyPublicConfirmed)"
              >
                <CheckIcon class="size-4" />
                {{ isSaving ? 'Đang xuất bản...' : 'Xuất bản wiki' }}
              </Button>
            </form>

            <Separator />

            <form class="grid gap-3" @submit.prevent="submitReject">
              <div class="grid gap-1.5">
                <Label for="wiki-reject-reason">Lý do từ chối</Label>
                <Textarea id="wiki-reject-reason" v-model="rejectReason" rows="3" placeholder="Nhập lý do từ chối..." />
              </div>
              <Button variant="destructive" size="sm" type="submit" class="justify-self-start" :disabled="isSaving">
                <XIcon class="size-4" />
                Từ chối draft
              </Button>
            </form>
          </template>
        </CardContent>
      </Card>
    </div>
  </div>
</template>
