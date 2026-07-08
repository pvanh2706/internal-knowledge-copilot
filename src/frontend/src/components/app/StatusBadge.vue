<script setup lang="ts">
import { computed } from 'vue'
import { statusInfo, type StatusVariant } from './status'

const props = defineProps<{
  status: string
  /** Ghi đè label tự map khi cần hiển thị text khác */
  label?: string
}>()

// Kiểu soft: nền alpha 0.12 (dark 0.18), chữ + border cùng token — không nền đặc
const variantClasses: Record<StatusVariant, string> = {
  success: 'border-success/30 bg-success/12 text-success dark:bg-success/18',
  warning: 'border-warning/30 bg-warning/12 text-warning dark:bg-warning/18',
  destructive: 'border-destructive/30 bg-destructive/12 text-destructive dark:bg-destructive/18',
  info: 'border-info/30 bg-info/12 text-info dark:bg-info/18',
  muted: 'border-border bg-muted text-muted-foreground',
}

const info = computed(() => statusInfo(props.status))
</script>

<template>
  <span
    class="inline-flex items-center whitespace-nowrap rounded-full border px-2 py-0.5 text-xs font-medium"
    :class="variantClasses[info.variant]"
  >
    {{ label ?? info.label }}
  </span>
</template>
