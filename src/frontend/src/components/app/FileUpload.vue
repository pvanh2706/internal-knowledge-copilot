<script setup lang="ts">
import { ref } from 'vue'
import { UploadIcon, XIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'

defineProps<{
  accept?: string
  inputId?: string
}>()

const model = defineModel<File | null>({ default: null })
const inputRef = ref<HTMLInputElement | null>(null)

function handleChange(event: Event) {
  model.value = (event.target as HTMLInputElement).files?.[0] ?? null
}

function clearFile() {
  model.value = null
  if (inputRef.value) {
    inputRef.value.value = ''
  }
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

defineExpose({ clear: clearFile })
</script>

<template>
  <div>
    <label
      v-if="!model"
      class="flex cursor-pointer flex-col items-center justify-center gap-1.5 rounded-md border border-dashed px-4 py-6 text-center transition-colors hover:bg-accent has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-ring"
    >
      <UploadIcon class="size-5 text-muted-foreground" aria-hidden="true" />
      <span class="text-sm text-muted-foreground">Chọn file để tải lên</span>
      <span v-if="accept" class="text-xs text-muted-foreground/70">{{ accept }}</span>
      <input
        :id="inputId"
        ref="inputRef"
        type="file"
        class="sr-only"
        :accept="accept"
        @change="handleChange"
      />
    </label>
    <div v-else class="flex items-center justify-between gap-3 rounded-md border px-3 py-2 text-sm">
      <span class="min-w-0 truncate">{{ model.name }}</span>
      <span class="shrink-0 text-xs text-muted-foreground">{{ formatSize(model.size) }}</span>
      <Button variant="ghost" size="icon-sm" type="button" aria-label="Bỏ chọn file" @click="clearFile">
        <XIcon class="size-4" />
      </Button>
    </div>
  </div>
</template>
