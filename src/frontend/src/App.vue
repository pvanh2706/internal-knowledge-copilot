<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { Toaster } from '@/components/ui/sonner'
import ConfirmDialog from '@/components/app/ConfirmDialog.vue'
import AppLayout from '@/layouts/AppLayout.vue'
import AuthLayout from '@/layouts/AuthLayout.vue'
import LegacyLayout from '@/layouts/LegacyLayout.vue'
import type { LayoutName } from '@/router'

const route = useRoute()

// Switch layout theo meta.layout (xem ghi chú trong router/index.ts);
// mỗi layout tự chứa <RouterView/> để render trang khớp route.
const layouts: Record<LayoutName, unknown> = {
  app: AppLayout,
  auth: AuthLayout,
  legacy: LegacyLayout,
}

const layout = computed(() => layouts[route.meta.layout ?? 'legacy'])
</script>

<template>
  <component :is="layout" />
  <Toaster position="bottom-right" :duration="5000" />
  <ConfirmDialog />
</template>
