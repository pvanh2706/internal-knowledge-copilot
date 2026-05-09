<script setup lang="ts">
import { onMounted, ref } from 'vue'
import type { VisibilityScope, WikiDraftDetail, WikiDraftListItem } from '../../api/wiki'
import { getWikiDraft, getWikiDrafts, publishWikiDraft, rejectWikiDraft } from '../../api/wiki'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const drafts = ref<WikiDraftListItem[]>([])
const selectedDraft = ref<WikiDraftDetail | null>(null)
const errorMessage = ref('')
const successMessage = ref('')
const publishForm = ref({
  visibilityScope: 'Folder' as VisibilityScope,
  isCompanyPublicConfirmed: false,
})
const rejectReason = ref('')

async function loadData() {
  if (!authStore.accessToken) return
  drafts.value = await getWikiDrafts(authStore.accessToken)
  if (selectedDraft.value) {
    selectedDraft.value = await getWikiDraft(selectedDraft.value.id, authStore.accessToken).catch(() => null)
  }
}

async function selectDraft(id: string) {
  if (!authStore.accessToken) return
  selectedDraft.value = await getWikiDraft(id, authStore.accessToken)
  publishForm.value.visibilityScope = 'Folder'
  publishForm.value.isCompanyPublicConfirmed = false
  rejectReason.value = ''
  errorMessage.value = ''
  successMessage.value = ''
}

async function submitPublish() {
  if (!authStore.accessToken || !selectedDraft.value) return
  errorMessage.value = ''
  successMessage.value = ''

  try {
    await publishWikiDraft(
      selectedDraft.value.id,
      {
        visibilityScope: publishForm.value.visibilityScope,
        folderId: null,
        isCompanyPublicConfirmed: publishForm.value.isCompanyPublicConfirmed,
      },
      authStore.accessToken,
    )
    successMessage.value = 'Da publish wiki va index vao vector DB.'
    await loadData()
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Khong the publish wiki.'
  }
}

async function submitReject() {
  if (!authStore.accessToken || !selectedDraft.value) return
  errorMessage.value = ''
  successMessage.value = ''

  try {
    selectedDraft.value = await rejectWikiDraft(selectedDraft.value.id, rejectReason.value, authStore.accessToken)
    successMessage.value = 'Da reject wiki draft.'
    await loadData()
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Khong the reject wiki.'
  }
}

onMounted(loadData)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Wiki drafts</h2>
      <p>Reviewer kiểm tra wiki draft, publish hoặc reject trước khi đưa vào Q&A.</p>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <div class="split-layout">
      <table class="data-table">
        <thead>
          <tr>
            <th>Tiêu đề</th>
            <th>Folder</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="draft in drafts" :key="draft.id" class="clickable-row" @click="selectDraft(draft.id)">
            <td>{{ draft.title }}</td>
            <td>{{ draft.folderPath }}</td>
            <td>{{ draft.status }}</td>
          </tr>
        </tbody>
      </table>

      <div v-if="selectedDraft" class="folder-detail">
        <h3>{{ selectedDraft.title }}</h3>
        <p>{{ selectedDraft.folderPath }} - {{ selectedDraft.status }}</p>
        <pre class="wiki-content">{{ selectedDraft.content }}</pre>

        <form v-if="selectedDraft.status === 'Draft'" class="stack-form" @submit.prevent="submitPublish">
          <label>
            Phạm vi publish
            <select v-model="publishForm.visibilityScope">
              <option value="Folder">Theo folder nguồn</option>
              <option value="Company">Toàn công ty</option>
            </select>
          </label>
          <label v-if="publishForm.visibilityScope === 'Company'" class="checkbox-line">
            <input v-model="publishForm.isCompanyPublicConfirmed" type="checkbox" />
            Xác nhận nội dung được phép public nội bộ
          </label>
          <button type="submit">Publish wiki</button>
        </form>

        <form v-if="selectedDraft.status === 'Draft'" class="stack-form" @submit.prevent="submitReject">
          <label>
            Lý do reject
            <textarea v-model="rejectReason" rows="3" placeholder="Nhập lý do reject..." />
          </label>
          <button type="submit" class="danger-button">Reject draft</button>
        </form>
      </div>
    </div>
  </section>
</template>
