<script setup lang="ts">
import { XIcon } from '@lucide/vue'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'

// Panel chi tiết master-detail B1: nhãn badge, nút bỏ chọn, tiêu đề + mô tả, rồi nội dung
defineProps<{
  /** Nhãn badge viết hoa phía trên tiêu đề, vd: "CHI TIẾT TÀI LIỆU" */
  label: string
  title: string
  description?: string
  /** Hiện nút X bỏ chọn (emit close) */
  closable?: boolean
  closeLabel?: string
}>()

defineEmits<{
  close: []
}>()
</script>

<template>
  <aside class="min-w-0 rounded-xl border bg-card p-4.5 text-card-foreground shadow-xs">
    <div class="mb-3.5 border-b pb-3.5">
      <div class="flex items-center justify-between gap-2">
        <Badge variant="soft" class="uppercase tracking-wide">{{ label }}</Badge>
        <div class="ml-auto flex items-center gap-2">
          <slot name="meta" />
          <Button
            v-if="closable"
            variant="ghost"
            size="icon-sm"
            type="button"
            :aria-label="closeLabel ?? 'Bỏ chọn'"
            @click="$emit('close')"
          >
            <XIcon class="size-4" />
          </Button>
        </div>
      </div>
      <h2 class="mt-1.5 mb-0.5 text-[15px] font-extrabold tracking-tight break-words">{{ title }}</h2>
      <p v-if="description" class="text-xs text-muted-foreground break-words">{{ description }}</p>
    </div>
    <slot />
  </aside>
</template>
