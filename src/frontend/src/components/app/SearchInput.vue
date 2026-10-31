<script setup lang="ts">
import { SearchIcon } from '@lucide/vue'
import { Input } from '@/components/ui/input'

// Ô tìm kiếm B1: icon kính lúp bên trái, chiếm phần rộng của thanh công cụ
defineProps<{
  placeholder?: string
  /** Nhãn đọc cho trình đọc màn hình (ô tìm kiếm không có label hiển thị) */
  label: string
  inputId?: string
}>()

const model = defineModel<string>({ default: '' })
</script>

<template>
  <div class="relative min-w-48 flex-1">
    <SearchIcon class="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
    <Input
      :id="inputId"
      v-model="model"
      type="search"
      class="h-10 bg-card pl-9"
      :placeholder="placeholder"
      :aria-label="label"
    />
  </div>
</template>
