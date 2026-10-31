<script setup lang="ts">
import { ref, watch } from 'vue'
import { CopyIcon, SparklesIcon, ThumbsDownIcon, ThumbsUpIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import InlineNotice from '@/components/app/InlineNotice.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import type { AskQuestionResponse } from '@/api/ai'
import type { AiFeedbackValue } from '@/api/feedback'
import { useToast } from '@/composables/useToast'

const props = defineProps<{
  answer: AskQuestionResponse
  isSubmittingFeedback?: boolean
}>()

const emit = defineEmits<{
  feedback: [value: AiFeedbackValue, note: string]
}>()

const toast = useToast()
const showNoteBox = ref(false)
const feedbackNote = ref('')

// Reset khung note khi có câu trả lời mới
watch(
  () => props.answer.interactionId,
  () => {
    showNoteBox.value = false
    feedbackNote.value = ''
  },
)

async function copyAnswer() {
  try {
    await navigator.clipboard.writeText(props.answer.answer)
    toast.success('Đã sao chép câu trả lời.')
  } catch {
    toast.error('Không thể sao chép.')
  }
}
</script>

<template>
  <!-- Thẻ câu trả lời B1 — độ tin cậy chỉ là hạng mục high/medium/low từ API, không hiển thị phần trăm -->
  <article class="grid gap-3.5 rounded-[13px] border bg-card p-5 text-card-foreground shadow-lg shadow-foreground/3" aria-label="Câu trả lời của AI">
    <div class="flex flex-wrap items-center gap-2.5">
      <span class="grid size-8 shrink-0 place-items-center rounded-[10px] bg-linear-to-br from-primary/70 to-primary text-primary-foreground" aria-hidden="true">
        <SparklesIcon class="size-5" />
      </span>
      <strong class="text-[13px]">Enterprise Copilot</strong>
      <StatusBadge :status="answer.confidence" />
    </div>

    <InlineNotice v-if="answer.needsClarification" tone="info" title="Copilot cần thêm ngữ cảnh." role="status">
      Hãy bổ sung chi tiết hoặc thu hẹp phạm vi để AI trả lời chắc chắn hơn.
    </InlineNotice>

    <p class="whitespace-pre-wrap text-[13px] leading-[1.83] text-foreground/85">{{ answer.answer }}</p>

    <InlineNotice v-if="answer.missingInformation.length" tone="warning" title="Thông tin còn thiếu">
      <ul class="grid list-disc gap-0.5 pl-4">
        <li v-for="item in answer.missingInformation" :key="item">{{ item }}</li>
      </ul>
    </InlineNotice>

    <InlineNotice v-if="answer.conflicts.length" tone="danger" title="Mâu thuẫn giữa các nguồn">
      <ul class="grid list-disc gap-0.5 pl-4">
        <li v-for="item in answer.conflicts" :key="item">{{ item }}</li>
      </ul>
    </InlineNotice>

    <div v-if="answer.suggestedFollowUps.length" class="grid gap-2">
      <p class="text-[11px] font-semibold text-muted-foreground">Câu hỏi gợi ý tiếp theo</p>
      <div class="flex flex-wrap gap-2">
        <slot name="follow-ups" />
      </div>
    </div>

    <div class="mt-1 flex flex-wrap items-center gap-1.5 border-t pt-3">
      <p class="mr-auto text-[11px] text-muted-foreground">Câu trả lời có hữu ích không?</p>
      <Button
        variant="ghost"
        size="icon-sm"
        type="button"
        class="text-muted-foreground hover:text-primary-soft-foreground"
        aria-label="Câu trả lời đúng"
        :disabled="isSubmittingFeedback"
        @click="emit('feedback', 'Correct', feedbackNote)"
      >
        <ThumbsUpIcon class="size-4" />
      </Button>
      <Button
        variant="ghost"
        size="icon-sm"
        type="button"
        class="text-muted-foreground hover:text-primary-soft-foreground"
        aria-label="Câu trả lời sai"
        :aria-expanded="showNoteBox"
        :disabled="isSubmittingFeedback"
        @click="showNoteBox = true"
      >
        <ThumbsDownIcon class="size-4" />
      </Button>
      <Button
        variant="ghost"
        size="sm"
        type="button"
        class="text-xs text-muted-foreground hover:text-primary-soft-foreground"
        aria-label="Sao chép câu trả lời"
        @click="copyAnswer"
      >
        <CopyIcon class="size-4" />
        Sao chép
      </Button>
    </div>

    <div v-if="showNoteBox" class="grid gap-2">
      <label for="ai-feedback-note" class="text-[11px] font-semibold text-foreground/80">Điều gì chưa đúng?</label>
      <Textarea
        id="ai-feedback-note"
        v-model="feedbackNote"
        rows="3"
        placeholder="Ghi chú giúp reviewer xử lý nhanh hơn..."
      />
      <div class="flex justify-end gap-2">
        <Button variant="outline" size="sm" type="button" class="text-xs" @click="showNoteBox = false">Hủy</Button>
        <Button
          variant="destructive"
          size="sm"
          type="button"
          class="text-xs"
          :disabled="isSubmittingFeedback"
          @click="emit('feedback', 'Incorrect', feedbackNote)"
        >
          Gửi phản hồi sai
        </Button>
      </div>
    </div>
  </article>
</template>
