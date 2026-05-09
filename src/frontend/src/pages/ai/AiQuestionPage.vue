<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { askQuestion, type AiScopeType, type AskQuestionResponse } from '../../api/ai'
import type { DocumentListItem } from '../../api/documents'
import { getDocuments } from '../../api/documents'
import type { FolderTreeItem } from '../../api/folders'
import { getFolderTree } from '../../api/folders'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const folders = ref<FolderTreeItem[]>([])
const documents = ref<DocumentListItem[]>([])
const answer = ref<AskQuestionResponse | null>(null)
const isLoading = ref(false)
const errorMessage = ref('')
const form = ref({
  question: '',
  scopeType: 'All' as AiScopeType,
  folderId: '',
  documentId: '',
})

const flattenedFolders = computed(() => flattenFolders(folders.value))
const indexedDocuments = computed(() => documents.value.filter((document) => document.latestVersionStatus === 'Indexed'))
const canSubmit = computed(() => {
  if (!form.value.question.trim()) return false
  if (form.value.scopeType === 'Folder') return Boolean(form.value.folderId)
  if (form.value.scopeType === 'Document') return Boolean(form.value.documentId)
  return true
})

async function loadScopeData() {
  if (!authStore.accessToken) return
  const [folderTree, documentList] = await Promise.all([
    getFolderTree(authStore.accessToken),
    getDocuments(authStore.accessToken, { status: 'Approved' }),
  ])
  folders.value = folderTree
  documents.value = documentList
}

async function submitQuestion() {
  if (!authStore.accessToken || !canSubmit.value) return
  errorMessage.value = ''
  answer.value = null
  isLoading.value = true

  try {
    answer.value = await askQuestion(
      {
        question: form.value.question,
        scopeType: form.value.scopeType,
        folderId: form.value.scopeType === 'Folder' ? form.value.folderId : null,
        documentId: form.value.scopeType === 'Document' ? form.value.documentId : null,
      },
      authStore.accessToken,
    )
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Khong the hoi AI.'
  } finally {
    isLoading.value = false
  }
}

function flattenFolders(items: FolderTreeItem[], level = 0): Array<FolderTreeItem & { label: string }> {
  return items.flatMap((item) => [
    { ...item, label: `${'--'.repeat(level)} ${item.name}`.trim() },
    ...flattenFolders(item.children, level + 1),
  ])
}

onMounted(loadScopeData)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Hỏi AI</h2>
      <p>Đặt câu hỏi trên các tài liệu đã được duyệt và index trong phạm vi bạn có quyền xem.</p>
    </div>

    <form class="ai-form" @submit.prevent="submitQuestion">
      <label>
        Câu hỏi
        <textarea v-model="form.question" rows="5" placeholder="Nhập câu hỏi cần tra cứu..." required />
      </label>

      <div class="management-form">
        <select v-model="form.scopeType">
          <option value="All">Tất cả nguồn được phép</option>
          <option value="Folder">Theo folder</option>
          <option value="Document">Theo tài liệu</option>
        </select>

        <select v-if="form.scopeType === 'Folder'" v-model="form.folderId" required>
          <option value="">Chọn folder</option>
          <option v-for="folder in flattenedFolders" :key="folder.id" :value="folder.id">{{ folder.label }}</option>
        </select>

        <select v-if="form.scopeType === 'Document'" v-model="form.documentId" required>
          <option value="">Chọn tài liệu đã index</option>
          <option v-for="document in indexedDocuments" :key="document.id" :value="document.id">
            {{ document.title }} - {{ document.folderPath }}
          </option>
        </select>

        <button type="submit" :disabled="isLoading || !canSubmit">{{ isLoading ? 'Đang hỏi...' : 'Hỏi AI' }}</button>
      </div>
    </form>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>

    <div v-if="answer" class="answer-layout">
      <section class="answer-panel">
        <h3>Câu trả lời</h3>
        <p v-if="answer.needsClarification" class="form-error">AI cần thêm ngữ cảnh để trả lời chắc chắn.</p>
        <pre>{{ answer.answer }}</pre>
      </section>

      <section class="answer-panel">
        <h3>Nguồn</h3>
        <p v-if="answer.citations.length === 0">Chưa có nguồn đủ phù hợp trong phạm vi đã chọn.</p>
        <article v-for="citation in answer.citations" :key="`${citation.sourceType}-${citation.title}-${citation.excerpt}`" class="citation-item">
          <strong>{{ citation.sourceType }} - {{ citation.title }}</strong>
          <small>{{ citation.folderPath || '-' }}</small>
          <p>{{ citation.excerpt }}</p>
        </article>
      </section>
    </div>
  </section>
</template>
