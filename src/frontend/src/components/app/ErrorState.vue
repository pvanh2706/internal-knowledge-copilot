<script setup lang="ts">
defineProps<{
  message: string
  /** Ẩn nút "Thử lại" cho lỗi không tải lại được */
  hideRetry?: boolean
}>()

defineEmits<{
  retry: []
}>()
</script>

<template>
  <div
    class="flex flex-wrap items-center justify-between gap-3 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive"
    role="alert"
  >
    <p class="min-w-0">{{ message }}</p>
    <button
      v-if="!hideRetry"
      type="button"
      class="shrink-0 rounded-md border border-destructive/40 px-3 py-1 text-xs font-medium outline-none transition-colors hover:bg-destructive/10 focus-visible:ring-2 focus-visible:ring-ring"
      @click="$emit('retry')"
    >
      Thử lại
    </button>
  </div>
</template>
