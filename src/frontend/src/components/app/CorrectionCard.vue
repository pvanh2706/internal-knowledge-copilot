<script setup lang="ts">
import { CheckIcon, XIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import StatusBadge from '@/components/app/StatusBadge.vue'
import type { KnowledgeCorrection } from '@/api/feedback'
import { formatDateTime } from '@/lib/format'

defineProps<{
  correction: KnowledgeCorrection
  disabled?: boolean
}>()

defineEmits<{
  approve: [correctionId: string]
  reject: [correctionId: string]
}>()
</script>

<template>
  <article class="grid gap-2 rounded-lg border p-3">
    <div class="flex flex-wrap items-center gap-2">
      <p class="text-sm font-medium">Correction</p>
      <StatusBadge :status="correction.status" />
      <span class="text-xs text-muted-foreground">
        {{ correction.visibilityScope === 'Company' ? 'Toàn công ty' : 'Theo thư mục' }}
        <template v-if="correction.approvedAt"> · duyệt lúc {{ formatDateTime(correction.approvedAt) }}</template>
      </span>
    </div>
    <p class="whitespace-pre-wrap text-sm">{{ correction.correctionText }}</p>
    <p v-if="correction.rejectReason" class="text-xs text-destructive">Lý do từ chối: {{ correction.rejectReason }}</p>
    <div v-if="correction.status === 'Draft'" class="flex flex-wrap gap-2">
      <Button size="sm" type="button" :disabled="disabled" @click="$emit('approve', correction.id)">
        <CheckIcon class="size-4" />
        Duyệt và lập chỉ mục
      </Button>
      <Button size="sm" variant="destructive" type="button" :disabled="disabled" @click="$emit('reject', correction.id)">
        <XIcon class="size-4" />
        Từ chối
      </Button>
    </div>
  </article>
</template>
