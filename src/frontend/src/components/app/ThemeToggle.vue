<script setup lang="ts">
import { MonitorIcon, MoonIcon, SunIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useTheme, type ThemeMode } from '@/composables/useTheme'

const { theme } = useTheme()

const modes: Array<{ value: ThemeMode; label: string }> = [
  { value: 'light', label: 'Sáng' },
  { value: 'dark', label: 'Tối' },
  { value: 'system', label: 'Theo hệ thống' },
]
</script>

<template>
  <DropdownMenu>
    <DropdownMenuTrigger as-child>
      <Button variant="outline" size="icon" class="text-muted-foreground" aria-label="Đổi giao diện sáng tối">
        <SunIcon class="size-4 dark:hidden" />
        <MoonIcon class="hidden size-4 dark:block" />
      </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent align="end">
      <DropdownMenuItem
        v-for="mode in modes"
        :key="mode.value"
        :class="theme === mode.value ? 'bg-accent' : ''"
        @click="theme = mode.value"
      >
        <SunIcon v-if="mode.value === 'light'" class="size-4" />
        <MoonIcon v-else-if="mode.value === 'dark'" class="size-4" />
        <MonitorIcon v-else class="size-4" />
        {{ mode.label }}
      </DropdownMenuItem>
    </DropdownMenuContent>
  </DropdownMenu>
</template>
