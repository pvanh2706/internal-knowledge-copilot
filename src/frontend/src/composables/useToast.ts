import { toast } from 'vue-sonner'

// Toast thống nhất toàn app (vue-sonner, bottom-right, tự đóng 5s — cấu hình ở <Toaster/> trong App.vue).
// Thay cho pattern successMessage/errorMessage ref cũ.
export function useToast() {
  return {
    success: (message: string, description?: string) => toast.success(message, { description }),
    error: (message: string, description?: string) => toast.error(message, { description }),
    info: (message: string, description?: string) => toast.info(message, { description }),
    warning: (message: string, description?: string) => toast.warning(message, { description }),
  }
}
