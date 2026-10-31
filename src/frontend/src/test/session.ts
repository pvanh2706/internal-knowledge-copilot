import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { UserRole } from '@/api/auth'
import { accessTokenStorageKey, tenantCodeStorageKey, userStorageKey } from '@/api/http'

export const TEST_TOKEN = 'test-token'

// Phiên đăng nhập giả lập cho test component: auth store đọc token/user từ localStorage khi khởi tạo
export function setupSession(role: UserRole = 'User') {
  localStorage.setItem(accessTokenStorageKey, TEST_TOKEN)
  localStorage.setItem(userStorageKey, JSON.stringify({ id: 'u1', displayName: 'Nguyễn Văn An', role }))
  localStorage.setItem(tenantCodeStorageKey, 'acme')
  const pinia = createPinia()
  setActivePinia(pinia)
  return pinia
}

// Router tối thiểu có đủ route name mà các trang dùng trong RouterLink
export function createTestRouter() {
  const Empty = { template: '<div />' }
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'dashboard', component: Empty },
      { path: '/ai', name: 'ai', component: Empty },
      { path: '/documents', name: 'documents', component: Empty },
      { path: '/review', name: 'review', component: Empty },
      { path: '/login', name: 'login', component: Empty },
    ],
  })
}
