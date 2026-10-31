import { ApiError } from '@/api/http'

// Message mặc định của http.ts khi response lỗi không có body JSON (vd: Forbid() trả 403 rỗng)
const genericApiMessage = 'Không thể xử lý yêu cầu.'

export function isForbiddenError(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403
}

// Chuyển lỗi gọi API thành câu hiển thị: ưu tiên message của máy chủ; 403 không kèm message
// thì nói rõ là thiếu quyền; lỗi mạng (fetch ném TypeError) thì báo mất kết nối thay vì "Failed to fetch"
export function errorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    if (error.status === 403 && error.message === genericApiMessage) {
      return 'Bạn không có quyền thực hiện thao tác này.'
    }
    return error.message
  }
  if (error instanceof TypeError) {
    return 'Không thể kết nối tới máy chủ. Vui lòng kiểm tra kết nối và thử lại.'
  }
  return error instanceof Error ? error.message : fallback
}
