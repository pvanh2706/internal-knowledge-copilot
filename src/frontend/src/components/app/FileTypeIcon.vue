<script setup lang="ts">
import { computed } from 'vue'
import { FileTextIcon } from '@lucide/vue'

// Ô nhỏ hiển thị loại file theo đuôi tên file thật (PDF đỏ, Word xanh, còn lại trung tính).
// Không có tên file (vd: dòng danh sách tài liệu — API list không trả loại file) thì hiện icon chung.
const props = defineProps<{
  fileName?: string | null
}>()

const extension = computed(() => {
  const match = props.fileName?.match(/\.([a-z0-9]{1,8})$/i)
  return match ? match[1].toUpperCase() : null
})

const toneClass = computed(() => {
  switch (extension.value) {
    case 'PDF':
      return 'bg-destructive/10 text-destructive'
    case 'DOC':
    case 'DOCX':
      return 'bg-primary-soft text-primary-soft-foreground'
    case null:
      return 'bg-primary-soft text-primary-soft-foreground'
    default:
      return 'bg-muted text-muted-foreground'
  }
})
</script>

<template>
  <span
    class="grid size-7 shrink-0 place-items-center rounded-md text-[9px] font-extrabold tracking-tight"
    :class="toneClass"
    aria-hidden="true"
  >
    <template v-if="extension">{{ extension.slice(0, 4) }}</template>
    <FileTextIcon v-else class="size-3.5" />
  </span>
</template>
