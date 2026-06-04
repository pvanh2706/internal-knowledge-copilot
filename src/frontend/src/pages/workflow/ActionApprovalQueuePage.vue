<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import type { Application } from '../../api/applications'
import { getApplications } from '../../api/applications'
import type { AiActionRequest, AiActionRequestStatus } from '../../api/actionApprovals'
import { approveActionRequest, executeActionRequest, getActionRequests, rejectActionRequest } from '../../api/actionApprovals'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const route = useRoute()
const authStore = useAuthStore()
const applications = ref<Application[]>([])
const actions = ref<AiActionRequest[]>([])
const selectedAction = ref<AiActionRequest | null>(null)
const isLoading = ref(false)
const isSaving = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

const filters = ref({
  applicationId: stringFromQuery(route.query.applicationId),
  status: stringFromQuery(route.query.status) as AiActionRequestStatus | '',
  recommendationId: stringFromQuery(route.query.recommendationId),
  objectType: stringFromQuery(route.query.objectType) || 'deal',
  externalObjectId: stringFromQuery(route.query.externalObjectId),
})

const approveNote = ref('')
const rejectReason = ref('')
const canLoadAll = computed(() => authStore.isAdmin || authStore.isReviewer)

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
  successMessage.value = ''

  if (!canLoadAll.value && !filters.value.recommendationId && (!filters.value.applicationId || !filters.value.externalObjectId)) {
    errorMessage.value = 'Enter recommendation id or application/object scope to load actions.'
    return
  }

  isLoading.value = true
  try {
    actions.value = await getActionRequests(authStore.accessToken, {
      applicationId: filters.value.applicationId || undefined,
      status: filters.value.status || undefined,
      recommendationId: filters.value.recommendationId || undefined,
      objectType: filters.value.externalObjectId ? filters.value.objectType : undefined,
      externalObjectId: filters.value.externalObjectId || undefined,
    })
    selectedAction.value = actions.value[0] ?? null
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot load action queue.')
  } finally {
    isLoading.value = false
  }
}

async function approveSelected() {
  if (!authStore.accessToken || !selectedAction.value || isSaving.value) return
  await mutateAction(() => approveActionRequest(selectedAction.value!.id, approveNote.value || null, authStore.accessToken!))
  approveNote.value = ''
}

async function rejectSelected() {
  if (!authStore.accessToken || !selectedAction.value || isSaving.value) return
  if (!rejectReason.value.trim()) {
    errorMessage.value = 'Reject reason is required.'
    return
  }

  await mutateAction(() => rejectActionRequest(selectedAction.value!.id, rejectReason.value, authStore.accessToken!))
  rejectReason.value = ''
}

async function executeSelected() {
  if (!authStore.accessToken || !selectedAction.value || isSaving.value) return
  await mutateAction(() => executeActionRequest(selectedAction.value!.id, authStore.accessToken!))
}

async function mutateAction(operation: () => Promise<AiActionRequest>) {
  errorMessage.value = ''
  successMessage.value = ''
  isSaving.value = true

  try {
    const updated = await operation()
    selectedAction.value = updated
    actions.value = actions.value.map((item) => (item.id === updated.id ? updated : item))
    successMessage.value = 'Action updated.'
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot update action.')
  } finally {
    isSaving.value = false
  }
}

function selectAction(action: AiActionRequest) {
  selectedAction.value = action
  rejectReason.value = action.rejectionReason ?? ''
}

function canApprove(action: AiActionRequest | null) {
  return Boolean(action && ['Draft', 'PendingApproval', 'Failed'].includes(action.status))
}

function canReject(action: AiActionRequest | null) {
  return Boolean(action && ['Draft', 'PendingApproval', 'Approved', 'Failed'].includes(action.status))
}

function canExecute(action: AiActionRequest | null) {
  return Boolean(action && ['Approved', 'Failed', 'Succeeded'].includes(action.status))
}

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleString() : '-'
}

function stringFromQuery(value: unknown) {
  return typeof value === 'string' ? value : ''
}

function toMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback
}

onMounted(async () => {
  await loadApplications()
  if (canLoadAll.value || filters.value.recommendationId || filters.value.applicationId || filters.value.externalObjectId) {
    await loadActions()
  }
})
</script>

