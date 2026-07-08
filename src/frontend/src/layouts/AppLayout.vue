<script setup lang="ts">
import { RouterView } from 'vue-router'
import AppSidebar from '@/components/app/AppSidebar.vue'
import AppTopbar from '@/components/app/AppTopbar.vue'
import LlmWarningBanner from '@/components/app/LlmWarningBanner.vue'
</script>

<template>
  <!-- Wrapper bg/text bắt buộc: style.css cũ (unlayered) set màu trên :root sẽ đè theme nếu chỉ dựa @layer base -->
  <div class="flex min-h-screen bg-background font-sans text-sm text-foreground antialiased">
    <AppSidebar />
    <div class="flex min-h-screen min-w-0 flex-1 flex-col">
      <AppTopbar />
      <LlmWarningBanner />
      <main class="min-w-0 flex-1 p-4 md:p-6">
        <RouterView />
      </main>
    </div>
  </div>
</template>
