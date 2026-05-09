<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import type { DocumentDetail, DocumentListItem, DocumentStatus } from '../../api/documents'
import { downloadDocument, getDocument, getDocuments, uploadDocument, uploadDocumentVersion } from '../../api/documents'
import type { FolderTreeItem } from '../../api/folders'
import { getFolderTree } from '../../api/folders'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const documents = ref<DocumentListItem[]>([])
const folders = ref<FolderTreeItem[]>([])
const selectedDocument = ref<DocumentDetail | null>(null)
const errorMessage = ref('')
const successMessage = ref('')
const selectedFile = ref<File | null>(null)
const selectedVersionFile = ref<File | null>(null)

const filters = ref({ status: '' as '' | DocumentStatus, keyword: '', folderId: '' })
const uploadForm = ref({ folderId: '', title: '', description: '' })
const flattenedFolders = computed(() => flattenFolders(folders.value))

async function loadData() {
  if (!authStore.accessToken) return
  const [documentList, folderTree] = await Promise.all([
    getDocuments(authStore.accessToken, {
      folderId: filters.value.folderId || undefined,
      status: filters.value.status || undefined,
      keyword: filters.value.keyword || undefined,
    }),
    getFolderTree(authStore.accessToken),
  ])
  documents.value = documentList
  folders.value = folderTree
}

async function selectDocument(id: string) {
  if (!authStore.accessToken) return
  selectedDocument.value = await getDocument(id, authStore.accessToken)
}

async function submitUpload() {
  if (!authStore.accessToken || !selectedFile.value) return
  await runAction(async () => {
    selectedDocument.value = await uploadDocument({ ...uploadForm.value, file: selectedFile.value! }, authStore.accessToken!)
    selectedFile.value = null
    uploadForm.value.title = ''
    uploadForm.value.description = ''
    await loadData()
  }, 'Da upload tai lieu, dang cho reviewer duyet.')
}

async function submitUploadVersion() {
  if (!authStore.accessToken || !selectedDocument.value || !selectedVersionFile.value) return
  await runAction(async () => {
    selectedDocument.value = await uploadDocumentVersion(selectedDocument.value!.id, selectedVersionFile.value!, authStore.accessToken!)
    selectedVersionFile.value = null
    await loadData()
  }, 'Da upload version moi, dang cho reviewer duyet.')
}

async function submitDownload(versionId?: string) {
  if (!authStore.accessToken || !selectedDocument.value) return
  await runAction(async () => {
    await downloadDocument(selectedDocument.value!.id, authStore.accessToken!, versionId)
  }, 'Da bat dau tai file.')
}

function handleFileChange(event: Event) {
  selectedFile.value = (event.target as HTMLInputElement).files?.[0] ?? null
}

function handleVersionFileChange(event: Event) {
  selectedVersionFile.value = (event.target as HTMLInputElement).files?.[0] ?? null
}

async function runAction(action: () => Promise<void>, success: string) {
  errorMessage.value = ''
  successMessage.value = ''
  try {
    await action()
    successMessage.value = success
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Khong the xu ly yeu cau.'
  }
}

function flattenFolders(items: FolderTreeItem[], level = 0): Array<FolderTreeItem & { label: string }> {
  return items.flatMap((item) => [
    { ...item, label: `${'--'.repeat(level)} ${item.name}`.trim() },
    ...flattenFolders(item.children, level + 1),
  ])
}

onMounted(loadData)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Tài liệu</h2>
      <p>Upload tài liệu, xem trạng thái review và tải file gốc.</p>
    </div>

    <form class="management-form" @submit.prevent="submitUpload">
      <select v-model="uploadForm.folderId" required>
        <option value="">Chọn folder</option>
        <option v-for="folder in flattenedFolders" :key="folder.id" :value="folder.id">{{ folder.label }}</option>
      </select>
      <input v-model="uploadForm.title" type="text" placeholder="Tên tài liệu" required />
      <input v-model="uploadForm.description" type="text" placeholder="Mô tả" />
      <input type="file" accept=".pdf,.docx,.md,.markdown,.txt" required @change="handleFileChange" />
      <button type="submit">Upload</button>
    </form>

    <form class="management-form" @submit.prevent="loadData">
      <select v-model="filters.folderId">
        <option value="">Tất cả folder</option>
        <option v-for="folder in flattenedFolders" :key="folder.id" :value="folder.id">{{ folder.label }}</option>
      </select>
      <select v-model="filters.status">
        <option value="">Tất cả trạng thái</option>
        <option value="PendingReview">Chờ duyệt</option>
        <option value="Approved">Đã duyệt</option>
        <option value="Rejected">Bị reject</option>
      </select>
      <input v-model="filters.keyword" type="text" placeholder="Tìm theo tên/mô tả" />
      <button type="submit">Lọc</button>
    </form>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <div class="split-layout">
      <table class="data-table">
        <thead>
          <tr>
            <th>Tên</th>
            <th>Folder</th>
            <th>Status</th>
            <th>Version</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="document in documents" :key="document.id" class="clickable-row" @click="selectDocument(document.id)">
            <td>{{ document.title }}</td>
            <td>{{ document.folderPath }}</td>
            <td>{{ document.status }}</td>
            <td>{{ document.currentVersionNumber ?? '-' }} / {{ document.latestVersionNumber }}</td>
          </tr>
        </tbody>
      </table>

      <div v-if="selectedDocument" class="folder-detail">
        <h3>{{ selectedDocument.title }}</h3>
        <p>{{ selectedDocument.folderPath }} - {{ selectedDocument.status }}</p>
        <button type="button" @click="submitDownload()">Tải bản hiện tại</button>

        <form class="stack-form" @submit.prevent="submitUploadVersion">
          <label>
            Upload version mới
            <input type="file" accept=".pdf,.docx,.md,.markdown,.txt" required @change="handleVersionFileChange" />
          </label>
          <button type="submit">Upload version</button>
        </form>

        <table class="data-table">
          <thead>
            <tr>
              <th>Version</th>
              <th>File</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="version in selectedDocument.versions" :key="version.id">
              <td>{{ version.versionNumber }}</td>
              <td>
                <button type="button" class="text-button" @click="submitDownload(version.id)">{{ version.originalFileName }}</button>
              </td>
              <td>{{ version.status }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>
