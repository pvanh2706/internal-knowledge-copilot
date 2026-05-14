<script setup lang="ts">
import { onMounted, ref } from 'vue'
import type { DocumentDetail, DocumentListItem } from '../../api/documents'
import { approveDocument, getDocument, getDocuments, rejectDocument } from '../../api/documents'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const documents = ref<DocumentListItem[]>([])
const selectedDocument = ref<DocumentDetail | null>(null)
const rejectReason = ref('')
const errorMessage = ref('')
const successMessage = ref('')

async function loadQueue() {
  if (!authStore.accessToken) return
  documents.value = await getDocuments(authStore.accessToken, { status: 'PendingReview' })
}

async function selectDocument(id: string) {
  if (!authStore.accessToken) return
  selectedDocument.value = await getDocument(id, authStore.accessToken)
}

async function approve(versionId: string) {
  if (!authStore.accessToken || !selectedDocument.value) return
  await runAction(async () => {
    await approveDocument(selectedDocument.value!.id, versionId, authStore.accessToken!)
    await loadQueue()
    selectedDocument.value = await getDocument(selectedDocument.value!.id, authStore.accessToken!)
  }, 'Đã duyệt version.')
}

async function reject(versionId: string) {
  if (!authStore.accessToken || !selectedDocument.value) return
  await runAction(async () => {
    await rejectDocument(selectedDocument.value!.id, versionId, rejectReason.value, authStore.accessToken!)
    rejectReason.value = ''
    await loadQueue()
    selectedDocument.value = await getDocument(selectedDocument.value!.id, authStore.accessToken!)
  }, 'Đã reject version.')
}

async function runAction(action: () => Promise<void>, success: string) {
  errorMessage.value = ''
  successMessage.value = ''
  try {
    await action()
    successMessage.value = success
  } catch (error) {
    errorMessage.value = error instanceof ApiError ? error.message : 'Không thể xử lý yêu cầu.'
  }
}

onMounted(loadQueue)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Review tài liệu</h2>
      <p>Duyệt hoặc reject các version đang chờ review.</p>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <div class="split-layout">
      <table class="data-table">
        <thead>
          <tr>
            <th>Tên</th>
            <th>Folder</th>
            <th>Pending</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="document in documents" :key="document.id" class="clickable-row" @click="selectDocument(document.id)">
            <td>{{ document.title }}</td>
            <td>{{ document.folderPath }}</td>
            <td>{{ document.pendingVersionCount }}</td>
          </tr>
        </tbody>
      </table>

      <div v-if="selectedDocument" class="folder-detail">
        <h3>{{ selectedDocument.title }}</h3>
        <div v-for="version in selectedDocument.versions.filter((item) => item.status === 'PendingReview')" :key="version.id" class="review-item">
          <strong>Version {{ version.versionNumber }}</strong>
          <span>{{ version.originalFileName }}</span>
          <button type="button" @click="approve(version.id)">Approve</button>
          <textarea v-model="rejectReason" placeholder="Lý do reject"></textarea>
          <button class="danger-button" type="button" @click="reject(version.id)">Reject</button>
        </div>
      </div>
    </div>
  </section>
</template>
