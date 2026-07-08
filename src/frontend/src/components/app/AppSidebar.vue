<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { navGroups, type NavItem } from './navigation'

const authStore = useAuthStore()

function canSee(item: NavItem): boolean {
  if (item.requiresAdmin && !authStore.isAdmin) {
    return false
  }
  if (item.requiresReviewer && !authStore.isAdmin && !authStore.isReviewer) {
    return false
  }
  return true
}

const visibleGroups = computed(() =>
  navGroups
    .map((group) => ({ ...group, items: group.items.filter(canSee) }))
    .filter((group) => group.items.length > 0),
)
</script>

<template>
  <aside class="flex h-screen w-64 shrink-0 flex-col border-r bg-background max-lg:hidden">
    <div class="flex h-14 items-center gap-2.5 border-b px-4">
      <span class="grid size-8 shrink-0 place-items-center rounded-md bg-primary text-sm font-semibold text-primary-foreground">
        IK
      </span>
      <div class="min-w-0">
        <p class="truncate text-sm font-semibold leading-tight">Knowledge Copilot</p>
        <p class="text-xs leading-tight text-muted-foreground">Nội bộ</p>
      </div>
    </div>

    <nav class="flex-1 overflow-y-auto p-3" aria-label="Điều hướng chính">
      <div v-for="group in visibleGroups" :key="group.label" class="mb-4 last:mb-0">
        <p class="px-3 pb-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
          {{ group.label }}
        </p>
        <ul class="grid gap-0.5">
          <li v-for="item in group.items" :key="item.to">
            <RouterLink
              :to="item.to"
              class="relative flex min-h-9 items-center gap-2.5 rounded-md px-3 py-2 text-sm text-foreground/80 outline-none transition-colors hover:bg-accent hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
              exact-active-class="bg-accent font-medium text-foreground before:absolute before:inset-y-2 before:left-0 before:w-0.5 before:rounded-full before:bg-primary"
            >
              <component :is="item.icon" class="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
              {{ item.label }}
            </RouterLink>
          </li>
        </ul>
      </div>
    </nav>
  </aside>
</template>
