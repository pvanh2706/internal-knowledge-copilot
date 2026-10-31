import { onBeforeUnmount, ref } from 'vue'

// State module-level của khung AppLayout: menu điều hướng mobile (sidebar dạng overlay)
// và chế độ "full-bleed" cho trang tự quản lý bố cục toàn chiều cao (AI Workspace 3 cột).
const isMobileNavOpen = ref(false)
const isFullBleed = ref(false)

export function useAppShell() {
  return { isMobileNavOpen, isFullBleed }
}

// Trang gọi trong setup để bỏ padding/scroll mặc định của <main>; tự trả lại khi rời trang
export function useFullBleedLayout() {
  isFullBleed.value = true
  onBeforeUnmount(() => {
    isFullBleed.value = false
  })
}
