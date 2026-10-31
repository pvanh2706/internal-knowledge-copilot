<script setup lang="ts">
import type { HTMLAttributes } from 'vue'
import { cn } from '@/lib/utils'

// Card B1: viền + bóng rất nhẹ, header (tiêu đề + badge/hành động) và footer tùy chọn.
// Dùng cho bảng (table card) và các khối nội dung — body không padding sẵn để bảng tràn mép.
const props = defineProps<{
  title?: string
  /** id gắn cho tiêu đề — dùng cho aria-labelledby của vùng chứa */
  titleId?: string
  as?: 'section' | 'aside' | 'div'
  class?: HTMLAttributes['class']
}>()
</script>

<template>
  <component
    :is="props.as ?? 'section'"
    :class="cn('min-w-0 overflow-hidden rounded-xl border bg-card text-card-foreground shadow-xs', props.class)"
  >
    <header v-if="title || $slots.actions" class="flex flex-wrap items-center justify-between gap-2.5 border-b px-4.5 py-3.5">
      <h2 v-if="title" :id="titleId" class="text-sm font-bold">{{ title }}</h2>
      <div v-if="$slots.actions" class="flex flex-wrap items-center gap-2">
        <slot name="actions" />
      </div>
    </header>
    <slot />
    <footer v-if="$slots.footer" class="flex flex-wrap items-center justify-between gap-3 border-t px-4 py-3 text-[11px] text-muted-foreground">
      <slot name="footer" />
    </footer>
  </component>
</template>
