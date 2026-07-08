<script setup lang="ts">
import { h, onMounted, ref } from 'vue'
import type { ColumnDef } from '@tanstack/vue-table'
import { MessageSquareIcon } from '@lucide/vue'
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
import CitationCard from '@/components/app/CitationCard.vue'
import CorrectionCard from '@/components/app/CorrectionCard.vue'
import DataTable from '@/components/app/DataTable.vue'
import EmptyState from '@/components/app/EmptyState.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import EvalCaseForm from '@/components/app/EvalCaseForm.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import type { FeedbackItem, FeedbackReviewStatus, QualityIssue } from '@/api/feedback'
import {
  approveCorrection,
  createCorrection,
  getIncorrectFeedback,
  getQualityIssues,
  rejectCorrection,
  updateFeedbackReviewStatus,
} from '@/api/feedback'
import { createEvaluationCaseFromFeedback } from '@/api/evaluation'
import { ApiError } from '@/api/http'
import type { VisibilityScope } from '@/api/wiki'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useToast } from '@/composables/useToast'

const authStore = useAuthStore()
const toast = useToast()
const { confirm } = useConfirm()

const feedbackItems = ref<FeedbackItem[]>([])
const qualityIssues = ref<QualityIssue[]>([])
const selectedFeedback = ref<FeedbackItem | null>(null)
const selectedIssue = ref<QualityIssue | null>(null)
const isLoading = ref(false)
const isSaving = ref(false)
const loadError = ref('')

const reviewForm = ref({
  status: 'InReview' as FeedbackReviewStatus,
  reviewerNote: '',
})
const correctionForm = ref({
  correctionText: '',
  visibilityScope: 'Folder' as VisibilityScope,
  isCompanyPublicConfirmed: false,
  rejectReason: '',
})
const evaluationForm = ref({
  expectedAnswer: '',
  expectedKeywords: '',
})

const columns: ColumnDef<FeedbackItem>[] = [
  { accessorKey: 'userDisplayName', header: 'Người gửi' },
  { accessorKey: 'question', header: 'Câu hỏi' },
  {
    accessorKey: 'reviewStatus',
    header: 'Trạng thái',
    cell: ({ row }) => h(StatusBadge, { status: row.original.reviewStatus }),
  },
]

async function loadData() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    const [feedback, issues] = await Promise.all([
      getIncorrectFeedback(authStore.accessToken),
      getQualityIssues(authStore.accessToken),
    ])
    feedbackItems.value = feedback
    qualityIssues.value = issues
    if (selectedFeedback.value) {
      selectedFeedback.value = feedbackItems.value.find((item) => item.id === selectedFeedback.value?.id) ?? null
      selectedIssue.value = selectedFeedback.value ? findIssue(selectedFeedback.value.id) : null
    }
  } catch (error) {
    loadError.value = toMessage(error, 'Không thể tải danh sách phản hồi.')
  } finally {
    isLoading.value = false
  }
}

function selectFeedback(item: FeedbackItem) {
  selectedFeedback.value = item
  selectedIssue.value = findIssue(item.id)
  reviewForm.value.status = item.reviewStatus
  reviewForm.value.reviewerNote = item.reviewerNote ?? ''
  correctionForm.value.correctionText = ''
  correctionForm.value.visibilityScope = 'Folder'
  correctionForm.value.isCompanyPublicConfirmed = false
  correctionForm.value.rejectReason = ''
  evaluationForm.value.expectedAnswer =
    selectedIssue.value?.corrections.find((correction) => correction.status === 'Approved')?.correctionText ?? ''
  evaluationForm.value.expectedKeywords = ''
}

function findIssue(feedbackId: string) {
  return qualityIssues.value.find((issue) => issue.feedbackId === feedbackId) ?? null
}

async function submitReviewStatus() {
  if (!authStore.accessToken || !selectedFeedback.value || isSaving.value) return
  isSaving.value = true

  try {
    await updateFeedbackReviewStatus(
      selectedFeedback.value.id,
      {
        status: reviewForm.value.status,
        reviewerNote: reviewForm.value.reviewerNote,
      },
      authStore.accessToken,
    )
    toast.success('Đã cập nhật trạng thái phản hồi.')
    await loadData()
  } catch (error) {
    toast.error(toMessage(error, 'Không thể cập nhật phản hồi.'))
  } finally {
    isSaving.value = false
  }
}

