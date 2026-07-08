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

const pathLine = computed(
  () => [props.folderPath, props.sectionTitle].filter(Boolean).join(' / ') || '—',
)
</script>

<template>
  <!-- V1 không click-through: chưa có API xem nội dung nguồn (source viewer trong backlog) -->
  <article class="flex gap-3 rounded-lg border bg-card p-3 text-card-foreground">
    <span
      class="grid size-6 shrink-0 place-items-center rounded-md bg-primary/12 text-xs font-semibold tabular-nums text-primary dark:bg-primary/18"
      aria-hidden="true"
    >
      {{ index }}
    </span>
    <div class="grid min-w-0 flex-1 content-start gap-1">
      <p class="flex items-center gap-1.5 text-sm font-medium">
        <component :is="sourceIcon" class="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
        <span class="truncate">{{ title }}</span>
      </p>
      <p class="truncate font-mono text-xs text-muted-foreground">{{ pathLine }}</p>
      <p v-if="meta" class="font-mono text-xs text-muted-foreground">{{ meta }}</p>
      <p class="line-clamp-3 text-sm text-muted-foreground">{{ excerpt }}</p>
    </div>
  </article>
</template>
