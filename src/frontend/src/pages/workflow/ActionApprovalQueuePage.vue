<script setup lang="ts">
import { computed, h, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import type { ColumnDef } from '@tanstack/vue-table'
import { CheckIcon, ListChecksIcon, PlayIcon, RefreshCwIcon, XIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
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
import FilterBar from '@/components/app/FilterBar.vue'
import JsonViewer from '@/components/app/JsonViewer.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import type { Application } from '@/api/applications'
import { getApplications } from '@/api/applications'
import type { AiActionRequest, AiActionRequestStatus } from '@/api/actionApprovals'
import { approveActionRequest, executeActionRequest, getActionRequests, rejectActionRequest } from '@/api/actionApprovals'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useToast } from '@/composables/useToast'
import { formatDateTime } from '@/lib/format'

const route = useRoute()
const authStore = useAuthStore()
const toast = useToast()
const { confirm } = useConfirm()

const applications = ref<Application[]>([])
const actions = ref<AiActionRequest[]>([])
const selectedAction = ref<AiActionRequest | null>(null)
const isLoading = ref(false)
const isSaving = ref(false)
const errorMessage = ref('')

const statusLabels: Record<AiActionRequestStatus, string> = {
  Draft: 'Bản nháp',
  PendingApproval: 'Chờ phê duyệt',
  Approved: 'Đã duyệt',
  Rejected: 'Từ chối',
  Executing: 'Đang thực thi',
  Succeeded: 'Thành công',
  Failed: 'Thất bại',
  Cancelled: 'Đã hủy',
}

const filters = ref({
  // Sentinel 'all' vì SelectItem của reka-ui không nhận value rỗng
  applicationId: stringFromQuery(route.query.applicationId) || 'all',
  status: (stringFromQuery(route.query.status) || 'all') as AiActionRequestStatus | 'all',
  recommendationId: stringFromQuery(route.query.recommendationId),
  objectType: stringFromQuery(route.query.objectType) || 'deal',
  externalObjectId: stringFromQuery(route.query.externalObjectId),
})

const approveNote = ref('')
const rejectReason = ref('')
const canLoadAll = computed(() => authStore.isAdmin || authStore.isReviewer)

const columns: ColumnDef<AiActionRequest>[] = [
  {
    accessorKey: 'targetObjectType',
    header: 'Đối tượng',
    cell: ({ row }) =>
      h(
        'span',
        { class: 'font-mono text-xs' },
        `${row.original.targetObjectType} / ${row.original.targetExternalObjectId}`,
      ),
  },
  { accessorKey: 'actionType', header: 'Hành động' },
  {
    accessorKey: 'status',
    header: 'Trạng thái',
    cell: ({ row }) => h(StatusBadge, { status: row.original.status }),
  },
  {
    accessorKey: 'createdAt',
    header: 'Tạo lúc',
    cell: ({ row }) => formatDateTime(row.original.createdAt),
  },
]

async function loadApplications() {
  if (!authStore.accessToken || !canLoadAll.value) return
  try {
    applications.value = await getApplications(authStore.accessToken)
  } catch {
    applications.value = []
  }
}

async function loadActions() {
  if (!authStore.accessToken) return
  errorMessage.value = ''

  const applicationId = filters.value.applicationId === 'all' ? '' : filters.value.applicationId
  if (!canLoadAll.value && !filters.value.recommendationId && (!applicationId || !filters.value.externalObjectId)) {
    errorMessage.value = 'Nhập recommendation id hoặc cặp application/object để tải hàng đợi.'
    return
  }

  isLoading.value = true
  try {
    actions.value = await getActionRequests(authStore.accessToken, {
      applicationId: applicationId || undefined,
      status: filters.value.status === 'all' ? undefined : filters.value.status,
      recommendationId: filters.value.recommendationId || undefined,
      objectType: filters.value.externalObjectId ? filters.value.objectType : undefined,
      externalObjectId: filters.value.externalObjectId || undefined,
    })
    selectedAction.value = actions.value[0] ?? null
    if (selectedAction.value) {
      rejectReason.value = selectedAction.value.rejectionReason ?? ''
    }
  } catch (error) {
    errorMessage.value = toMessage(error, 'Không thể tải hàng đợi hành động.')
  } finally {
    isLoading.value = false
  }
}

async function approveSelected() {
  if (!authStore.accessToken || !selectedAction.value || isSaving.value) return
  await mutateAction(
    () => approveActionRequest(selectedAction.value!.id, approveNote.value || null, authStore.accessToken!),
    'Đã phê duyệt hành động.',
  )
  approveNote.value = ''
}

async function rejectSelected() {
  if (!authStore.accessToken || !selectedAction.value || isSaving.value) return
  if (!rejectReason.value.trim()) {
    toast.error('Cần nhập lý do từ chối.')
    return
  }

  const confirmed = await confirm({
    title: `Từ chối hành động "${selectedAction.value.actionType}"?`,
    description: 'Hành động bị từ chối sẽ không được thực thi lên hệ thống nguồn.',
    confirmText: 'Từ chối',
    destructive: true,
  })
  if (!confirmed) return

  await mutateAction(
    () => rejectActionRequest(selectedAction.value!.id, rejectReason.value, authStore.accessToken!),
    'Đã từ chối hành động.',
  )
  rejectReason.value = ''
}

async function executeSelected() {
  if (!authStore.accessToken || !selectedAction.value || isSaving.value) return

  const confirmed = await confirm({
    title: `Thực thi hành động "${selectedAction.value.actionType}"?`,
    description: 'Hành động sẽ được gửi sang hệ thống nguồn và có thể thay đổi dữ liệu thật.',
    confirmText: 'Thực thi',
  })
  if (!confirmed) return

  await mutateAction(
    () => executeActionRequest(selectedAction.value!.id, authStore.accessToken!),
    'Đã gửi thực thi hành động.',
  )
}

async function mutateAction(operation: () => Promise<AiActionRequest>, successMessage: string) {
  isSaving.value = true

  try {
    const updated = await operation()
    selectedAction.value = updated
    actions.value = actions.value.map((item) => (item.id === updated.id ? updated : item))
    toast.success(successMessage)
  } catch (error) {
    toast.error(toMessage(error, 'Không thể cập nhật hành động.'))
  } finally {
    isSaving.value = false
  }
}

function selectAction(action: AiActionRequest) {
  selectedAction.value = action
  rejectReason.value = action.rejectionReason ?? ''
}

// Action bar theo state machine 8 trạng thái của AiActionRequestStatus
function canApprove(action: AiActionRequest | null) {
  return Boolean(action && ['Draft', 'PendingApproval', 'Failed'].includes(action.status))
}

function canReject(action: AiActionRequest | null) {
  return Boolean(action && ['Draft', 'PendingApproval', 'Approved', 'Failed'].includes(action.status))
}

function canExecute(action: AiActionRequest | null) {
  return Boolean(action && ['Approved', 'Failed', 'Succeeded'].includes(action.status))
}

function stringFromQuery(value: unknown) {
  return typeof value === 'string' ? value : ''
}

function toMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback
}

