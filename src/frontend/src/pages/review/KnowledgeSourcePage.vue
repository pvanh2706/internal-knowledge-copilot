<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import type { Application } from '../../api/applications'
import { getApplications } from '../../api/applications'
import { ApiError } from '../../api/http'
import type { ExternalObject, KnowledgeSource } from '../../api/knowledgeSources'
import { getExternalObjects, getKnowledgeSources } from '../../api/knowledgeSources'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const applications = ref<Application[]>([])
const sources = ref<KnowledgeSource[]>([])
const externalObjects = ref<ExternalObject[]>([])
const selectedApplicationId = ref('')
const selectedSourceId = ref('')
const selectedSource = ref<KnowledgeSource | null>(null)
const selectedObject = ref<ExternalObject | null>(null)
const isLoading = ref(false)
const errorMessage = ref('')

const filteredSources = computed(() => sources.value)

async function loadData() {
  if (!authStore.accessToken) return
  errorMessage.value = ''
  isLoading.value = true

  try {
    applications.value = await getApplications(authStore.accessToken)
    sources.value = await getKnowledgeSources(authStore.accessToken, selectedApplicationId.value || undefined)
    externalObjects.value = await getExternalObjects(
      authStore.accessToken,
      selectedApplicationId.value || undefined,
      selectedSourceId.value || undefined,
    )
    if (selectedSource.value) {
      selectedSource.value = sources.value.find((source) => source.id === selectedSource.value?.id) ?? null
    }
    if (selectedObject.value) {
      selectedObject.value = externalObjects.value.find((item) => item.id === selectedObject.value?.id) ?? null
    }
  } catch (error) {
    errorMessage.value = toMessage(error, 'Cannot load knowledge sources.')
  } finally {
    isLoading.value = false
  }
}

function selectSource(source: KnowledgeSource) {
  selectedSource.value = source
  selectedSourceId.value = source.id
  selectedObject.value = null
  void loadData()
}

function clearSourceFilter() {
  selectedSource.value = null
  selectedSourceId.value = ''
  void loadData()
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
        <h2>Knowledge sources</h2>
        <p>Inspect source sync state and external business objects available to retrieval.</p>
      </div>
      <button class="login-link" type="button" :disabled="isLoading" @click="loadData">Refresh</button>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>

    <div class="toolbar-row">
      <label>
        Application
        <select v-model="selectedApplicationId" @change="loadData">
          <option value="">All applications</option>
          <option v-for="application in applications" :key="application.id" :value="application.id">
            {{ application.tenantCode }} / {{ application.code }}
          </option>
        </select>
      </label>
      <button class="login-link" type="button" :disabled="!selectedSourceId" @click="clearSourceFilter">Clear source</button>
    </div>

    <div class="split-layout">
      <section class="answer-panel">
        <h3>Sources</h3>
        <table class="data-table">
          <thead>
            <tr>
              <th>Application</th>
              <th>Name</th>
              <th>Type</th>
              <th>Status</th>
              <th>Last sync</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="source in filteredSources"
              :key="source.id"
              class="clickable-row"
              @click="selectSource(source)"
            >
              <td>{{ source.applicationCode }}</td>
              <td>{{ source.name }}</td>
              <td>{{ source.sourceType }} / {{ source.syncMode }}</td>
              <td><span class="status-pill">{{ source.status }}</span></td>
              <td>{{ formatDate(source.lastSyncCompletedAt) }}</td>
            </tr>
          </tbody>
        </table>
      </section>

      <section class="answer-panel">
        <h3>External objects</h3>
        <table class="data-table">
          <thead>
            <tr>
              <th>Object</th>
              <th>Title</th>
              <th>Status</th>
              <th>ACL sync</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="item in externalObjects"
              :key="item.id"
              class="clickable-row"
              @click="selectedObject = item"
            >
              <td>{{ item.objectType }} / {{ item.externalObjectId }}</td>
              <td>{{ item.title }}</td>
              <td><span class="status-pill">{{ item.status }}</span></td>
              <td>{{ formatDate(item.aclSyncedAt) }}</td>
            </tr>
          </tbody>
        </table>
      </section>
    </div>

    <section v-if="selectedSource || selectedObject" class="answer-panel">
      <h3>Detail</h3>
      <div v-if="selectedSource" class="detail-grid">
        <p><strong>Source:</strong> {{ selectedSource.name }}</p>
        <p><strong>External id:</strong> {{ selectedSource.externalSourceId }}</p>
        <p><strong>Sync status:</strong> {{ selectedSource.lastSyncStatus ?? '-' }}</p>
        <p><strong>Sync error:</strong> {{ selectedSource.lastSyncError ?? '-' }}</p>
        <pre v-if="selectedSource.metadataJson">{{ selectedSource.metadataJson }}</pre>
      </div>
      <div v-if="selectedObject" class="detail-grid">
        <p><strong>Object:</strong> {{ selectedObject.objectType }} / {{ selectedObject.externalObjectId }}</p>
        <p><strong>URL:</strong> {{ selectedObject.url ?? '-' }}</p>
        <p><strong>Content hash:</strong> {{ selectedObject.contentHash ?? '-' }}</p>
        <p><strong>ACL hash:</strong> {{ selectedObject.aclHash ?? '-' }}</p>
        <pre v-if="selectedObject.metadataJson">{{ selectedObject.metadataJson }}</pre>
      </div>
    </section>
  </section>
</template>
