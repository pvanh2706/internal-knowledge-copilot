import { useColorMode } from '@vueuse/core'
import { computed } from 'vue'

export type ThemeMode = 'light' | 'dark' | 'system'

// 3 chế độ Sáng/Tối/Theo hệ thống — key localStorage `theme`, mặc định theo hệ thống.
// Inline script trong index.html đọc cùng key này để set class `dark` trước khi Vue mount (chống FOUC).
export function useTheme() {
  const colorMode = useColorMode({
    storageKey: 'theme',
    emitAuto: true,
    disableTransition: true,
  })

  const theme = computed<ThemeMode>({
    get: () => (colorMode.value === 'auto' ? 'system' : (colorMode.value as ThemeMode)),
    set: (value) => {
      colorMode.value = value === 'system' ? 'auto' : value
    },
  })

  return { theme }
}
