<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import type { Application } from '../../api/applications'
import { getApplications } from '../../api/applications'
import { createActionRequest } from '../../api/actionApprovals'
import { ApiError } from '../../api/http'
import type { AiRecommendationFeedbackValue, WorkflowRecommendation } from '../../api/workflowCopilot'
import { getWorkflowRecommendations, submitRecommendationFeedback } from '../../api/workflowCopilot'
import { useAuthStore } from '../../stores/auth'

const route = useRoute()
const authStore = useAuthStore()
const applications = ref<Application[]>([])
const recommendations = ref<WorkflowRecommendation[]>([])
const selectedRecommendation = ref<WorkflowRecommendation | null>(null)
const isLoading = ref(false)
const isSaving = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

const filters = ref({
  applicationId: stringFromQuery(route.query.applicationId),
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
  successMessage.value = ''

  if (!canLoadAll.value && (!filters.value.applicationId || !filters.value.externalObjectId)) {
    errorMessage.value = 'Enter application id and object id to load recommendations.'
    return
  }

  isLoading.value = true
  try {
    recommendations.value = await getWorkflowRecommendations(authStore.accessToken, {
      applicationId: filters.value.applicationId || undefined,
      objectType: filters.value.externalObjectId ? filters.value.objectType : undefined,
      externalObjectId: filters.value.externalObjectId || undefined,
    })
    selectedRecommendation.value = recommendations.value[0] ?? null
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot load recommendations.')
  } finally {
    isLoading.value = false
  }
}

async function submitFeedback() {
  if (!authStore.accessToken || !selectedRecommendation.value || isSaving.value) return
  errorMessage.value = ''
  successMessage.value = ''
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
    successMessage.value = 'Feedback saved.'
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot save feedback.')
  } finally {
    isSaving.value = false
  }
}

async function submitActionRequest() {
  if (!authStore.accessToken || !selectedRecommendation.value || isSaving.value) return
  errorMessage.value = ''
  successMessage.value = ''
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
    successMessage.value = 'Action request created.'
    actionForm.value.idempotencyKey = ''
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot create action request.')
  } finally {
    isSaving.value = false
  }
}

function selectRecommendation(recommendation: WorkflowRecommendation) {
  selectedRecommendation.value = recommendation
  feedbackForm.value.value = recommendation.feedbackValue ?? 'Helpful'
  feedbackForm.value.note = recommendation.feedbackNote ?? ''
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}

function stringFromQuery(value: unknown) {
  return typeof value === 'string' ? value : ''
}

function toMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback
}

onMounted(async () => {
  await loadApplications()
  if (canLoadAll.value || filters.value.applicationId || filters.value.externalObjectId) {
    await loadRecommendations()
  }
})
</script>

<template>
  <section class="panel management-page">
    <div class="section-header">
      <div>
        <h2>Workflow recommendations</h2>
        <p>Review grounded CRM recommendations, citations, feedback, and proposed next actions.</p>
      </div>
      <button class="login-link" type="button" :disabled="isLoading" @click="loadRecommendations">Refresh</button>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <form class="management-form" @submit.prevent="loadRecommendations">
      <select v-if="canLoadAll" v-model="filters.applicationId">
        <option value="">All applications</option>
        <option v-for="application in applications" :key="application.id" :value="application.id">
          {{ application.tenantCode }} / {{ application.code }}
        </option>
      </select>
      <input v-else v-model="filters.applicationId" type="text" placeholder="Application id" required />
      <input v-model="filters.objectType" type="text" placeholder="Object type" />
      <input v-model="filters.externalObjectId" type="text" placeholder="External object id" />
      <button type="submit" :disabled="isLoading">Load recommendations</button>
    </form>

    <div class="split-layout">
      <section class="answer-panel">
        <h3>History</h3>
        <table class="data-table">
          <thead>
            <tr>
              <th>Object</th>
              <th>Title</th>
              <th>Status</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="recommendation in recommendations"
              :key="recommendation.id"
              class="clickable-row"
              @click="selectRecommendation(recommendation)"
            >
              <td>{{ recommendation.objectType }} / {{ recommendation.externalObjectId }}</td>
              <td>{{ recommendation.title }}</td>
              <td><span class="status-pill">{{ recommendation.status }}</span></td>
              <td>{{ formatDate(recommendation.createdAt) }}</td>
            </tr>
          </tbody>
        </table>
      </section>

      <section v-if="selectedRecommendation" class="answer-panel">
        <h3>{{ selectedRecommendation.title }}</h3>
        <p>{{ selectedRecommendation.summary }}</p>
        <p><strong>{{ selectedRecommendation.reasoningLabel }}</strong></p>

        <div class="detail-grid">
          <div>
            <h4>Next steps</h4>
            <ul>
              <li v-for="step in selectedRecommendation.recommendedNextSteps" :key="step">{{ step }}</li>
            </ul>
          </div>
          <div>
            <h4>Risks</h4>
            <ul>
              <li v-for="risk in selectedRecommendation.risks" :key="risk">{{ risk }}</li>
            </ul>
          </div>
          <div>
            <h4>Questions</h4>
            <ul>
              <li v-for="question in selectedRecommendation.clarificationQuestions" :key="question">{{ question }}</li>
            </ul>
          </div>
          <div>
            <h4>Won/lost signals</h4>
            <ul>
              <li v-for="signal in selectedRecommendation.wonLostSignals" :key="signal">{{ signal }}</li>
            </ul>
          </div>
        </div>

        <section>
          <h4>Citations</h4>
          <div v-for="source in selectedRecommendation.sources" :key="`${source.sourceId}-${source.rank}`" class="citation-item">
            <strong>{{ source.rank }}. {{ source.title }}</strong>
            <small>{{ source.sourceType }} / {{ source.folderPath }} / {{ source.sectionTitle ?? '-' }}</small>
            <p>{{ source.excerpt }}</p>
          </div>
        </section>

        <form class="stack-form" @submit.prevent="submitFeedback">
          <h4>Feedback</h4>
          <select v-model="feedbackForm.value">
            <option value="Helpful">Helpful</option>
            <option value="NotHelpful">Not helpful</option>
            <option value="NeedsReview">Needs review</option>
          </select>
          <textarea v-model="feedbackForm.note" rows="2" placeholder="Feedback note"></textarea>
          <button type="submit" :disabled="isSaving">Save feedback</button>
        </form>

        <form class="stack-form" @submit.prevent="submitActionRequest">
          <h4>Create action request</h4>
          <input v-model="actionForm.actionType" type="text" placeholder="Action type" required />
          <select v-model="actionForm.approvalMode">
            <option value="Manual">Manual approval</option>
            <option value="Rule">Rule approval</option>
          </select>
          <textarea v-model="actionForm.payloadJson" rows="6" required></textarea>
          <input v-model="actionForm.idempotencyKey" type="text" placeholder="Optional idempotency key" />
          <button type="submit" :disabled="isSaving">Create action request</button>
        </form>
      </section>
    </div>
  </section>
</template>
