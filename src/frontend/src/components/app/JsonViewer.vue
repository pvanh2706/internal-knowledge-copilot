<script setup lang="ts">
import { computed } from 'vue'
import { CopyIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { useToast } from '@/composables/useToast'

// Khối JSON scroll + copy — dùng cho các cột/khối JSON của ActionQueue, KnowledgeSource...
const props = defineProps<{
  title: string
  /** Chuỗi JSON thô từ API; null/rỗng hiển thị "—" */
  value?: string | null
}>()

const toast = useToast()

const prettyValue = computed(() => {
  if (!props.value) return null
  try {
    return JSON.stringify(JSON.parse(props.value), null, 2)
  } catch {
    return props.value
  }
})

async function copyValue() {
  if (!prettyValue.value) return
  try {
    await navigator.clipboard.writeText(prettyValue.value)
    toast.success('Đã sao chép JSON.')
  } catch {
    toast.error('Không thể sao chép.')
  }
}
</script>

<template>
  <div class="grid min-w-0 gap-1.5">
    <div class="flex items-center justify-between gap-2">
      <p class="text-sm font-medium">{{ title }}</p>
      <Button
        v-if="prettyValue"
        variant="ghost"
        size="icon-sm"
        type="button"
        :aria-label="`Sao chép ${title}`"
        @click="copyValue"
      >
        <CopyIcon class="size-3.5" />
      </Button>
    </div>
    <pre
      v-if="prettyValue"
      class="max-h-64 overflow-auto rounded-md border bg-muted/50 p-3 font-mono text-xs leading-5"
    >{{ prettyValue }}</pre>
    <p v-else class="text-sm text-muted-foreground">—</p>
  </div>
</template>
