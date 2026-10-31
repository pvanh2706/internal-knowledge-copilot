<script setup lang="ts">
import { watch } from 'vue'
import { useRoute } from 'vue-router'
import { useMediaQuery } from '@vueuse/core'
import { Sheet, SheetContent, SheetDescription, SheetTitle } from '@/components/ui/sheet'
import { useAppShell } from '@/composables/useAppShell'
import AppSidebarContent from './AppSidebarContent.vue'

// Sidebar B1: cố định trên desktop (232px, thu 190px ≤1100px); ≤750px thành overlay mở từ nút menu
// trên topbar — dùng Sheet (reka Dialog) để có focus trap, Esc và click ngoài để đóng.
const route = useRoute()
const { isMobileNavOpen } = useAppShell()
const isNarrow = useMediaQuery('(max-width: 750px)')

watch(() => route.fullPath, () => {
  isMobileNavOpen.value = false
})

watch(isNarrow, (narrow) => {
  if (!narrow) isMobileNavOpen.value = false
})
</script>

<template>
  <aside class="flex h-full w-58 shrink-0 flex-col border-r bg-sidebar max-[1100px]:w-47.5 max-[750px]:hidden">
    <AppSidebarContent />
  </aside>

  <Sheet v-model:open="isMobileNavOpen">
    <SheetContent side="left" class="w-60 gap-0 bg-sidebar p-0 sm:max-w-60">
      <SheetTitle class="sr-only">Điều hướng</SheetTitle>
      <SheetDescription class="sr-only">Menu điều hướng chính của ứng dụng</SheetDescription>
      <AppSidebarContent />
    </SheetContent>
  </Sheet>
</template>
