<script setup lang="ts">
import { RouterView } from 'vue-router'
</script>

<template>
  <div class="flex min-h-screen flex-col items-center justify-center bg-background p-4 font-sans text-sm text-foreground antialiased">
    <div class="mb-6 flex items-center gap-2.5">
      <span class="grid size-9 place-items-center rounded-md bg-primary text-sm font-semibold text-primary-foreground">
        IK
      </span>
      <div>
        <p class="text-base font-semibold leading-tight">Knowledge Copilot</p>
        <p class="text-xs leading-tight text-muted-foreground">Nội bộ</p>
      </div>
    </div>
    <RouterView />
  </div>
</template>
