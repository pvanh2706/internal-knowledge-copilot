<script setup lang="ts">
defineProps<{
  title: string
  description?: string
  /** Nhãn nhỏ viết hoa phía trên tiêu đề (B1 eyebrow), vd: "KNOWLEDGE MANAGEMENT" */
  eyebrow?: string
}>()
</script>

<template>
  <div class="mb-5 flex flex-wrap items-start justify-between gap-4">
    <div class="min-w-0">
      <p v-if="eyebrow" class="mb-1 text-[10px] font-extrabold uppercase tracking-[0.11em] text-primary-soft-foreground">
        {{ eyebrow }}
      </p>
      <h1 class="text-2xl font-bold tracking-tight">{{ title }}</h1>
      <p v-if="description" class="mt-1 text-sm text-muted-foreground">{{ description }}</p>
    </div>
    <div v-if="$slots.actions" class="flex shrink-0 flex-wrap items-center gap-2">
      <slot name="actions" />
    </div>
  </div>
</template>
