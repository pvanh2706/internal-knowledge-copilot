import { readonly, ref } from 'vue'

export interface ConfirmOptions {
  title: string
  description?: string
  confirmText?: string
  cancelText?: string
  destructive?: boolean
}

// State module-level: 1 host ConfirmDialog duy nhất đặt ở App.vue phục vụ toàn app
const isOpen = ref(false)
const options = ref<ConfirmOptions>({ title: '' })
let resolver: ((confirmed: boolean) => void) | null = null

// Xác nhận dạng promise — bắt buộc cho mọi hành động phá hủy (xóa, archive, reject, reset...)
export function useConfirm() {
  function confirm(opts: ConfirmOptions): Promise<boolean> {
    options.value = opts
    isOpen.value = true
    return new Promise((resolve) => {
      resolver = resolve
    })
  }

  return { confirm }
}

// Dành riêng cho component host ConfirmDialog
export function useConfirmHost() {
  function settle(confirmed: boolean) {
    isOpen.value = false
    resolver?.(confirmed)
    resolver = null
  }

  return { isOpen: readonly(isOpen), options: readonly(options), settle }
}
