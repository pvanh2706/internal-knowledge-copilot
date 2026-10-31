import type { UserRole } from '@/api/auth'

// Hiển thị thông tin người dùng đăng nhập — chỉ mang tính trình bày, quyền thật do backend kiểm tra
export function roleLabel(role?: UserRole | null): string {
  switch (role) {
    case 'Admin':
      return 'Quản trị viên'
    case 'Reviewer':
      return 'Người duyệt'
    default:
      return 'Nhân viên'
  }
}

// Chữ viết tắt cho avatar: chữ đầu của từ đầu + từ cuối ("Nguyễn Văn An" → "NA")
export function userInitials(displayName?: string | null): string {
  const words = (displayName ?? '').trim().split(/\s+/).filter(Boolean)
  if (words.length === 0) return '?'
  const first = words[0].charAt(0)
  const last = words.length > 1 ? words[words.length - 1].charAt(0) : ''
  return (first + last).toUpperCase()
}
