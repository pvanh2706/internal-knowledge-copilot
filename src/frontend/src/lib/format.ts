// Định dạng ngày giờ hiển thị thống nhất toàn app (thay các bản formatDate lặp ở từng trang)
export function formatDateTime(value?: string | null): string {
  if (!value) {
    return '—'
  }
  return new Date(value).toLocaleString('vi-VN')
}

export function formatDate(value?: string | null): string {
  if (!value) {
    return '—'
  }
  return new Date(value).toLocaleDateString('vi-VN')
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