<template>
  <section class="panel management-page">
    <div class="section-header">
      <div>
        <h2>Action approval queue</h2>
        <p>Approve, reject, and execute AI-proposed source-system actions.</p>
      </div>
      <button class="login-link" type="button" :disabled="isLoading" @click="loadActions">Refresh</button>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <form class="management-form" @submit.prevent="loadActions">
      <select v-if="canLoadAll" v-model="filters.applicationId">
        <option value="">All applications</option>
        <option v-for="application in applications" :key="application.id" :value="application.id">
          {{ application.tenantCode }} / {{ application.code }}
        </option>
      </select>
      <input v-else v-model="filters.applicationId" type="text" placeholder="Application id" />
      <select v-model="filters.status">
        <option value="">Any status</option>
        <option value="Draft">Draft</option>
        <option value="PendingApproval">Pending approval</option>
        <option value="Approved">Approved</option>
        <option value="Rejected">Rejected</option>
        <option value="Executing">Executing</option>
        <option value="Succeeded">Succeeded</option>
        <option value="Failed">Failed</option>
        <option value="Cancelled">Cancelled</option>
      </select>
      <input v-model="filters.recommendationId" type="text" placeholder="Recommendation id" />
      <input v-model="filters.objectType" type="text" placeholder="Object type" />
      <input v-model="filters.externalObjectId" type="text" placeholder="External object id" />
      <button type="submit" :disabled="isLoading">Load queue</button>
    </form>

    <div class="split-layout">
      <section class="answer-panel">
        <h3>Actions</h3>
        <table class="data-table">
          <thead>
            <tr>
              <th>Target</th>
              <th>Action</th>
              <th>Status</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="action in actions" :key="action.id" class="clickable-row" @click="selectAction(action)">
              <td>{{ action.targetObjectType }} / {{ action.targetExternalObjectId }}</td>
              <td>{{ action.actionType }}</td>
              <td><span class="status-pill">{{ action.status }}</span></td>
              <td>{{ formatDate(action.createdAt) }}</td>
            </tr>
          </tbody>
        </table>
      </section>

      <section v-if="selectedAction" class="answer-panel">
        <h3>{{ selectedAction.actionType }}</h3>
        <p><strong>Status:</strong> {{ selectedAction.status }}</p>
        <p><strong>Target:</strong> {{ selectedAction.targetObjectType }} / {{ selectedAction.targetExternalObjectId }}</p>
        <p><strong>Approval:</strong> {{ selectedAction.approvalMode }}</p>
        <p><strong>Idempotency:</strong> {{ selectedAction.idempotencyKey }}</p>
        <p><strong>External execution:</strong> {{ selectedAction.externalExecutionId ?? '-' }}</p>
        <p v-if="selectedAction.executionError" class="form-error">{{ selectedAction.executionError }}</p>

        <div class="detail-grid">
          <div>
            <h4>Payload</h4>
            <pre>{{ selectedAction.normalizedPayloadJson ?? selectedAction.payloadJson }}</pre>
          </div>
          <div>
            <h4>Validation</h4>
            <pre>{{ selectedAction.validationResultJson ?? '-' }}</pre>
          </div>
          <div>
            <h4>Rule decision</h4>
            <pre>{{ selectedAction.ruleDecisionJson ?? '-' }}</pre>
          </div>
          <div>
            <h4>Execution result</h4>
            <pre>{{ selectedAction.executionResultJson ?? '-' }}</pre>
          </div>
        </div>

        <div class="stack-form">
          <textarea v-model="approveNote" rows="2" placeholder="Approval note"></textarea>
          <button type="button" :disabled="isSaving || !canApprove(selectedAction)" @click="approveSelected">
            Approve
          </button>
        </div>

        <div class="stack-form">
          <textarea v-model="rejectReason" rows="2" placeholder="Reject reason"></textarea>
          <button type="button" :disabled="isSaving || !canReject(selectedAction)" @click="rejectSelected">
            Reject
          </button>
        </div>

        <div class="button-row">
          <button type="button" :disabled="isSaving || !canExecute(selectedAction)" @click="executeSelected">
            Execute
          </button>
        </div>
      </section>
    </div>
  </section>
</template>
