<script setup lang="ts">
import { ref, watch } from 'vue'
import { CopyIcon, InfoIcon, ThumbsDownIcon, ThumbsUpIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
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
  <section class="grid gap-4 rounded-xl border bg-card p-5 text-card-foreground">
    <div class="flex flex-wrap items-center justify-between gap-2">
      <StatusBadge :status="answer.confidence" />
      <Button variant="ghost" size="icon-sm" type="button" aria-label="Sao chép câu trả lời" @click="copyAnswer">
        <CopyIcon class="size-4" />
      </Button>
    </div>

    <div
      v-if="answer.needsClarification"
      class="flex items-start gap-2 rounded-md border-l-2 border-info bg-info/10 p-3 text-sm"
      role="status"
    >
      <InfoIcon class="mt-0.5 size-4 shrink-0 text-info" aria-hidden="true" />
      <p>AI cần thêm ngữ cảnh để trả lời chắc chắn — hãy bổ sung chi tiết hoặc thu hẹp phạm vi.</p>
    </div>

    <p class="whitespace-pre-wrap text-base leading-7">{{ answer.answer }}</p>

    <div v-if="answer.missingInformation.length" class="grid gap-1 border-l-2 border-warning bg-warning/10 p-3 text-sm">
      <p class="font-medium">Thông tin còn thiếu</p>
      <ul class="grid list-disc gap-0.5 pl-5 text-muted-foreground">
        <li v-for="item in answer.missingInformation" :key="item">{{ item }}</li>
      </ul>
    </div>

    <div v-if="answer.conflicts.length" class="grid gap-1 border-l-2 border-destructive bg-destructive/10 p-3 text-sm">
      <p class="font-medium">Mâu thuẫn giữa các nguồn</p>
      <ul class="grid list-disc gap-0.5 pl-5 text-muted-foreground">
        <li v-for="item in answer.conflicts" :key="item">{{ item }}</li>
      </ul>
    </div>

    <div v-if="answer.suggestedFollowUps.length" class="flex flex-wrap gap-2">
      <slot name="follow-ups" />
    </div>

    <div class="flex flex-wrap items-center gap-2 border-t pt-3">
      <p class="mr-auto text-xs text-muted-foreground">Câu trả lời có hữu ích không?</p>
      <Button
        variant="outline"
        size="icon-sm"
        type="button"
        aria-label="Câu trả lời đúng"
        :disabled="isSubmittingFeedback"
        @click="emit('feedback', 'Correct', feedbackNote)"
      >
        <ThumbsUpIcon class="size-4" />
      </Button>
      <Button
        variant="outline"
        size="icon-sm"
        type="button"
        aria-label="Câu trả lời sai"
        :disabled="isSubmittingFeedback"
        @click="showNoteBox = true"
      >
        <ThumbsDownIcon class="size-4" />
      </Button>
    </div>

    <div v-if="showNoteBox" class="grid gap-2">
      <Textarea v-model="feedbackNote" rows="3" placeholder="Điều gì chưa đúng? Ghi chú giúp reviewer xử lý nhanh hơn..." />
      <div class="flex justify-end gap-2">
        <Button variant="outline" size="sm" type="button" @click="showNoteBox = false">Hủy</Button>
        <Button
          variant="destructive"
          size="sm"
          type="button"
          :disabled="isSubmittingFeedback"
          @click="emit('feedback', 'Incorrect', feedbackNote)"
        >
          Gửi phản hồi sai
        </Button>
      </div>
    </div>
  </section>
</template>
