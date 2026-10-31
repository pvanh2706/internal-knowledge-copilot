<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import { LinkIcon, LockIcon, PlusIcon, SendIcon, SparklesIcon, XCircleIcon } from '@lucide/vue'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import AnswerPanel from '@/components/app/AnswerPanel.vue'
import CitationCard from '@/components/app/CitationCard.vue'
import EmptyState from '@/components/app/EmptyState.vue'
import ScopeSelector from '@/components/app/ScopeSelector.vue'
import { askQuestion, type AiScopeType, type AskQuestionPayload, type AskQuestionResponse } from '@/api/ai'
import type { DocumentListItem } from '@/api/documents'
import { getDocuments } from '@/api/documents'
import { submitAiFeedback, type AiFeedbackValue } from '@/api/feedback'
import { useAuthStore } from '@/stores/auth'
import { useFullBleedLayout } from '@/composables/useAppShell'
import { useFolderTree } from '@/composables/useFolderTree'
import { useToast } from '@/composables/useToast'
import { errorMessage, isForbiddenError } from '@/lib/errors'

// UX-01 AI Workspace (B1): cột hỏi đáp + composer ở giữa, cột nguồn & ngữ cảnh bên phải.
// Hỏi đáp một lượt theo contract /ai/ask hiện tại — không có lịch sử hội thoại hay streaming.
useFullBleedLayout()

const authStore = useAuthStore()
const toast = useToast()
const { flatFolders, loadFolders } = useFolderTree()

const documents = ref<DocumentListItem[]>([])
const answer = ref<AskQuestionResponse | null>(null)
const isLoading = ref(false)
const isSubmittingFeedback = ref(false)
const askError = ref<{ forbidden: boolean; message: string } | null>(null)
// Câu hỏi + payload của lượt hỏi gần nhất — hiển thị bong bóng câu hỏi và dùng cho "Thử lại"
const askedQuestion = ref('')
const lastPayload = ref<AskQuestionPayload | null>(null)
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

const missingScopeMessage = computed(() => {
  if (form.value.scopeType === 'Folder' && !form.value.folderId) return 'Chọn thư mục để giới hạn phạm vi truy xuất.'
  if (form.value.scopeType === 'Document' && !form.value.documentId) return 'Chọn tài liệu để giới hạn phạm vi truy xuất.'
  return ''
})

const canSubmit = computed(() => Boolean(form.value.question.trim()) && !missingScopeMessage.value)

const citations = computed(() => answer.value?.citations ?? [])

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
  const lineHeight = 22
  element.style.height = `${Math.min(element.scrollHeight, lineHeight * 8 + 8)}px`
}

function handleKeydown(event: KeyboardEvent) {
  // isComposing: đang gõ bộ gõ tiếng Việt (Telex/VNI) — Enter để chốt chữ, không gửi câu hỏi
  if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
    event.preventDefault()
    void submitQuestion()
  }
}

async function submitQuestion() {
  if (!canSubmit.value || isLoading.value) return
  await ask({
    question: form.value.question,
    scopeType: form.value.scopeType,
    folderId: form.value.scopeType === 'Folder' ? form.value.folderId : null,
    documentId: form.value.scopeType === 'Document' ? form.value.documentId : null,
  })
}

async function ask(payload: AskQuestionPayload) {
  if (!authStore.accessToken) return
  askError.value = null
  answer.value = null
  askedQuestion.value = payload.question
  lastPayload.value = payload
  isLoading.value = true

  try {
    answer.value = await askQuestion(payload, authStore.accessToken)
    // Câu hỏi đã hiện ở bong bóng phía trên — làm trống composer cho câu tiếp theo
    if (form.value.question === payload.question) {
      form.value.question = ''
      await nextTick()
      resizeTextarea()
    }
  } catch (error) {
    askError.value = { forbidden: isForbiddenError(error), message: errorMessage(error, 'Không thể hỏi AI.') }
  } finally {
    isLoading.value = false
  }
}

function retryLastQuestion() {
  if (lastPayload.value && !isLoading.value) {
    void ask(lastPayload.value)
  }
}

