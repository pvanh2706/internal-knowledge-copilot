<script setup lang="ts">
import type { FunctionalComponent } from 'vue'
import { InboxIcon } from '@lucide/vue'

withDefaults(
  defineProps<{
    title: string
    description?: string
    icon?: FunctionalComponent
  }>(),
  { icon: undefined, description: undefined },
)
</script>

<template>
  <!-- State block B1: khung nét đứt nền nhạt, icon màu primary — dùng cho trống/chưa chọn/không có quyền/lỗi -->
  <div class="flex flex-col items-center justify-center gap-1.5 rounded-xl border border-dashed border-input bg-surface px-5 py-8 text-center">
    <component :is="icon ?? InboxIcon" class="mb-1 size-7 text-primary/80" aria-hidden="true" />
    <p class="text-[15px] font-bold tracking-tight">{{ title }}</p>
    <p v-if="description" class="max-w-md text-xs text-muted-foreground">{{ description }}</p>
    <div v-if="$slots.action" class="mt-2">
      <slot name="action" />
    </div>
  </div>
</template>
