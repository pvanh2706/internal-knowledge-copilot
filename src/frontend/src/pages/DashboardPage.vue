<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import type { DashboardSummary, NamedCount } from '../api/dashboard'
import { getDashboardSummary } from '../api/dashboard'
import { ApiError } from '../api/http'
import { useAuthStore } from '../stores/auth'

const authStore = useAuthStore()
const summary = ref<DashboardSummary | null>(null)
const errorMessage = ref('')

const feedbackTotal = computed(() => (summary.value?.feedbackCorrectCount ?? 0) + (summary.value?.feedbackIncorrectCount ?? 0))
const feedbackCorrectRate = computed(() => {
  if (!summary.value || feedbackTotal.value === 0) return '0%'
  return `${Math.round((summary.value.feedbackCorrectCount / feedbackTotal.value) * 100)}%`
})
const latestEvaluationPassRate = computed(() => {
  if (!summary.value || summary.value.latestEvaluationPassRate == null) return 'Chua chay'
  return `${Math.round(summary.value.latestEvaluationPassRate)}%`
})

async function loadSummary() {
  if (!authStore.accessToken || (!authStore.isReviewer && !authStore.isAdmin)) return
  errorMessage.value = ''

  try {
    summary.value = await getDashboardSummary(authStore.accessToken)
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Khong the tai dashboard.'
  }
}

function countOf(items: NamedCount[] | undefined, name: string) {
  return items?.find((item) => item.name === name)?.count ?? 0
}

onMounted(loadSummary)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Dashboard</h2>
      <p v-if="authStore.isReviewer || authStore.isAdmin">Theo dõi tài liệu, wiki, lượt hỏi AI và feedback.</p>
      <p v-else>Dashboard KPI chỉ dành cho Admin và Reviewer.</p>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>

    <div v-if="summary" class="stat-grid">
      <article class="stat-card">
        <span>Tài liệu approved</span>
        <strong>{{ countOf(summary.documentCounts, 'Approved') }}</strong>
      </article>
      <article class="stat-card">
        <span>Tài liệu chờ duyệt</span>
        <strong>{{ countOf(summary.documentCounts, 'PendingReview') }}</strong>
      </article>
      <article class="stat-card">
        <span>Wiki published</span>
        <strong>{{ countOf(summary.wikiCounts, 'Published') }}</strong>
      </article>
      <article class="stat-card">
        <span>Lượt hỏi AI</span>
        <strong>{{ summary.aiQuestionCount }}</strong>
      </article>
      <article class="stat-card">
        <span>Tỷ lệ feedback đúng</span>
        <strong>{{ feedbackCorrectRate }}</strong>
      </article>
      <article class="stat-card">
        <span>Feedback sai chờ xử lý</span>
        <strong>{{ summary.incorrectFeedbackPendingCount }}</strong>
      </article>
      <article class="stat-card">
        <span>Eval cases active</span>
        <strong>{{ summary.evaluationCaseCount }}</strong>
      </article>
      <article class="stat-card">
        <span>Eval pass rate</span>
        <strong>{{ latestEvaluationPassRate }}</strong>
        <small v-if="summary.latestEvaluationTotalCases">
          {{ summary.latestEvaluationPassedCases }}/{{ summary.latestEvaluationTotalCases }} pass
        </small>
      </article>
    </div>

    <section v-if="summary" class="answer-panel">
      <h3>Nguồn được trích dẫn nhiều</h3>
      <table class="data-table">
        <thead>
          <tr>
            <th>Nguồn</th>
            <th>Folder</th>
            <th>Lượt</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="source in summary.topCitedSources" :key="`${source.sourceType}-${source.title}-${source.folderPath}`">
            <td>{{ source.sourceType }} - {{ source.title }}</td>
            <td>{{ source.folderPath || '-' }}</td>
            <td>{{ source.count }}</td>
          </tr>
        </tbody>
      </table>
    </section>
  </section>
</template>
