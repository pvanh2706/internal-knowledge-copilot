// Định dạng ngày giờ hiển thị thống nhất toàn app (thay các bản formatDate lặp ở từng trang)
export function formatDateTime(value?: string | null): string {
  if (!value) {
    return '—'
  }
  return new Date(value).toLocaleString('vi-VN')
}
