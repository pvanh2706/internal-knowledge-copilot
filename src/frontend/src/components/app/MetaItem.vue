<script setup lang="ts">
defineProps<{
  label: string
}>()
</script>

<template>
  <div class="min-w-0 rounded-lg border bg-muted/60 p-2.5">
    <dt class="text-[10px] text-muted-foreground">{{ label }}</dt>
    <dd class="mt-1 text-xs font-semibold break-words">
      <slot />
    </dd>
  </div>
</template>