async function submitCorrection() {
  if (!authStore.accessToken || !selectedIssue.value || isSaving.value) return
  isSaving.value = true

  try {
    await createCorrection(
      selectedIssue.value.id,
      {
        correctionText: correctionForm.value.correctionText,
        visibilityScope: correctionForm.value.visibilityScope,
        folderId: null,
        isCompanyPublicConfirmed: correctionForm.value.isCompanyPublicConfirmed,
      },
      authStore.accessToken,
    )
    toast.success('Đã tạo correction draft.')
    correctionForm.value.correctionText = ''
    await loadData()
  } catch (error) {
    toast.error(toMessage(error, 'Không thể tạo correction.'))
  } finally {
    isSaving.value = false
  }
}

async function submitApproveCorrection(correctionId: string) {
  if (!authStore.accessToken || isSaving.value) return
  isSaving.value = true

  try {
    await approveCorrection(correctionId, authStore.accessToken)
    toast.success('Đã duyệt và lập chỉ mục correction.')
    await loadData()
  } catch (error) {
    toast.error(toMessage(error, 'Không thể duyệt correction.'))
  } finally {
    isSaving.value = false
  }
}

async function submitRejectCorrection(correctionId: string) {
  if (!authStore.accessToken || isSaving.value) return

  const confirmed = await confirm({
    title: 'Từ chối correction này?',
    description: 'Correction bị từ chối sẽ không được dùng để cải thiện câu trả lời AI.',
    confirmText: 'Từ chối',
    destructive: true,
  })
  if (!confirmed) return

  isSaving.value = true
  try {
    await rejectCorrection(correctionId, correctionForm.value.rejectReason || 'Bị loại bởi reviewer.', authStore.accessToken)
    toast.success('Đã từ chối correction.')
    await loadData()
  } catch (error) {
    toast.error(toMessage(error, 'Không thể từ chối correction.'))
  } finally {
    isSaving.value = false
  }
}

function splitKeywords(value: string) {
  return value
    .split(/[\n,]/)
    .map((keyword) => keyword.trim())
    .filter(Boolean)
}

