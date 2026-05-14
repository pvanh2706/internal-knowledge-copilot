<script setup lang="ts">
import { onMounted, ref } from 'vue'
import type { FeedbackItem, FeedbackReviewStatus, QualityIssue } from '../../api/feedback'
import { approveCorrection, createCorrection, getIncorrectFeedback, getQualityIssues, rejectCorrection, updateFeedbackReviewStatus } from '../../api/feedback'
import { createEvaluationCaseFromFeedback } from '../../api/evaluation'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'
import type { VisibilityScope } from '../../api/wiki'

const authStore = useAuthStore()
const feedbackItems = ref<FeedbackItem[]>([])
const qualityIssues = ref<QualityIssue[]>([])
const selectedFeedback = ref<FeedbackItem | null>(null)
const selectedIssue = ref<QualityIssue | null>(null)
const errorMessage = ref('')
const successMessage = ref('')
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

async function loadData() {
  if (!authStore.accessToken) return
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
  errorMessage.value = ''
  successMessage.value = ''
}

function findIssue(feedbackId: string) {
  return qualityIssues.value.find((issue) => issue.feedbackId === feedbackId) ?? null
}

async function submitReviewStatus() {
  if (!authStore.accessToken || !selectedFeedback.value) return
  errorMessage.value = ''
  successMessage.value = ''

  try {
    await updateFeedbackReviewStatus(
      selectedFeedback.value.id,
      {
        status: reviewForm.value.status,
        reviewerNote: reviewForm.value.reviewerNote,
      },
      authStore.accessToken,
    )
    successMessage.value = 'Đã cập nhật trạng thái feedback.'
    await loadData()
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể cập nhật feedback.'
  }
}

async function submitCorrection() {
  if (!authStore.accessToken || !selectedIssue.value) return
  errorMessage.value = ''
  successMessage.value = ''

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
    successMessage.value = 'Đã tạo correction draft.'
    correctionForm.value.correctionText = ''
    await loadData()
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tạo correction.'
  }
}

async function submitApproveCorrection(correctionId: string) {
  if (!authStore.accessToken) return
  errorMessage.value = ''
  successMessage.value = ''

  try {
    await approveCorrection(correctionId, authStore.accessToken)
    successMessage.value = 'Đã approve và index correction.'
    await loadData()
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể approve correction.'
  }
}

async function submitRejectCorrection(correctionId: string) {
  if (!authStore.accessToken) return
  errorMessage.value = ''
  successMessage.value = ''

  try {
    await rejectCorrection(correctionId, correctionForm.value.rejectReason || 'Bị loại bởi reviewer.', authStore.accessToken)
    successMessage.value = 'Đã reject correction.'
    await loadData()
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể reject correction.'
  }
}

function splitKeywords(value: string) {
  return value
    .split(/[\n,]/)
    .map((keyword) => keyword.trim())
    .filter(Boolean)
}

async function submitEvaluationCase() {
  if (!authStore.accessToken || !selectedFeedback.value) return
  errorMessage.value = ''
  successMessage.value = ''

  try {
    await createEvaluationCaseFromFeedback(
      selectedFeedback.value.id,
      {
        expectedAnswer: evaluationForm.value.expectedAnswer,
        expectedKeywords: splitKeywords(evaluationForm.value.expectedKeywords),
      },
      authStore.accessToken,
    )
    successMessage.value = 'Đã tạo eval case từ feedback.'
    evaluationForm.value.expectedKeywords = ''
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tạo eval case.'
  }
}