function startNewQuestion() {
  answer.value = null
  askError.value = null
  askedQuestion.value = ''
  lastPayload.value = null
  form.value.question = ''
  void nextTick(() => {
    resizeTextarea()
    textareaRef.value?.focus()
  })
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
    toast.error(errorMessage(error, 'Không thể gửi phản hồi.'))
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
  <div
    class="grid h-full min-h-0 grid-cols-[minmax(0,1fr)_308px] max-[1100px]:grid-cols-[minmax(0,1fr)_245px] max-[750px]:flex max-[750px]:h-auto max-[750px]:shrink-0 max-[750px]:flex-col"
  >
    <section class="flex min-h-0 min-w-0 flex-col bg-card max-[750px]:min-h-150" aria-label="Hỏi đáp với AI">
      <div class="min-h-0 flex-1 overflow-y-auto px-[clamp(20px,4vw,54px)] pt-5.5 pb-2.5 max-[1100px]:px-6 max-[750px]:overflow-visible max-[750px]:px-3.5 max-[750px]:py-5">
        <div class="mx-auto grid max-w-[750px] gap-5">
          <div>
            <p class="mb-1 text-[10px] font-extrabold uppercase tracking-[0.11em] text-primary-soft-foreground">AI-powered knowledge workspace</p>
            <h2 class="text-[23px] font-bold tracking-tight max-[750px]:text-[21px]">Bạn đang tìm hiểu vấn đề gì?</h2>
            <p class="mt-1 text-xs text-muted-foreground">
              Hỏi về tài liệu nội bộ đã được duyệt và lập chỉ mục trong phạm vi bạn được phép xem.
            </p>
          </div>

          <ScopeSelector
            v-model:scope-type="form.scopeType"
            v-model:folder-id="form.folderId"
            v-model:document-id="form.documentId"
            variant="inline"
            id-prefix="ai-scope"
            :folders="flatFolders"
            :documents="indexedDocuments"
          />

          <div aria-live="polite" :aria-busy="isLoading" class="grid gap-3">
            <p
              v-if="askedQuestion && (isLoading || answer || askError)"
              class="ml-auto max-w-[90%] rounded-xl rounded-br-[3px] border border-primary/10 bg-primary-soft/60 px-4 py-3 text-[13px] font-semibold whitespace-pre-wrap break-words"
            >
              <span class="sr-only">Câu hỏi của bạn: </span>{{ askedQuestion }}
            </p>

            <div v-if="isLoading" class="grid gap-3 rounded-xl border bg-card p-4.5">
              <p class="flex items-center gap-2 text-[13px] font-semibold">
                <SparklesIcon class="size-4 text-primary-soft-foreground" aria-hidden="true" />
                Đang tìm trong kho tri thức...
              </p>
              <div class="h-3 w-full rounded-[5px] animate-shimmer"></div>
              <div class="h-3 w-[85%] rounded-[5px] animate-shimmer"></div>
              <div class="h-3 w-[94%] rounded-[5px] animate-shimmer"></div>
              <div class="h-3 w-[65%] rounded-[5px] animate-shimmer"></div>
            </div>

            <EmptyState
              v-else-if="askError?.forbidden"
              :icon="LockIcon"
              title="Không thể truy cập nguồn này"
              :description="askError.message"
              role="alert"
            />

            <EmptyState
              v-else-if="askError"
              :icon="XCircleIcon"
              title="Không thể tải câu trả lời"
              :description="askError.message"
              role="alert"
            >
              <template #action>
                <Button variant="outline" size="sm" type="button" class="text-xs" @click="retryLastQuestion">Thử lại</Button>
              </template>
            </EmptyState>

            <Transition
              v-else-if="answer"
              appear
              enter-active-class="transition duration-150 ease-out motion-reduce:transition-none"
              enter-from-class="translate-y-2 opacity-0"
              enter-to-class="translate-y-0 opacity-100"
            >
              <AnswerPanel :answer="answer" :is-submitting-feedback="isSubmittingFeedback" @feedback="submitFeedback">
                <template #follow-ups>
                  <button
                    v-for="followUp in answer.suggestedFollowUps"
                    :key="followUp"
                    type="button"
                    class="rounded-full border bg-card px-3 py-1 text-xs text-foreground/80 outline-none transition-colors hover:border-primary/40 hover:bg-primary-soft hover:text-primary-soft-foreground focus-visible:ring-2 focus-visible:ring-ring"
                    @click="useFollowUp(followUp)"
                  >
                    {{ followUp }}
                  </button>
                </template>
              </AnswerPanel>
            </Transition>

            <EmptyState
              v-else
              :icon="SparklesIcon"
              title="Chưa có câu hỏi"
              description="Hãy nhập câu hỏi để bắt đầu tra cứu trong phạm vi được phép."
            />
          </div>
        </div>
      </div>

      <form
        class="mx-[clamp(20px,4vw,54px)] mt-3 mb-5 rounded-[13px] border border-input bg-card p-3 shadow-lg shadow-foreground/5 transition-shadow focus-within:border-primary/50 focus-within:ring-3 focus-within:ring-ring/20 max-[1100px]:mx-6 max-[750px]:mx-3.5"
        @submit.prevent="submitQuestion"
      >
        <label for="ai-question" class="sr-only">Câu hỏi dành cho AI</label>
        <textarea
          id="ai-question"
          ref="textareaRef"
          v-model="form.question"
          rows="2"
          placeholder="Đặt câu hỏi về tài liệu nội bộ... (Enter để gửi, Shift+Enter xuống dòng)"
          class="block max-h-45 min-h-12 w-full resize-none bg-transparent text-[13px] leading-[22px] outline-none placeholder:text-muted-foreground"
          :aria-describedby="missingScopeMessage ? 'ai-scope-hint' : undefined"
          @input="resizeTextarea"
          @keydown="handleKeydown"
        ></textarea>
        <div class="mt-1 flex flex-wrap items-center justify-between gap-2.5">
          <p v-if="missingScopeMessage" id="ai-scope-hint" class="text-[11px] font-medium text-warning">
            {{ missingScopeMessage }}
          </p>
          <p v-else class="text-[11px] text-muted-foreground">Chỉ dùng tài liệu đã duyệt trong phạm vi bạn được phép xem.</p>
          <div class="flex items-center gap-2">
            <Button variant="outline" size="sm" type="button" class="text-xs" @click="startNewQuestion">
              <PlusIcon class="size-4" />
              Câu hỏi mới
            </Button>
            <Button size="sm" type="submit" class="text-xs" :disabled="isLoading || !canSubmit">
              <SendIcon class="size-4" />
              Gửi
            </Button>
          </div>
        </div>
      </form>
    </section>

    <aside
      class="flex min-h-0 min-w-0 flex-col overflow-y-auto border-l bg-surface max-[750px]:border-t max-[750px]:border-l-0"
      aria-label="Nguồn và ngữ cảnh"
    >
      <div class="flex items-center justify-between gap-2 border-b px-4.5 py-5 max-[750px]:px-4 max-[750px]:py-3.5">
        <h2 class="flex items-center gap-2 text-[13px] font-extrabold">
          <LinkIcon class="size-4.5 text-primary-soft-foreground" aria-hidden="true" />
          Nguồn &amp; Ngữ cảnh
        </h2>
        <Badge v-if="answer" variant="muted">{{ citations.length }} nguồn</Badge>
      </div>

      <section class="px-3.5 pt-4.5" aria-labelledby="ai-sources-heading">
        <h3 id="ai-sources-heading" class="mb-2.5 text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground">
          Nguồn tham khảo<template v-if="answer"> ({{ citations.length }})</template>
        </h3>
        <div v-if="isLoading" class="grid gap-2">
          <div v-for="n in 2" :key="n" class="h-20 rounded-[10px] border animate-shimmer"></div>
        </div>
        <ol v-else-if="citations.length" class="grid gap-2.5">
          <li v-for="(citation, index) in citations" :key="`${citation.sourceType}-${citation.title}-${index}`">
            <CitationCard
              :index="index + 1"
              :source-type="citation.sourceType"
              :title="citation.title"
              :folder-path="citation.folderPath"
              :section-title="citation.sectionTitle"
              :excerpt="citation.excerpt"
            />
          </li>
        </ol>
        <EmptyState
          v-else-if="answer"
          title="Chưa có nguồn phù hợp"
          description="Không có nguồn đủ phù hợp trong phạm vi đã chọn. Thử mở rộng phạm vi hoặc diễn đạt lại câu hỏi."
        />
        <p v-else class="rounded-[10px] border border-dashed bg-card px-3 py-4 text-center text-[11px] text-muted-foreground">
          Nguồn trích dẫn sẽ hiển thị tại đây sau khi AI trả lời.
        </p>
      </section>

      <section class="px-3.5 pt-4.5" aria-labelledby="ai-context-heading">
        <h3 id="ai-context-heading" class="mb-2.5 text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground">
          Ngữ cảnh nghiệp vụ
        </h3>
        <div class="rounded-[10px] border bg-card p-3 text-[11px] leading-relaxed text-muted-foreground">
          Câu trả lời hiện chỉ dựa trên tài liệu nội bộ đã duyệt. Ngữ cảnh từ hệ thống nghiệp vụ chỉ hiển thị khi máy chủ hỗ trợ và cấp quyền hợp lệ.
          <div class="mt-2.5"><Badge variant="muted" class="bg-warning/10 text-warning">Chưa khả dụng</Badge></div>
        </div>
      </section>

      <section class="px-3.5 pt-4.5 pb-5" aria-labelledby="ai-access-heading">
        <h3 id="ai-access-heading" class="mb-2.5 text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground">
          Quyền truy cập
        </h3>
        <p class="flex items-start gap-2 rounded-[10px] border bg-card p-3 text-[11px] leading-relaxed text-muted-foreground">
          <LockIcon class="mt-0.5 size-3.5 shrink-0 text-primary-soft-foreground" aria-hidden="true" />
          “Tất cả nguồn” nghĩa là mọi nguồn bạn được phép xem. Quyền truy cập luôn do máy chủ kiểm tra.
        </p>
      </section>
    </aside>
  </div>
</template>
