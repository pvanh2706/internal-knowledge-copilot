<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import type { Application, ApplicationStatus, ApplicationType } from '../../api/applications'
import { createApplication, deleteApplication, getApplications, updateApplication } from '../../api/applications'
import { ApiError } from '../../api/http'
import type { Tenant } from '../../api/tenants'
import { getTenants } from '../../api/tenants'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const tenants = ref<Tenant[]>([])
const applications = ref<Application[]>([])
const selectedApplication = ref<Application | null>(null)
const tenantFilter = ref('')
const isLoading = ref(false)
const isSaving = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

const form = ref({
  tenantId: '',
  code: '',
  name: '',
  applicationType: 'Crm' as ApplicationType,
  baseUrl: '',
  status: 'Active' as ApplicationStatus,
})

const visibleApplications = computed(() => applications.value)

async function loadData() {
  if (!authStore.accessToken) return
  errorMessage.value = ''
  isLoading.value = true

  try {
    tenants.value = await getTenants(authStore.accessToken)
    applications.value = await getApplications(authStore.accessToken, tenantFilter.value || undefined)
    if (!form.value.tenantId && tenants.value.length > 0) {
      form.value.tenantId = tenants.value[0].id
    }
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot load applications.')
  } finally {
    isLoading.value = false
  }
}

async function submitApplication() {
  if (!authStore.accessToken || isSaving.value) return
  errorMessage.value = ''
  successMessage.value = ''
  isSaving.value = true

  try {
    if (selectedApplication.value) {
      selectedApplication.value = await updateApplication(
        selectedApplication.value.id,
        {
          name: form.value.name,
          applicationType: form.value.applicationType,
          baseUrl: form.value.baseUrl || null,
          status: form.value.status,
        },
        authStore.accessToken,
      )
      successMessage.value = 'Application updated.'
    } else {
      selectedApplication.value = await createApplication(
        {
          tenantId: form.value.tenantId,
          code: form.value.code,
          name: form.value.name,
          applicationType: form.value.applicationType,
          baseUrl: form.value.baseUrl || null,
          status: form.value.status,
        },
        authStore.accessToken,
      )
      successMessage.value = 'Application created.'
    }

    await loadData()
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot save application.')
  } finally {
    isSaving.value = false
  }
}

async function archiveApplication(application: Application) {
  if (!authStore.accessToken || isSaving.value) return
  errorMessage.value = ''
  successMessage.value = ''
  isSaving.value = true

  try {
    await deleteApplication(application.id, authStore.accessToken)
    successMessage.value = 'Application archived.'
    if (selectedApplication.value?.id === application.id) resetForm()
    await loadData()
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot archive application.')
  } finally {
    isSaving.value = false
  }
}

function selectApplication(application: Application) {
  selectedApplication.value = application
  form.value = {
    tenantId: application.tenantId,
    code: application.code,
    name: application.name,
    applicationType: application.applicationType,
    baseUrl: application.baseUrl ?? '',
    status: application.status,
  }
}

function resetForm() {
  selectedApplication.value = null
  form.value = {
    tenantId: tenantFilter.value || tenants.value[0]?.id || '',
    code: '',
    name: '',
    applicationType: 'Crm',
    baseUrl: '',
    status: 'Active',
  }
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
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
        <h2>Application management</h2>
        <p>Register internal CRM, sales, support, and platform applications.</p>
      </div>
      <button class="login-link" type="button" :disabled="isLoading" @click="loadData">Refresh</button>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <div class="toolbar-row">
      <label>
        Tenant filter
        <select v-model="tenantFilter" @change="loadData">
          <option value="">All tenants</option>
          <option v-for="tenant in tenants" :key="tenant.id" :value="tenant.id">{{ tenant.code }}</option>
        </select>
      </label>
    </div>

    <form class="management-form" @submit.prevent="submitApplication">
      <select v-model="form.tenantId" :disabled="Boolean(selectedApplication)" required>
        <option value="" disabled>Tenant</option>
        <option v-for="tenant in tenants" :key="tenant.id" :value="tenant.id">{{ tenant.code }}</option>
      </select>
      <input v-model="form.code" type="text" placeholder="Application code" :disabled="Boolean(selectedApplication)" required />
      <input v-model="form.name" type="text" placeholder="Application name" required />
      <select v-model="form.applicationType">
        <option value="Internal">Internal</option>
        <option value="Crm">CRM</option>
        <option value="Sales">Sales</option>
        <option value="Support">Support</option>
        <option value="Other">Other</option>
      </select>
      <input v-model="form.baseUrl" type="url" placeholder="Base URL" />
      <select v-model="form.status">
        <option value="Active">Active</option>
        <option value="Disabled">Disabled</option>
        <option value="Archived">Archived</option>
      </select>
      <button type="submit" :disabled="isSaving">{{ selectedApplication ? 'Update app' : 'Create app' }}</button>
      <button class="login-link" type="button" :disabled="isSaving" @click="resetForm">New</button>
    </form>

    <table class="data-table">
      <thead>
        <tr>
          <th>Tenant</th>
          <th>Code</th>
          <th>Name</th>
          <th>Type</th>
          <th>Status</th>
          <th>Updated</th>
          <th>Action</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="application in visibleApplications" :key="application.id" class="clickable-row" @click="selectApplication(application)">
          <td>{{ application.tenantCode }}</td>
          <td>{{ application.code }}</td>
          <td>{{ application.name }}</td>
          <td>{{ application.applicationType }}</td>
          <td><span class="status-pill">{{ application.status }}</span></td>
          <td>{{ formatDate(application.updatedAt) }}</td>
          <td>
            <button class="text-button" type="button" :disabled="isSaving" @click.stop="archiveApplication(application)">
              Archive
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
