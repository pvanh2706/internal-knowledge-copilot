<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ApiError } from '../../api/http'
import type { Tenant, TenantStatus } from '../../api/tenants'
import { createTenant, deleteTenant, getTenants, updateTenant } from '../../api/tenants'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const tenants = ref<Tenant[]>([])
const selectedTenant = ref<Tenant | null>(null)
const isLoading = ref(false)
const isSaving = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

const form = ref({
  code: '',
  name: '',
  status: 'Active' as TenantStatus,
})

async function loadTenants() {
  if (!authStore.accessToken) return
  errorMessage.value = ''
  isLoading.value = true

  try {
    tenants.value = await getTenants(authStore.accessToken)
    if (selectedTenant.value) {
      selectedTenant.value = tenants.value.find((tenant) => tenant.id === selectedTenant.value?.id) ?? null
    }
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot load tenants.')
  } finally {
    isLoading.value = false
  }
}

async function submitTenant() {
  if (!authStore.accessToken || isSaving.value) return
  errorMessage.value = ''
  successMessage.value = ''
  isSaving.value = true

  try {
    if (selectedTenant.value) {
      selectedTenant.value = await updateTenant(
        selectedTenant.value.id,
        {
          name: form.value.name,
          status: form.value.status,
        },
        authStore.accessToken,
      )
      successMessage.value = 'Tenant updated.'
    } else {
      selectedTenant.value = await createTenant(
        {
          code: form.value.code,
          name: form.value.name,
          status: form.value.status,
        },
        authStore.accessToken,
      )
      successMessage.value = 'Tenant created.'
    }

    await loadTenants()
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot save tenant.')
  } finally {
    isSaving.value = false
  }
}

async function archiveTenant(tenant: Tenant) {
  if (!authStore.accessToken || isSaving.value) return
  errorMessage.value = ''
  successMessage.value = ''
  isSaving.value = true

  try {
    await deleteTenant(tenant.id, authStore.accessToken)
    successMessage.value = 'Tenant archived.'
    if (selectedTenant.value?.id === tenant.id) resetForm()
    await loadTenants()
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot archive tenant.')
  } finally {
    isSaving.value = false
  }
}

function selectTenant(tenant: Tenant) {
  selectedTenant.value = tenant
  form.value = {
    code: tenant.code,
    name: tenant.name,
    status: tenant.status,
  }
}

function resetForm() {
  selectedTenant.value = null
  form.value = {
    code: '',
    name: '',
    status: 'Active',
  }
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}

function toMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback
}

onMounted(loadTenants)
</script>

<template>
  <section class="panel management-page">
    <div class="section-header">
      <div>
        <h2>Tenant management</h2>
        <p>Manage tenant records used by the shared Copilot platform.</p>
      </div>
      <button class="login-link" type="button" :disabled="isLoading" @click="loadTenants">Refresh</button>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <form class="management-form" @submit.prevent="submitTenant">
      <input v-model="form.code" type="text" placeholder="Tenant code" :disabled="Boolean(selectedTenant)" required />
      <input v-model="form.name" type="text" placeholder="Tenant name" required />
      <select v-model="form.status">
        <option value="Active">Active</option>
        <option value="Suspended">Suspended</option>
        <option value="Archived">Archived</option>
      </select>
      <button type="submit" :disabled="isSaving">{{ selectedTenant ? 'Update tenant' : 'Create tenant' }}</button>
      <button class="login-link" type="button" :disabled="isSaving" @click="resetForm">New</button>
    </form>

    <table class="data-table">
      <thead>
        <tr>
          <th>Code</th>
          <th>Name</th>
          <th>Status</th>
          <th>Updated</th>
          <th>Action</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="tenant in tenants" :key="tenant.id" class="clickable-row" @click="selectTenant(tenant)">
          <td>{{ tenant.code }}</td>
          <td>{{ tenant.name }}</td>
          <td><span class="status-pill">{{ tenant.status }}</span></td>
          <td>{{ formatDate(tenant.updatedAt) }}</td>
          <td>
            <button class="text-button" type="button" :disabled="isSaving" @click.stop="archiveTenant(tenant)">
              Archive
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
