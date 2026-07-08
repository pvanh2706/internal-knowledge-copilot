<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { LogOutIcon } from '@lucide/vue'
import { Badge } from '@/components/ui/badge'
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
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const pageTitle = computed(() => findNavItemByPath(route.path)?.label ?? String(route.meta.title ?? ''))

const userInitial = computed(() => (authStore.user?.displayName ?? '?').charAt(0).toUpperCase())

const roleLabel = computed(() => {
  switch (authStore.user?.role) {
    case 'Admin':
      return 'Quản trị viên'
    case 'Reviewer':
      return 'Người duyệt'
    default:
      return 'Nhân viên'
  }
})

// Đăng xuất phải điều hướng về login vì trang login nằm ở layout riêng
async function handleLogout() {
  authStore.logout()
  await router.push({ name: 'login' })
}
</script>

<template>
  <header class="flex h-14 shrink-0 items-center gap-3 border-b px-4">
    <h1 class="truncate text-sm font-semibold">{{ pageTitle }}</h1>

    <div class="ml-auto flex items-center gap-2">
      <Badge variant="outline" class="max-sm:hidden">{{ authStore.tenantCode }}</Badge>
      <ThemeToggle />

      <DropdownMenu v-if="authStore.user">
        <DropdownMenuTrigger as-child>
          <Button variant="ghost" size="icon" class="rounded-full" aria-label="Menu tài khoản">
            <span class="grid size-7 place-items-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
              {{ userInitial }}
            </span>
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" class="w-56">
          <DropdownMenuLabel>
            <p class="truncate text-sm font-medium">{{ authStore.user.displayName }}</p>
            <p class="text-xs font-normal text-muted-foreground">{{ roleLabel }}</p>
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
