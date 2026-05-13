<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { explainRetrieval, type AiScopeType, type RetrievalExplainResponse } from '../../api/ai'
import type { DocumentListItem } from '../../api/documents'
import { getDocuments } from '../../api/documents'
import type { FolderTreeItem } from '../../api/folders'
import { getFolderTree } from '../../api/folders'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const folders = ref<FolderTreeItem[]>([])
const documents = ref<DocumentListItem[]>([])
const explain = ref<RetrievalExplainResponse | null>(null)
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
const rejectedCandidates = computed(() => explain.value?.candidates.filter((candidate) => !candidate.passedPermissionFilter) ?? [])

async function loadScopeData() {
  if (!authStore.accessToken) return
  const [folderTree, documentList] = await Promise.all([
    getFolderTree(authStore.accessToken),
    getDocuments(authStore.accessToken, { status: 'Approved' }),
  ])
  folders.value = folderTree
  documents.value = documentList
}

async function submitExplain() {
  if (!authStore.accessToken || !canSubmit.value) return
  errorMessage.value = ''
  explain.value = null
  isLoading.value = true

  try {
    explain.value = await explainRetrieval(
      {
        question: form.value.question,
        scopeType: form.value.scopeType,
        folderId: form.value.scopeType === 'Folder' ? form.value.folderId : null,
        documentId: form.value.scopeType === 'Document' ? form.value.documentId : null,
      },
      authStore.accessToken,
    )
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Khong the explain retrieval.'
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

function formatDistance(distance?: number | null) {
  return typeof distance === 'number' ? distance.toFixed(3) : '-'
}

function formatScore(score: number) {
  return score.toFixed(2)
}

onMounted(loadScopeData)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Retrieval Explain</h2>
    </div>

    <form class="ai-form" @submit.prevent="submitExplain">
      <label>
        Cau hoi
        <textarea v-model="form.question" rows="4" placeholder="Nhap cau hoi can kiem tra..." required />
      </label>

      <div class="management-form">
        <select v-model="form.scopeType">
          <option value="All">Tat ca nguon duoc phep</option>
          <option value="Folder">Theo folder</option>
          <option value="Document">Theo tai lieu</option>
        </select>

        <select v-if="form.scopeType === 'Folder'" v-model="form.folderId" required>
          <option value="">Chon folder</option>
          <option v-for="folder in flattenedFolders" :key="folder.id" :value="folder.id">{{ folder.label }}</option>
        </select>

        <select v-if="form.scopeType === 'Document'" v-model="form.documentId" required>
          <option value="">Chon tai lieu da index</option>
          <option v-for="document in indexedDocuments" :key="document.id" :value="document.id">
            {{ document.title }} - {{ document.folderPath }}
          </option>
        </select>

        <button type="submit" :disabled="isLoading || !canSubmit">{{ isLoading ? 'Dang explain...' : 'Explain' }}</button>
      </div>
    </form>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>

    <div v-if="explain" class="management-page">
      <section class="answer-panel">
        <h3>Query</h3>
        <p><strong>Normalized:</strong> {{ explain.queryUnderstanding.normalizedQuestion || '-' }}</p>
        <p><strong>Keywords:</strong> {{ explain.queryUnderstanding.keywords.join(', ') || '-' }}</p>
        <p>
          <strong>Filter:</strong>
          {{ explain.filter.sourceTypes.join(', ') }} /
          {{ explain.filter.statuses.join(', ') }} /
          visible folders {{ explain.filter.visibleFolderCount }} /
          filtered folders {{ explain.filter.filteredFolderCount }}
        </p>
      </section>

      <div class="stat-grid">
        <div class="stat-card">
          <span>Vector</span>
          <strong>{{ explain.candidateStats.vectorCandidateCount }}</strong>
        </div>
        <div class="stat-card">
          <span>Keyword</span>
          <strong>{{ explain.candidateStats.keywordCandidateCount }}</strong>
        </div>
        <div class="stat-card">
          <span>Merged</span>
          <strong>{{ explain.candidateStats.mergedCandidateCount }}</strong>
        </div>
        <div class="stat-card">
          <span>Allowed</span>
          <strong>{{ explain.candidateStats.allowedCandidateCount }}</strong>
        </div>
        <div class="stat-card">
          <span>Final context</span>
          <strong>{{ explain.candidateStats.finalContextCount }}</strong>
        </div>
        <div class="stat-card">
          <span>Rejected</span>
          <strong>{{ rejectedCandidates.length }}</strong>
        </div>
      </div>

      <section class="answer-panel">
        <h3>Final Context</h3>
        <p v-if="explain.finalContext.length === 0">Khong co context phu hop.</p>
        <article v-for="candidate in explain.finalContext" :key="candidate.candidateId" class="citation-item">
          <strong>{{ candidate.sourceType }} - {{ candidate.title }}</strong>
          <small>
            {{ candidate.retrievalSource }} / score {{ formatScore(candidate.score) }} / distance {{ formatDistance(candidate.distance) }}
          </small>
          <small>{{ [candidate.folderPath, candidate.sectionTitle].filter(Boolean).join(' / ') || '-' }}</small>
          <p>{{ candidate.excerpt }}</p>
          <small>{{ candidate.scoreReasons.join(' | ') }}</small>
        </article>
      </section>

      <section class="answer-panel">
        <h3>Candidates</h3>
        <table class="data-table">
          <thead>
            <tr>
              <th>Source</th>
              <th>Score</th>
              <th>Match</th>
              <th>Decision</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="candidate in explain.candidates" :key="candidate.candidateId">
              <td>
                <strong>{{ candidate.sourceType }} - {{ candidate.title }}</strong>
                <small>{{ candidate.retrievalSource }} / {{ [candidate.folderPath, candidate.sectionTitle].filter(Boolean).join(' / ') || '-' }}</small>
              </td>
              <td>{{ formatScore(candidate.score) }}</td>
              <td>{{ candidate.matchedKeywords.join(', ') || '-' }}</td>
              <td>{{ candidate.decision }}</td>
            </tr>
          </tbody>
        </table>
      </section>
    </div>
  </section>
</template>