onMounted(async () => {
  await loadApplications()
  if (
    canLoadAll.value ||
    filters.value.recommendationId ||
    filters.value.applicationId !== 'all' ||
    filters.value.externalObjectId
  ) {
    await loadActions()
  }
})
</script>

<template>
  <div>
    <PageHeader
      title="Hàng đợi hành động"
      description="Phê duyệt, từ chối và thực thi các hành động AI đề xuất lên hệ thống nguồn."
    >
      <template #actions>
        <Button variant="outline" :disabled="isLoading" @click="loadActions">
          <RefreshCwIcon class="size-4" />
          Làm mới
        </Button>
      </template>
    </PageHeader>

    <ErrorState v-if="errorMessage" :message="errorMessage" class="mb-4" @retry="loadActions" />

    <form @submit.prevent="loadActions">
      <FilterBar>
        <div class="grid gap-1.5">
          <Label for="action-application">Application</Label>
          <Select v-if="canLoadAll" v-model="filters.applicationId">
            <SelectTrigger id="action-application" class="w-56">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tất cả application</SelectItem>
              <SelectItem v-for="application in applications" :key="application.id" :value="application.id">
                {{ application.tenantCode }} / {{ application.code }}
              </SelectItem>
            </SelectContent>
          </Select>
          <Input v-else id="action-application" v-model="filters.applicationId" type="text" class="w-56" placeholder="Application id" />
        </div>
        <div class="grid gap-1.5">
          <Label for="action-status">Trạng thái</Label>
          <Select v-model="filters.status">
            <SelectTrigger id="action-status" class="w-44">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Mọi trạng thái</SelectItem>
              <SelectItem v-for="(label, status) in statusLabels" :key="status" :value="status">
                {{ label }}
              </SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div class="grid gap-1.5">
          <Label for="action-recommendation">Recommendation id</Label>
          <Input id="action-recommendation" v-model="filters.recommendationId" type="text" class="w-48" />
        </div>
        <div class="grid gap-1.5">
          <Label for="action-object-type">Loại đối tượng</Label>
          <Input id="action-object-type" v-model="filters.objectType" type="text" class="w-32" />
        </div>
        <div class="grid gap-1.5">
          <Label for="action-object-id">Mã đối tượng ngoài</Label>
          <Input id="action-object-id" v-model="filters.externalObjectId" type="text" class="w-44" />
        </div>
        <Button type="submit" variant="outline" :disabled="isLoading">Tải hàng đợi</Button>
      </FilterBar>
    </form>

    <div class="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.9fr)]">
      <DataTable
        :columns="columns"
        :data="actions"
        :is-loading="isLoading"
        clickable-rows
        empty-title="Hàng đợi trống"
        empty-description="Yêu cầu hành động được tạo từ trang Gợi ý quy trình."
        @row-click="selectAction"
      />

      <EmptyState
        v-if="!selectedAction"
        :icon="ListChecksIcon"
        title="Chọn một hành động để xem chi tiết"
      />

      <Card v-else>
        <CardHeader>
          <CardTitle class="flex flex-wrap items-center gap-2 text-lg">
            {{ selectedAction.actionType }}
            <StatusBadge :status="selectedAction.status" />
          </CardTitle>
          <CardDescription class="font-mono text-xs">
            {{ selectedAction.targetObjectType }} / {{ selectedAction.targetExternalObjectId }}
          </CardDescription>
        </CardHeader>
        <CardContent class="grid gap-5">
          <dl class="grid gap-1.5 text-sm">
            <div class="flex gap-2">
              <dt class="w-36 shrink-0 text-muted-foreground">Chế độ phê duyệt</dt>
              <dd>{{ selectedAction.approvalMode === 'Manual' ? 'Thủ công' : 'Theo rule' }}</dd>
            </div>
            <div class="flex gap-2">
              <dt class="w-36 shrink-0 text-muted-foreground">Idempotency</dt>
              <dd class="font-mono text-xs">{{ selectedAction.idempotencyKey || '—' }}</dd>
            </div>
            <div class="flex gap-2">
              <dt class="w-36 shrink-0 text-muted-foreground">Mã thực thi ngoài</dt>
              <dd class="font-mono text-xs">{{ selectedAction.externalExecutionId ?? '—' }}</dd>
            </div>
          </dl>

          <ErrorState
            v-if="selectedAction.executionError"
            :message="selectedAction.executionError"
            hide-retry
          />

          <div class="grid gap-4 sm:grid-cols-2">
            <JsonViewer title="Payload" :value="selectedAction.normalizedPayloadJson ?? selectedAction.payloadJson" />
            <JsonViewer title="Kết quả kiểm tra" :value="selectedAction.validationResultJson" />
            <JsonViewer title="Quyết định rule" :value="selectedAction.ruleDecisionJson" />
            <JsonViewer title="Kết quả thực thi" :value="selectedAction.executionResultJson" />
          </div>

          <Separator />

          <div class="grid gap-3">
            <div class="grid gap-1.5">
              <Label for="approve-note">Ghi chú phê duyệt</Label>
              <Textarea id="approve-note" v-model="approveNote" rows="2" placeholder="Ghi chú khi phê duyệt (tùy chọn)..." />
            </div>
            <div class="grid gap-1.5">
              <Label for="reject-reason">Lý do từ chối</Label>
              <Textarea id="reject-reason" v-model="rejectReason" rows="2" placeholder="Bắt buộc khi từ chối..." />
            </div>
            <div class="flex flex-wrap gap-2">
              <Button size="sm" type="button" :disabled="isSaving || !canApprove(selectedAction)" @click="approveSelected">
                <CheckIcon class="size-4" />
                Phê duyệt
              </Button>
              <Button
                size="sm"
                variant="destructive"
                type="button"
                :disabled="isSaving || !canReject(selectedAction)"
                @click="rejectSelected"
              >
                <XIcon class="size-4" />
                Từ chối
              </Button>
              <Button
                size="sm"
                variant="outline"
                type="button"
                :disabled="isSaving || !canExecute(selectedAction)"
                @click="executeSelected"
              >
                <PlayIcon class="size-4" />
                Thực thi
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  </div>
</template>
