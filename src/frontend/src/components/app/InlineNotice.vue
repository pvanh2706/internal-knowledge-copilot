<script setup lang="ts">
import { computed, type FunctionalComponent } from 'vue'
import { CircleAlertIcon, InfoIcon, TriangleAlertIcon } from '@lucide/vue'

// Khối ghi chú B1 (.notice xanh / .warn vàng) + biến thể danger cho mâu thuẫn/lỗi
const props = withDefaults(
  defineProps<{
    tone?: 'info' | 'warning' | 'danger'
    title?: string
    icon?: FunctionalComponent | null
  }>(),
  { tone: 'info', title: undefined, icon: undefined },
)

const toneClasses = {
  info: 'border-primary/20 bg-primary-soft/60 text-foreground [--notice-icon:hsl(var(--primary-soft-foreground))]',
  warning: 'border-warning/25 bg-warning/8 text-foreground [--notice-icon:hsl(var(--warning))]',
  danger: 'border-destructive/25 bg-destructive/8 text-foreground [--notice-icon:hsl(var(--destructive))]',
}

const defaultIcons = { info: InfoIcon, warning: TriangleAlertIcon, danger: CircleAlertIcon }

const iconComponent = computed(() => (props.icon === null ? null : (props.icon ?? defaultIcons[props.tone])))
</script>

<template>
  <div class="flex items-start gap-2.5 rounded-lg border px-3 py-2.5 text-xs leading-relaxed" :class="toneClasses[tone]">
    <component :is="iconComponent" v-if="iconComponent" class="mt-0.5 size-4 shrink-0 text-(--notice-icon)" aria-hidden="true" />
    <div class="min-w-0 flex-1">
      <p v-if="title" class="font-semibold">{{ title }}</p>
      <div :class="title ? 'mt-0.5 text-muted-foreground' : 'text-muted-foreground'">
        <slot />
      </div>
    </div>
  </div>
</template>
