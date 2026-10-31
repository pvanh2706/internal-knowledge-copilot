<script setup lang="ts">
import { computed } from 'vue'
import { statusInfo, type StatusVariant } from './status'

const props = defineProps<{
  status: string
  /** Ghi đè label tự map khi cần hiển thị text khác */
  label?: string
}>()

// Kiểu soft B1: pill nền alpha 0.10 (dark 0.18), chữ cùng token, không viền ở light — viền mờ ở dark để tách nền
const variantClasses: Record<StatusVariant, string> = {
  success: 'bg-success/10 text-success dark:border-success/30 dark:bg-success/18',
  warning: 'bg-warning/10 text-warning dark:border-warning/30 dark:bg-warning/18',
  destructive: 'bg-destructive/10 text-destructive dark:border-destructive/30 dark:bg-destructive/18',
  info: 'bg-info/10 text-info dark:border-info/30 dark:bg-info/18',
  muted: 'bg-muted text-muted-foreground dark:border-border',
}

const info = computed(() => statusInfo(props.status))
</script>

<template>
  <span
    class="inline-flex items-center whitespace-nowrap rounded-full border border-transparent px-2 py-0.5 text-[11px] font-semibold leading-4"
    :class="variantClasses[info.variant]"
  >
    {{ label ?? info.label }}
  </span>
</template>
