<script setup lang="ts">
defineProps<{
  label: string
  value: string | number
  hint?: string
}>()
</script>

<template>
  <article class="rounded-lg border bg-card p-4 text-card-foreground">
    <p class="text-sm text-muted-foreground">{{ label }}</p>
    <p class="mt-1.5 text-3xl font-semibold tabular-nums">{{ value }}</p>
    <p v-if="hint" class="mt-1 text-xs text-muted-foreground">{{ hint }}</p>
  </article>
</template>
