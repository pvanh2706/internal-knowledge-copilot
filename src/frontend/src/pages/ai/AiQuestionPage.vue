<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import { SendIcon, SparklesIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import AnswerPanel from '@/components/app/AnswerPanel.vue'
import CitationCard from '@/components/app/CitationCard.vue'
import EmptyState from '@/components/app/EmptyState.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import ScopeSelector from '@/components/app/ScopeSelector.vue'
import { askQuestion, type AiScopeType, type AskQuestionResponse } from '@/api/ai'
import type { DocumentListItem } from '@/api/documents'
import { getDocuments } from '@/api/documents'
import { submitAiFeedback, type AiFeedbackValue } from '@/api/feedback'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import { useFolderTree } from '@/composables/useFolderTree'
import { useToast } from '@/composables/useToast'

const authStore = useAuthStore()
const toast = useToast()
const { flatFolders, loadFolders } = useFolderTree()

const documents = ref<DocumentListItem[]>([])
const answer = ref<AskQuestionResponse | null>(null)
const isLoading = ref(false)
const isSubmittingFeedback = ref(false)
const errorMessage = ref('')
const textareaRef = ref<HTMLTextAreaElement | null>(null)

const form = ref({
  question: '',
  scopeType: 'All' as AiScopeType,
  folderId: '',
  documentId: '',
})

const indexedDocuments = computed(() =>
  documents.value.filter((document) => document.latestVersionStatus === 'Indexed'),
)

const canSubmit = computed(() => {
  if (!form.value.question.trim()) return false
  if (form.value.scopeType === 'Folder') return Boolean(form.value.folderId)
  if (form.value.scopeType === 'Document') return Boolean(form.value.documentId)
  return true
})

async function loadScopeData() {
  if (!authStore.accessToken) return
  try {
    const [documentList] = await Promise.all([
      getDocuments(authStore.accessToken, { status: 'Approved' }),
      loadFolders(authStore.accessToken),
    ])
    documents.value = documentList
  } catch {
    // Không chặn hỏi AI khi tải dữ liệu scope thất bại — scope mặc định "Tất cả" vẫn dùng được
  }
}

// Auto-grow textarea 2→8 dòng
function resizeTextarea() {
  const element = textareaRef.value
  if (!element) return
  element.style.height = 'auto'
  const lineHeight = 24
  element.style.height = `${Math.min(element.scrollHeight, lineHeight * 8 + 16)}px`
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    void submitQuestion()
  }
}

async function submitQuestion() {
  if (!authStore.accessToken || !canSubmit.value || isLoading.value) return
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
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể hỏi AI.'
  } finally {
    isLoading.value = false
  }
}

async function submitFeedback(value: AiFeedbackValue, note: string) {
  if (!authStore.accessToken || !answer.value || isSubmittingFeedback.value) return
  isSubmittingFeedback.value = true

  try {
    await submitAiFeedback(answer.value.interactionId, { value, note }, authStore.accessToken)
    toast.success(
      value === 'Correct' ? 'Đã ghi nhận câu trả lời đúng.' : 'Đã gửi phản hồi sai cho reviewer.',
    )
  } catch (error) {
    toast.error(error instanceof ApiError || error instanceof Error ? error.message : 'Không thể gửi phản hồi.')
  } finally {
    isSubmittingFeedback.value = false
  }
}

async function useFollowUp(question: string) {
  form.value.question = question
  await nextTick()
  resizeTextarea()
  void submitQuestion()
}

onMounted(loadScopeData)
</script>

<template>
  <div class="mx-auto grid max-w-3xl gap-6">
    <div class="text-center">
      <h1 class="text-2xl font-semibold tracking-tight">Hỏi AI</h1>
      <p class="mt-1 text-sm text-muted-foreground">
        Đặt câu hỏi trên các tài liệu đã duyệt và lập chỉ mục trong phạm vi bạn có quyền xem.
      </p>
    </div>

    <form
      class="grid gap-3 rounded-xl border bg-card p-4 text-card-foreground transition-shadow focus-within:ring-2 focus-within:ring-ring/40"
      @submit.prevent="submitQuestion"
    >
      <textarea
        ref="textareaRef"
        v-model="form.question"
        rows="2"
        required
        placeholder="Nhập câu hỏi cần tra cứu... (Enter để gửi, Shift+Enter xuống dòng)"
        aria-label="Câu hỏi cho AI"
        class="w-full resize-none bg-transparent text-base leading-6 outline-none placeholder:text-muted-foreground"
        @input="resizeTextarea"
        @keydown="handleKeydown"
      ></textarea>
      <div class="flex items-center justify-between gap-3">
        <ScopeSelector
          v-model:scope-type="form.scopeType"
          v-model:folder-id="form.folderId"
          v-model:document-id="form.documentId"
          :folders="flatFolders"
          :documents="indexedDocuments"
        />
        <Button type="submit" size="icon" :disabled="isLoading || !canSubmit" aria-label="Gửi câu hỏi">
          <SendIcon class="size-4" />
        </Button>
      </div>
    </form>

    <ErrorState v-if="errorMessage" :message="errorMessage" hide-retry />

    <div v-if="isLoading" class="grid gap-3 rounded-xl border bg-card p-5">
      <p class="text-sm text-muted-foreground">Đang tìm trong kho tri thức...</p>
      <Skeleton class="h-4 w-full" />
      <Skeleton class="h-4 w-11/12" />
      <Skeleton class="h-4 w-full" />
      <Skeleton class="h-4 w-2/3" />
    </div>

    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="translate-y-2 opacity-0"
      enter-to-class="translate-y-0 opacity-100"
    >
      <div v-if="answer" class="grid gap-4">
        <AnswerPanel :answer="answer" :is-submitting-feedback="isSubmittingFeedback" @feedback="submitFeedback">
          <template #follow-ups>
            <button
              v-for="followUp in answer.suggestedFollowUps"
              :key="followUp"
              type="button"
              class="rounded-full border px-3 py-1 text-sm text-muted-foreground outline-none transition-colors hover:bg-accent hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
              @click="useFollowUp(followUp)"
            >
              {{ followUp }}
            </button>
          </template>
        </AnswerPanel>

        <div>
          <p class="mb-2 text-sm font-medium">Nguồn tham khảo</p>
          <EmptyState
            v-if="answer.citations.length === 0"
            title="Chưa có nguồn đủ phù hợp trong phạm vi đã chọn"
            description="Thử mở rộng phạm vi hoặc diễn đạt lại câu hỏi."
          />
          <div v-else class="grid gap-3 sm:grid-cols-2">
            <CitationCard
              v-for="(citation, index) in answer.citations"
              :key="`${citation.sourceType}-${citation.title}-${index}`"
              :index="index + 1"
              :source-type="citation.sourceType"
              :title="citation.title"
              :folder-path="citation.folderPath"
              :section-title="citation.sectionTitle"
              :excerpt="citation.excerpt"
            />
          </div>
        </div>
      </div>
    </Transition>

    <EmptyState
      v-if="!answer && !isLoading && !errorMessage"
      :icon="SparklesIcon"
      title="Bắt đầu bằng một câu hỏi"
      description="Ví dụ: “Quy trình xin nghỉ phép của công ty như thế nào?”"
    />
  </div>
</template>
