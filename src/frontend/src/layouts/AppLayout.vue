<script setup lang="ts">
import { RouterView } from 'vue-router'
import AppSidebar from '@/components/app/AppSidebar.vue'
import AppTopbar from '@/components/app/AppTopbar.vue'
import LlmWarningBanner from '@/components/app/LlmWarningBanner.vue'
import { useAppShell } from '@/composables/useAppShell'

// Khung B1: sidebar + (topbar, nội dung cuộn) chiếm đúng chiều cao viewport.
// Trang full-bleed (AI Workspace) tự chia cột và tự cuộn bên trong nên <main> bỏ padding.
const { isFullBleed } = useAppShell()
</script>

<template>
  <div class="flex h-dvh overflow-hidden bg-surface font-sans text-sm text-foreground antialiased">
    <AppSidebar />
    <div class="flex min-w-0 flex-1 flex-col">
      <AppTopbar />
      <LlmWarningBanner />
      <main
        class="min-h-0 flex-1 overflow-y-auto"
        :class="isFullBleed ? 'flex flex-col' : 'px-4 py-5 md:px-7 md:py-6.5'"
      >
        <RouterView />
      </main>
    </div>
  </div>
</template>
