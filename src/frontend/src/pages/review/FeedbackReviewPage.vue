<script setup lang="ts">
import { onMounted, ref } from 'vue'
import type { FeedbackItem, FeedbackReviewStatus } from '../../api/feedback'
import { getIncorrectFeedback, updateFeedbackReviewStatus } from '../../api/feedback'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const feedbackItems = ref<FeedbackItem[]>([])
const selectedFeedback = ref<FeedbackItem | null>(null)
const errorMessage = ref('')
const successMessage = ref('')
const reviewForm = ref({
  status: 'InReview' as FeedbackReviewStatus,
  reviewerNote: '',
})

async function loadData() {
  if (!authStore.accessToken) return
  feedbackItems.value = await getIncorrectFeedback(authStore.accessToken)
  if (selectedFeedback.value) {
    selectedFeedback.value = feedbackItems.value.find((item) => item.id === selectedFeedback.value?.id) ?? null
  }
}

function selectFeedback(item: FeedbackItem) {
  selectedFeedback.value = item
  reviewForm.value.status = item.reviewStatus
  reviewForm.value.reviewerNote = item.reviewerNote ?? ''
  errorMessage.value = ''
  successMessage.value = ''
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
    successMessage.value = 'Da cap nhat trang thai feedback.'
    await loadData()
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Khong the cap nhat feedback.'
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
        <p>{{ selectedFeedback.note || 'Khong co ghi chu tu user.' }}</p>

        <section class="answer-panel">
          <h4>Câu trả lời AI</h4>
          <pre>{{ selectedFeedback.answer }}</pre>
        </section>

        <section class="answer-panel">
          <h4>Nguồn đã dùng</h4>
          <article v-for="source in selectedFeedback.sources" :key="`${source.rank}-${source.title}`" class="citation-item">
            <strong>{{ source.sourceType }} - {{ source.title }}</strong>
            <small>{{ source.folderPath || '-' }}</small>
            <p>{{ source.excerpt }}</p>
          </article>
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