onMounted(loadData)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Feedback sai</h2>
      <p>Xem các phản hồi Incorrect từ người dùng và cập nhật trạng thái xử lý.</p>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <div class="split-layout">
      <table class="data-table">
        <thead>
          <tr>
            <th>Người gửi</th>
            <th>Câu hỏi</th>
            <th>Trạng thái</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in feedbackItems" :key="item.id" class="clickable-row" @click="selectFeedback(item)">
            <td>{{ item.userDisplayName }}</td>
            <td>{{ item.question }}</td>
            <td>{{ item.reviewStatus }}</td>
          </tr>
        </tbody>
      </table>

      <div v-if="selectedFeedback" class="folder-detail">
        <h3>{{ selectedFeedback.question }}</h3>
        <p>{{ selectedFeedback.note || 'Không có ghi chú từ user.' }}</p>

        <section class="answer-panel">
          <h4>Câu trả lời AI</h4>
          <pre>{{ selectedFeedback.answer }}</pre>
        </section>

        <section class="answer-panel">
          <h4>Nguồn đã dùng</h4>
          <article v-for="source in selectedFeedback.sources" :key="`${source.rank}-${source.title}`" class="citation-item">
            <strong>{{ source.sourceType }} - {{ source.title }}</strong>
            <small>{{ [source.folderPath, source.sectionTitle].filter(Boolean).join(' / ') || '-' }}</small>
            <p>{{ source.excerpt }}</p>
          </article>
        </section>

        <section v-if="selectedIssue" class="answer-panel">
          <h4>Vấn đề chất lượng</h4>
          <p>{{ selectedIssue.status }} - {{ selectedIssue.failureType || 'Chưa phân loại' }} - {{ selectedIssue.severity || '-' }}</p>
          <p v-if="selectedIssue.rootCauseHypothesis">{{ selectedIssue.rootCauseHypothesis }}</p>
          <div v-if="selectedIssue.recommendedActions.length">
            <strong>Hành động đề xuất</strong>
            <ul>
              <li v-for="action in selectedIssue.recommendedActions" :key="action">{{ action }}</li>
            </ul>
          </div>

          <article v-for="correction in selectedIssue.corrections" :key="correction.id" class="citation-item">
            <strong>Correction - {{ correction.status }}</strong>
            <small>{{ correction.visibilityScope }}{{ correction.approvedAt ? ` - approved ${correction.approvedAt}` : '' }}</small>
            <p>{{ correction.correctionText }}</p>
            <div v-if="correction.status === 'Draft'" class="button-row">
              <button type="button" @click="submitApproveCorrection(correction.id)">Approve + index</button>
              <button type="button" class="danger-button" @click="submitRejectCorrection(correction.id)">Reject</button>
            </div>
          </article>

          <form class="stack-form" @submit.prevent="submitCorrection">
            <label>
              Correction
              <textarea v-model="correctionForm.correctionText" rows="5" placeholder="Nhập correction đúng để hệ thống ưu tiên dùng lần sau..." required />
            </label>
            <label>
              Phạm vi correction
              <select v-model="correctionForm.visibilityScope">
                <option value="Folder">Theo folder nguồn</option>
                <option value="Company">Toàn công ty</option>
              </select>
            </label>
            <label v-if="correctionForm.visibilityScope === 'Company'" class="checkbox-line">
              <input v-model="correctionForm.isCompanyPublicConfirmed" type="checkbox" />
              Xác nhận correction được phép public nội bộ
            </label>
            <label>
              Lý do reject correction
              <textarea v-model="correctionForm.rejectReason" rows="2" placeholder="Dùng khi bấm Reject correction..." />
            </label>
            <button type="submit">Tạo correction draft</button>
          </form>
        </section>

        <section class="answer-panel">
          <h4>Evaluation case</h4>
          <form class="stack-form" @submit.prevent="submitEvaluationCase">
            <label>
              Đáp án kỳ vọng
              <textarea v-model="evaluationForm.expectedAnswer" rows="4" placeholder="Nhập đáp án đúng dùng để chấm eval..." required />
            </label>
            <label>
              Từ khóa kỳ vọng
              <textarea v-model="evaluationForm.expectedKeywords" rows="2" placeholder="Mỗi keyword một dòng hoặc cách nhau bằng dấu phẩy..." />
            </label>
            <button type="submit">Tạo eval case</button>
          </form>
        </section>

        <form class="stack-form" @submit.prevent="submitReviewStatus">
          <label>
            Trạng thái xử lý
            <select v-model="reviewForm.status">
              <option value="New">New</option>
              <option value="InReview">InReview</option>
              <option value="Resolved">Resolved</option>
            </select>
          </label>
          <label>
            Ghi chú reviewer
            <textarea v-model="reviewForm.reviewerNote" rows="4" placeholder="Ghi chú xử lý..." />
          </label>
          <button type="submit">Cập nhật</button>
        </form>
      </div>
    </div>
  </section>
</template>
