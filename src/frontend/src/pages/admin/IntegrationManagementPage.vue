<script setup lang="ts">
import { onMounted, ref } from 'vue'
import type { Application } from '../../api/applications'
import { getApplications } from '../../api/applications'
import { ApiError } from '../../api/http'
import type { IntegrationAuthMode, IntegrationConnection, IntegrationConnectionStatus } from '../../api/integrations'
import { createIntegrationConnection, getIntegrationConnections } from '../../api/integrations'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const applications = ref<Application[]>([])
const connections = ref<IntegrationConnection[]>([])
const selectedApplicationId = ref('')
const isLoading = ref(false)
const isSaving = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

const form = ref({
  applicationId: '',
  name: '',
  baseUrl: '',
  authMode: 'InternalApiKey' as IntegrationAuthMode,
  secretReference: '',
  secretValue: '',
  status: 'Active' as IntegrationConnectionStatus,
  metadataJson: '',
})

async function loadData() {
  if (!authStore.accessToken) return
  errorMessage.value = ''
  isLoading.value = true

  try {
    applications.value = await getApplications(authStore.accessToken)
    if (!form.value.applicationId && applications.value.length > 0) {
      form.value.applicationId = applications.value[0].id
    }
    connections.value = await getIntegrationConnections(authStore.accessToken, selectedApplicationId.value || undefined)
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot load integration settings.')
  } finally {
    isLoading.value = false
  }
}

async function submitConnection() {
  if (!authStore.accessToken || isSaving.value) return
  errorMessage.value = ''
  successMessage.value = ''
  isSaving.value = true

  try {
    await createIntegrationConnection(
      {
        applicationId: form.value.applicationId,
        name: form.value.name,
        baseUrl: form.value.baseUrl,
        authMode: form.value.authMode,
        secretReference: form.value.secretReference,
        secretValue: form.value.secretValue || null,
        status: form.value.status,
        metadataJson: form.value.metadataJson || null,
      },
      authStore.accessToken,
    )
    successMessage.value = 'Integration connection created.'
    form.value.name = ''
    form.value.baseUrl = ''
    form.value.secretReference = ''
    form.value.secretValue = ''
    form.value.metadataJson = ''
    await loadData()
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot create integration connection.')
  } finally {
    isSaving.value = false
  }
}

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleString() : '-'
}

function toMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback
}

onMounted(loadData)
</script>

<template>
  <section class="panel management-page">
    <div class="section-header">
      <div>
        <h2>Integration settings</h2>
        <p>Configure connector endpoints used for context, permission checks, validation, and execution.</p>
      </div>
      <button class="login-link" type="button" :disabled="isLoading" @click="loadData">Refresh</button>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <form class="management-form" @submit.prevent="submitConnection">
      <select v-model="form.applicationId" required>
        <option value="" disabled>Application</option>
        <option v-for="application in applications" :key="application.id" :value="application.id">
          {{ application.tenantCode }} / {{ application.code }}
        </option>
      </select>
      <input v-model="form.name" type="text" placeholder="Connection name" required />
      <input v-model="form.baseUrl" type="url" placeholder="Base URL" required />
      <select v-model="form.authMode">
        <option value="InternalApiKey">Internal API key</option>
        <option value="None">None</option>
      </select>
      <input v-model="form.secretReference" type="text" placeholder="Secret key id" required />
      <input v-model="form.secretValue" type="password" placeholder="Secret value" />
      <select v-model="form.status">
        <option value="Active">Active</option>
        <option value="Disabled">Disabled</option>
        <option value="Archived">Archived</option>
      </select>
      <input v-model="form.metadataJson" type="text" placeholder='Metadata JSON, e.g. {"owner":"sales"}' />
      <button type="submit" :disabled="isSaving">Create connection</button>
    </form>

    <div class="toolbar-row">
      <label>
        Application filter
        <select v-model="selectedApplicationId" @change="loadData">
          <option value="">All applications</option>
          <option v-for="application in applications" :key="application.id" :value="application.id">
            {{ application.tenantCode }} / {{ application.code }}
          </option>
        </select>
      </label>
    </div>

    <table class="data-table">
      <thead>
        <tr>
          <th>Application</th>
          <th>Name</th>
          <th>Base URL</th>
          <th>Auth</th>
          <th>Status</th>
          <th>Secret</th>
          <th>Rotated</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="connection in connections" :key="connection.id">
          <td>{{ connection.applicationCode }}</td>
          <td>{{ connection.name }}</td>
          <td>{{ connection.baseUrl }}</td>
          <td>{{ connection.authMode }}</td>
          <td><span class="status-pill">{{ connection.status }}</span></td>
          <td>{{ connection.secretReference }} / {{ connection.secretConfigured ? 'configured' : 'missing' }}</td>
          <td>{{ formatDate(connection.secretRotatedAt) }}</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
