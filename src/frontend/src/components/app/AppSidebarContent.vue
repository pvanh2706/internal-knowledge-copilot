<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { ShieldCheckIcon, SparklesIcon } from '@lucide/vue'
import { useAuthStore } from '@/stores/auth'
import { navGroups, type NavItem } from './navigation'
import { roleLabel, userInitials } from './user'

// Nội dung sidebar B1 (brand, tenant hiện tại, menu, người dùng) — dùng chung cho
// sidebar desktop và sidebar overlay trên mobile trong AppSidebar
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
  <div class="flex items-center gap-3 border-b px-4.5 pt-6 pb-5">
    <span class="grid size-9.5 shrink-0 place-items-center rounded-xl bg-linear-to-br from-primary to-primary-hover text-primary-foreground shadow-md shadow-primary/20">
      <SparklesIcon class="size-5" aria-hidden="true" />
    </span>
    <div class="min-w-0">
      <p class="truncate text-sm font-extrabold tracking-tight">Enterprise Copilot</p>
      <p class="text-[10px] leading-snug text-muted-foreground">Không gian tri thức doanh nghiệp</p>
    </div>
  </div>

  <!-- Chỉ hiển thị tenant của phiên hiện tại — không có chức năng chuyển tenant (ranh giới tenant do backend quyết định) -->
  <div
    class="mx-3.5 mt-4 mb-1 flex items-center gap-2 rounded-lg border bg-card px-3 py-2 text-xs font-semibold"
    title="Tenant của phiên đăng nhập hiện tại"
  >
    <ShieldCheckIcon class="size-4 shrink-0 text-primary-soft-foreground" aria-hidden="true" />
    <span class="sr-only">Tenant:</span>
    <span class="truncate uppercase">{{ authStore.tenantCode }}</span>
  </div>

  <nav class="min-h-0 flex-1 overflow-y-auto px-2.5 pb-3" aria-label="Điều hướng chính">
    <div v-for="group in visibleGroups" :key="group.label">
      <p class="px-3 pt-4 pb-1.5 text-[10px] font-extrabold uppercase tracking-widest text-muted-foreground">
        {{ group.label }}
      </p>
      <ul class="grid gap-0.5">
        <li v-for="item in group.items" :key="item.to">
          <RouterLink
            :to="item.to"
            class="flex min-h-10 items-center gap-3 rounded-lg px-3 py-2 text-[13px] font-medium text-foreground/75 outline-none transition-colors hover:bg-accent hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
            exact-active-class="bg-primary-soft font-bold text-primary-soft-foreground! hover:bg-primary-soft"
          >
            <component :is="item.icon" class="size-4.5 shrink-0" aria-hidden="true" />
            <span class="truncate">{{ item.label }}</span>
          </RouterLink>
        </li>
      </ul>
    </div>
  </nav>

  <div v-if="authStore.user" class="flex items-center gap-2.5 border-t px-3.5 py-4">
    <span class="grid size-8.5 shrink-0 place-items-center rounded-full border border-primary/20 bg-primary-soft text-xs font-extrabold text-primary-soft-foreground" aria-hidden="true">
      {{ userInitials(authStore.user.displayName) }}
    </span>
    <div class="min-w-0">
      <p class="truncate text-[13px] font-bold">{{ authStore.user.displayName }}</p>
      <p class="truncate text-[11px] text-muted-foreground">{{ roleLabel(authStore.user.role) }}</p>
    </div>
  </div>
</template>
