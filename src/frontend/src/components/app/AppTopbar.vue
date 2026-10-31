<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { LogOutIcon, MenuIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import ThemeToggle from '@/components/app/ThemeToggle.vue'
import { findNavItemByPath } from '@/components/app/navigation'
import { roleLabel, userInitials } from '@/components/app/user'
import { useAppShell } from '@/composables/useAppShell'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const { isMobileNavOpen } = useAppShell()

const navItem = computed(() => findNavItemByPath(route.path))
const pageTitle = computed(() => navItem.value?.label ?? String(route.meta.title ?? ''))

// Đăng xuất phải điều hướng về login vì trang login nằm ở layout riêng
async function handleLogout() {
  authStore.logout()
  await router.push({ name: 'login' })
}
</script>

<template>
  <header class="flex h-16 shrink-0 items-center gap-2.5 border-b bg-card px-6 max-[750px]:h-15.5 max-[750px]:px-3.5">
    <Button
      variant="outline"
      size="icon"
      class="hidden max-[750px]:inline-flex"
      aria-label="Mở menu điều hướng"
      :aria-expanded="isMobileNavOpen"
      @click="isMobileNavOpen = true"
    >
      <MenuIcon class="size-4.5" />
    </Button>
    <div class="min-w-0">
      <h1 class="truncate text-[15px] font-bold tracking-tight">{{ pageTitle }}</h1>
      <p v-if="navItem?.description" class="truncate text-[11px] text-muted-foreground">{{ navItem.description }}</p>
    </div>

    <div class="ml-auto flex items-center gap-2">
      <ThemeToggle />

      <DropdownMenu v-if="authStore.user">
        <DropdownMenuTrigger as-child>
          <Button variant="ghost" size="icon" class="rounded-full" aria-label="Menu tài khoản">
            <span class="grid size-8 place-items-center rounded-full border border-primary/20 bg-primary-soft text-xs font-extrabold text-primary-soft-foreground">
              {{ userInitials(authStore.user.displayName) }}
            </span>
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" class="w-56">
          <DropdownMenuLabel>
            <p class="truncate text-sm font-medium">{{ authStore.user.displayName }}</p>
            <p class="text-xs font-normal text-muted-foreground">{{ roleLabel(authStore.user.role) }}</p>
            <p class="truncate text-xs font-normal text-muted-foreground">Tenant: {{ authStore.tenantCode }}</p>
          </DropdownMenuLabel>
          <DropdownMenuSeparator />
          <DropdownMenuItem @click="handleLogout">
            <LogOutIcon class="size-4" />
            Đăng xuất
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  </header>
</template>
