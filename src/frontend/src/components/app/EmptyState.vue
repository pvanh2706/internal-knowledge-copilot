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
  <div class="flex flex-col items-center justify-center gap-2 rounded-lg border border-dashed px-6 py-10 text-center">
    <component :is="icon ?? InboxIcon" class="size-10 text-muted-foreground/50" aria-hidden="true" />
    <p class="text-sm font-medium">{{ title }}</p>
    <p v-if="description" class="text-sm text-muted-foreground">{{ description }}</p>
    <div v-if="$slots.action" class="mt-2">
      <slot name="action" />
    </div>
  </div>
</template>
