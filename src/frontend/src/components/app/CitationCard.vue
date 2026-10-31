<script setup lang="ts">
import { computed } from 'vue'
import { BookOpenIcon, FileTextIcon, PenLineIcon } from '@lucide/vue'

const props = defineProps<{
  /** Số thứ tự citation, hiển thị dạng [n] */
  index: number
  sourceType: string
  title: string
  folderPath?: string | null
  sectionTitle?: string | null
  excerpt: string
  /** Dòng metadata phụ (score/distance...) hiển thị text-xs font-mono */
  meta?: string
}>()

const sourceIcon = computed(() => {
  switch (props.sourceType) {
    case 'Wiki':
      return BookOpenIcon
    case 'Correction':
      return PenLineIcon
    default:
      return FileTextIcon
  }
})

const sourceTypeLabel = computed(() => {
  switch (props.sourceType) {
    case 'Document':
      return 'Tài liệu'
    case 'Correction':
      return 'Hiệu chỉnh'
    default:
      return props.sourceType
  }
})

const pathLine = computed(
  () => [props.folderPath, props.sectionTitle].filter(Boolean).join(' / ') || '—',
)
</script>

<template>
  <!-- Thẻ nguồn B1. Không click-through: contract citation chưa có định danh/điều hướng tới nguồn
       (source viewer trong backlog) — prototype chỉ tô sáng, production không giả lập deep-link -->
  <article class="flex gap-2.5 rounded-[10px] border bg-card p-3 text-card-foreground">
    <span
      class="grid size-6.5 shrink-0 place-items-center rounded-md bg-primary-soft text-xs font-extrabold tabular-nums text-primary-soft-foreground"
      aria-hidden="true"
    >
      {{ index }}
    </span>
    <div class="grid min-w-0 flex-1 content-start gap-1">
      <p class="line-clamp-2 text-xs font-bold break-words">
        <span class="sr-only">Nguồn {{ index }}: </span>{{ title }}
      </p>
      <p class="flex min-w-0 items-center gap-1 text-[10px] text-muted-foreground">
        <component :is="sourceIcon" class="size-3 shrink-0" aria-hidden="true" />
        <span class="shrink-0">{{ sourceTypeLabel }}</span>
        <span aria-hidden="true">·</span>
        <span class="truncate">{{ pathLine }}</span>
      </p>
      <p v-if="meta" class="font-mono text-[10px] text-muted-foreground">{{ meta }}</p>
      <p class="line-clamp-4 text-[11px] leading-relaxed text-muted-foreground">“{{ excerpt }}”</p>
    </div>
  </article>
</template>
