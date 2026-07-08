<script setup lang="ts">
import { computed, h, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import type { ColumnDef } from '@tanstack/vue-table'
import { LightbulbIcon, RefreshCwIcon } from '@lucide/vue'
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
import CitationCard from '@/components/app/CitationCard.vue'
import DataTable from '@/components/app/DataTable.vue'
import EmptyState from '@/components/app/EmptyState.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import FilterBar from '@/components/app/FilterBar.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import type { Application } from '@/api/applications'
import { getApplications } from '@/api/applications'
import { createActionRequest } from '@/api/actionApprovals'
import { ApiError } from '@/api/http'
import type { AiRecommendationFeedbackValue, WorkflowRecommendation } from '@/api/workflowCopilot'
import { getWorkflowRecommendations, submitRecommendationFeedback } from '@/api/workflowCopilot'
import { useAuthStore } from '@/stores/auth'
import { useToast } from '@/composables/useToast'
import { formatDateTime } from '@/lib/format'

const route = useRoute()
const authStore = useAuthStore()
const toast = useToast()

const applications = ref<Application[]>([])
const recommendations = ref<WorkflowRecommendation[]>([])
const selectedRecommendation = ref<WorkflowRecommendation | null>(null)
const isLoading = ref(false)
const isSaving = ref(false)
const errorMessage = ref('')

const filters = ref({
  // Sentinel 'all' vì SelectItem của reka-ui không nhận value rỗng
  applicationId: stringFromQuery(route.query.applicationId) || 'all',
  objectType: stringFromQuery(route.query.objectType) || 'deal',
  externalObjectId: stringFromQuery(route.query.externalObjectId),
})

const feedbackForm = ref({
  value: 'Helpful' as AiRecommendationFeedbackValue,
  note: '',
})

const actionForm = ref({
  actionType: 'create_task',
  approvalMode: 'Manual' as 'Manual' | 'Rule',
  payloadJson: '{\n  "title": "Follow up with customer",\n  "priority": "normal"\n}',
  idempotencyKey: '',
})

const canLoadAll = computed(() => authStore.isAdmin || authStore.isReviewer)

const columns: ColumnDef<WorkflowRecommendation>[] = [
  {
    accessorKey: 'objectType',
    header: 'Đối tượng',
    cell: ({ row }) =>
      h('span', { class: 'font-mono text-xs' }, `${row.original.objectType} / ${row.original.externalObjectId}`),
  },
  { accessorKey: 'title', header: 'Tiêu đề' },
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

async function loadRecommendations() {
  if (!authStore.accessToken) return
  errorMessage.value = ''

  const applicationId = filters.value.applicationId === 'all' ? '' : filters.value.applicationId
  if (!canLoadAll.value && (!applicationId || !filters.value.externalObjectId)) {
    errorMessage.value = 'Nhập application id và object id để tải danh sách gợi ý.'
    return
  }

  isLoading.value = true
  try {
    recommendations.value = await getWorkflowRecommendations(authStore.accessToken, {
      applicationId: applicationId || undefined,
      objectType: filters.value.externalObjectId ? filters.value.objectType : undefined,
      externalObjectId: filters.value.externalObjectId || undefined,
    })
    selectedRecommendation.value = recommendations.value[0] ?? null
    if (selectedRecommendation.value) {
      syncFeedbackForm(selectedRecommendation.value)
    }
  } catch (error) {
    errorMessage.value = toMessage(error, 'Không thể tải danh sách gợi ý.')
  } finally {
    isLoading.value = false
  }
}

async function submitFeedback() {
  if (!authStore.accessToken || !selectedRecommendation.value || isSaving.value) return
  isSaving.value = true

  try {
    selectedRecommendation.value = await submitRecommendationFeedback(
      selectedRecommendation.value.id,
      {
        value: feedbackForm.value.value,
        note: feedbackForm.value.note || null,
      },
      authStore.accessToken,
    )
    recommendations.value = recommendations.value.map((item) =>
      item.id === selectedRecommendation.value?.id ? selectedRecommendation.value : item,
    )
    toast.success('Đã lưu phản hồi.')
  } catch (error) {
    toast.error(toMessage(error, 'Không thể lưu phản hồi.'))
  } finally {
    isSaving.value = false
  }
}

async function submitActionRequest() {
  if (!authStore.accessToken || !selectedRecommendation.value || isSaving.value) return
  isSaving.value = true

  try {
    await createActionRequest(
      selectedRecommendation.value.id,
      {
        actionType: actionForm.value.actionType,
        targetObjectType: selectedRecommendation.value.objectType,
        targetExternalObjectId: selectedRecommendation.value.externalObjectId,
        payloadJson: actionForm.value.payloadJson,
        approvalMode: actionForm.value.approvalMode,
        idempotencyKey: actionForm.value.idempotencyKey || null,
      },
      authStore.accessToken,
    )
    toast.success('Đã tạo yêu cầu hành động.')
    actionForm.value.idempotencyKey = ''
  } catch (error) {
    toast.error(toMessage(error, 'Không thể tạo yêu cầu hành động.'))
  } finally {
    isSaving.value = false
  }
}

function selectRecommendation(recommendation: WorkflowRecommendation) {
  selectedRecommendation.value = recommendation
  syncFeedbackForm(recommendation)
}

function syncFeedbackForm(recommendation: WorkflowRecommendation) {
  feedbackForm.value.value = recommendation.feedbackValue ?? 'Helpful'
  feedbackForm.value.note = recommendation.feedbackNote ?? ''
}

function stringFromQuery(value: unknown) {
  return typeof value === 'string' ? value : ''
}

function toMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback
}

const detailLists = computed(() => {
  if (!selectedRecommendation.value) return []
  return [
    { title: 'Bước tiếp theo', items: selectedRecommendation.value.recommendedNextSteps },
    { title: 'Rủi ro', items: selectedRecommendation.value.risks },
    { title: 'Câu hỏi làm rõ', items: selectedRecommendation.value.clarificationQuestions },
    { title: 'Tín hiệu thắng/thua', items: selectedRecommendation.value.wonLostSignals },
  ].filter((section) => section.items.length > 0)
})

onMounted(async () => {
  await loadApplications()
  if (canLoadAll.value || filters.value.applicationId !== 'all' || filters.value.externalObjectId) {
    await loadRecommendations()
  }
})
</script>

<template>
  <div>
    <PageHeader
      title="Gợi ý quy trình"
      description="Xem gợi ý CRM có căn cứ nguồn, gửi phản hồi và tạo hành động tiếp theo."
    >
      <template #actions>
        <Button variant="outline" :disabled="isLoading" @click="loadRecommendations">
          <RefreshCwIcon class="size-4" />
          Làm mới
        </Button>
      </template>
    </PageHeader>

    <ErrorState v-if="errorMessage" :message="errorMessage" class="mb-4" @retry="loadRecommendations" />

    <form @submit.prevent="loadRecommendations">
      <FilterBar>
        <div class="grid gap-1.5">
          <Label for="rec-application">Application</Label>
          <Select v-if="canLoadAll" v-model="filters.applicationId">
            <SelectTrigger id="rec-application" class="w-64">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tất cả application</SelectItem>
              <SelectItem v-for="application in applications" :key="application.id" :value="application.id">
                {{ application.tenantCode }} / {{ application.code }}
              </SelectItem>
            </SelectContent>
          </Select>
          <Input v-else id="rec-application" v-model="filters.applicationId" type="text" class="w-64" placeholder="Application id" />
        </div>
        <div class="grid gap-1.5">
          <Label for="rec-object-type">Loại đối tượng</Label>
          <Input id="rec-object-type" v-model="filters.objectType" type="text" class="w-36" />
        </div>
        <div class="grid gap-1.5">
          <Label for="rec-object-id">Mã đối tượng ngoài</Label>
          <Input id="rec-object-id" v-model="filters.externalObjectId" type="text" class="w-48" />
        </div>
        <Button type="submit" variant="outline" :disabled="isLoading">Tải gợi ý</Button>
      </FilterBar>
    </form>

    <div class="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.9fr)]">
      <DataTable
        :columns="columns"
        :data="recommendations"
        :is-loading="isLoading"
        clickable-rows
        empty-title="Chưa có gợi ý nào"
        empty-description="Gợi ý xuất hiện khi hệ thống nguồn phát sinh sự kiện nghiệp vụ."
        @row-click="selectRecommendation"
      />

      <EmptyState
        v-if="!selectedRecommendation"
        :icon="LightbulbIcon"
        title="Chọn một gợi ý để xem chi tiết"
      />

      <Card v-else>
        <CardHeader>
          <CardTitle class="text-lg">{{ selectedRecommendation.title }}</CardTitle>
          <CardDescription>{{ selectedRecommendation.summary }}</CardDescription>
        </CardHeader>
        <CardContent class="grid gap-5">
          <p class="text-sm font-medium">{{ selectedRecommendation.reasoningLabel }}</p>

          <div class="grid gap-4 sm:grid-cols-2">
            <div v-for="section in detailLists" :key="section.title" class="grid content-start gap-1">
              <p class="text-sm font-medium">{{ section.title }}</p>
              <ul class="grid list-disc gap-0.5 pl-5 text-sm text-muted-foreground">
                <li v-for="item in section.items" :key="item">{{ item }}</li>
              </ul>
            </div>
          </div>

          <div v-if="selectedRecommendation.sources.length" class="grid gap-2">
            <p class="text-sm font-medium">Nguồn tham khảo</p>
            <CitationCard
              v-for="source in selectedRecommendation.sources"
              :key="`${source.sourceId}-${source.rank}`"
              :index="source.rank"
              :source-type="source.sourceType"
              :title="source.title"
              :folder-path="source.folderPath"
              :section-title="source.sectionTitle"
              :excerpt="source.excerpt"
            />
          </div>

          <Separator />

          <form class="grid gap-3" @submit.prevent="submitFeedback">
            <p class="text-sm font-medium">Phản hồi</p>
            <div class="grid gap-1.5">
              <Label for="rec-feedback-value">Đánh giá</Label>
              <Select v-model="feedbackForm.value">
                <SelectTrigger id="rec-feedback-value" class="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Helpful">Hữu ích</SelectItem>
                  <SelectItem value="NotHelpful">Không hữu ích</SelectItem>
                  <SelectItem value="NeedsReview">Cần xem lại</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div class="grid gap-1.5">
              <Label for="rec-feedback-note">Ghi chú</Label>
              <Textarea id="rec-feedback-note" v-model="feedbackForm.note" rows="2" placeholder="Ghi chú phản hồi..." />
            </div>
            <Button type="submit" size="sm" class="justify-self-start" :disabled="isSaving">Lưu phản hồi</Button>
          </form>

          <Separator />

          <form class="grid gap-3" @submit.prevent="submitActionRequest">
            <p class="text-sm font-medium">Tạo yêu cầu hành động</p>
            <div class="grid gap-1.5">
              <Label for="rec-action-type">Loại hành động</Label>
              <Input id="rec-action-type" v-model="actionForm.actionType" type="text" required />
            </div>
            <div class="grid gap-1.5">
              <Label for="rec-approval-mode">Chế độ phê duyệt</Label>
              <Select v-model="actionForm.approvalMode">
                <SelectTrigger id="rec-approval-mode" class="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Manual">Phê duyệt thủ công</SelectItem>
                  <SelectItem value="Rule">Phê duyệt theo rule</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div class="grid gap-1.5">
              <Label for="rec-payload">Payload JSON</Label>
              <Textarea id="rec-payload" v-model="actionForm.payloadJson" rows="6" required class="font-mono text-xs" />
            </div>
            <div class="grid gap-1.5">
              <Label for="rec-idempotency">Idempotency key (tùy chọn)</Label>
              <Input id="rec-idempotency" v-model="actionForm.idempotencyKey" type="text" />
            </div>
            <Button type="submit" size="sm" class="justify-self-start" :disabled="isSaving">
              Tạo yêu cầu hành động
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  </div>
</template>