async function submitEvaluationCase() {
  if (!authStore.accessToken || !selectedFeedback.value || isSaving.value) return
  isSaving.value = true

  try {
    await createEvaluationCaseFromFeedback(
      selectedFeedback.value.id,
      {
        expectedAnswer: evaluationForm.value.expectedAnswer,
        expectedKeywords: splitKeywords(evaluationForm.value.expectedKeywords),
      },
      authStore.accessToken,
    )
    toast.success('Đã tạo eval case từ phản hồi.')
    evaluationForm.value.expectedKeywords = ''
  } catch (error) {
    toast.error(toMessage(error, 'Không thể tạo eval case.'))
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
      title="Phản hồi"
      description="Xem các phản hồi sai từ người dùng, tạo correction và eval case để cải thiện chất lượng."
    />

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadData" />

    <div class="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.9fr)]">
      <DataTable
        :columns="columns"
        :data="feedbackItems"
        :is-loading="isLoading"
        clickable-rows
        empty-title="Không có phản hồi sai nào chờ xử lý"
        empty-description="Phản hồi 👎 từ trang Hỏi AI sẽ xuất hiện ở đây."
        @row-click="selectFeedback"
      />

      <EmptyState
        v-if="!selectedFeedback"
        :icon="MessageSquareIcon"
        title="Chọn một phản hồi để xử lý"
        description="Bấm vào dòng trong bảng để xem câu trả lời AI, nguồn và tạo correction."
      />

      <Card v-else>
        <CardHeader>
          <CardTitle class="text-lg">{{ selectedFeedback.question }}</CardTitle>
          <CardDescription>{{ selectedFeedback.note || 'Không có ghi chú từ người dùng.' }}</CardDescription>
        </CardHeader>
        <CardContent class="grid gap-5">
          <div class="grid gap-1.5">
            <p class="text-sm font-medium">Câu trả lời AI</p>
            <p class="max-h-64 overflow-auto whitespace-pre-wrap rounded-md border bg-muted/50 p-3 text-sm leading-6">
              {{ selectedFeedback.answer }}
            </p>
          </div>

          <div v-if="selectedFeedback.sources.length" class="grid gap-2">
            <p class="text-sm font-medium">Nguồn đã dùng</p>
            <CitationCard
              v-for="source in selectedFeedback.sources"
              :key="`${source.rank}-${source.title}`"
              :index="source.rank"
              :source-type="source.sourceType"
              :title="source.title"
              :folder-path="source.folderPath"
              :section-title="source.sectionTitle"
              :excerpt="source.excerpt"
            />
          </div>

          <template v-if="selectedIssue">
            <Separator />

            <div class="grid gap-3">
              <div class="flex flex-wrap items-center gap-2">
                <p class="text-sm font-medium">Vấn đề chất lượng</p>
                <StatusBadge :status="selectedIssue.status" />
                <span class="text-xs text-muted-foreground">
                  {{ selectedIssue.failureType || 'Chưa phân loại' }} · {{ selectedIssue.severity || '—' }}
                </span>
              </div>
              <p v-if="selectedIssue.rootCauseHypothesis" class="text-sm text-muted-foreground">
                {{ selectedIssue.rootCauseHypothesis }}
              </p>
              <div v-if="selectedIssue.recommendedActions.length" class="grid gap-1 text-sm">
                <p class="font-medium">Hành động đề xuất</p>
                <ul class="grid list-disc gap-0.5 pl-5 text-muted-foreground">
                  <li v-for="action in selectedIssue.recommendedActions" :key="action">{{ action }}</li>
                </ul>
              </div>

              <CorrectionCard
                v-for="correction in selectedIssue.corrections"
                :key="correction.id"
                :correction="correction"
                :disabled="isSaving"
                @approve="submitApproveCorrection"
                @reject="submitRejectCorrection"
              />

              <form class="grid gap-3 rounded-lg border p-3" @submit.prevent="submitCorrection">
                <p class="text-sm font-medium">Tạo correction mới</p>
                <div class="grid gap-1.5">
                  <Label for="correction-text">Nội dung correction</Label>
                  <Textarea
                    id="correction-text"
                    v-model="correctionForm.correctionText"
                    rows="5"
                    required
                    placeholder="Nhập correction đúng để hệ thống ưu tiên dùng lần sau..."
                  />
                </div>
                <div class="grid gap-1.5">
                  <Label for="correction-scope">Phạm vi correction</Label>
                  <Select v-model="correctionForm.visibilityScope">
                    <SelectTrigger id="correction-scope" class="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Folder">Theo thư mục nguồn</SelectItem>
                      <SelectItem value="Company">Toàn công ty</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <label
                  v-if="correctionForm.visibilityScope === 'Company'"
                  class="flex cursor-pointer items-start gap-2.5 rounded-md border border-warning/30 bg-warning/10 p-3 text-sm"
                >
                  <Checkbox v-model="correctionForm.isCompanyPublicConfirmed" class="mt-0.5" />
                  Tôi xác nhận correction này được phép công khai cho toàn bộ nhân viên công ty.
                </label>
                <div class="grid gap-1.5">
                  <Label for="correction-reject-reason">Lý do từ chối correction</Label>
                  <Textarea
                    id="correction-reject-reason"
                    v-model="correctionForm.rejectReason"
                    rows="2"
                    placeholder="Dùng khi bấm Từ chối trên correction draft..."
                  />
                </div>
                <Button
                  type="submit"
                  size="sm"
                  class="justify-self-start"
                  :disabled="isSaving || (correctionForm.visibilityScope === 'Company' && !correctionForm.isCompanyPublicConfirmed)"
                >
                  Tạo correction draft
                </Button>
              </form>
            </div>
          </template>

          <Separator />

          <div class="grid gap-2">
            <p class="text-sm font-medium">Tạo eval case từ phản hồi</p>
            <EvalCaseForm
              v-model:expected-answer="evaluationForm.expectedAnswer"
              v-model:expected-keywords="evaluationForm.expectedKeywords"
              :disabled="isSaving"
              @submit="submitEvaluationCase"
            />
          </div>

          <Separator />

          <form class="grid gap-3" @submit.prevent="submitReviewStatus">
            <p class="text-sm font-medium">Trạng thái xử lý</p>
            <div class="grid gap-1.5">
              <Label for="review-status">Trạng thái</Label>
              <Select v-model="reviewForm.status">
                <SelectTrigger id="review-status" class="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="New">Mới</SelectItem>
                  <SelectItem value="InReview">Đang xem xét</SelectItem>
                  <SelectItem value="Resolved">Đã xử lý</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div class="grid gap-1.5">
              <Label for="reviewer-note">Ghi chú reviewer</Label>
              <Textarea id="reviewer-note" v-model="reviewForm.reviewerNote" rows="3" placeholder="Ghi chú xử lý..." />
            </div>
            <Button type="submit" size="sm" class="justify-self-start" :disabled="isSaving">Cập nhật</Button>
          </form>
        </CardContent>
      </Card>
    </div>
  </div>
</template>
