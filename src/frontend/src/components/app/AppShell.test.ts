import { enableAutoUnmount, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import type { UserRole } from '@/api/auth'
import { createTestRouter, setupSession } from '@/test/session'
import AppSidebar from './AppSidebar.vue'
import AppTopbar from './AppTopbar.vue'
import { useAppShell } from '@/composables/useAppShell'

async function mountWithRole(component: typeof AppSidebar | typeof AppTopbar, role: UserRole, path = '/documents') {
  const pinia = setupSession(role)
  const router = createTestRouter()
  await router.push(path)
  await router.isReady()
  return mount(component, { global: { plugins: [pinia, router] }, attachTo: document.body })
}

// Unmount sau mỗi test trước khi dọn DOM — tránh component cũ còn phản ứng với state module-level
enableAutoUnmount(afterEach)

describe('AppSidebar (B1)', () => {
  afterEach(() => {
    localStorage.clear()
    useAppShell().isMobileNavOpen.value = false
  })

  it('ẩn mục cần quyền reviewer/admin với nhân viên — giữ nguyên quy tắc hiển thị theo role', async () => {
    const wrapper = await mountWithRole(AppSidebar, 'User')
    const links = wrapper.findAll('nav a').map((link) => link.text())
    expect(links).toContain('AI Workspace')
    expect(links).toContain('Tài liệu')
    expect(links).not.toContain('Duyệt tài liệu')
    expect(links).not.toContain('Wiki')
    expect(links).not.toContain('Người dùng')
    expect(links).not.toContain('Tenant')
  })

  it('reviewer thấy nhóm chất lượng nhưng không thấy mục quản trị', async () => {
    const wrapper = await mountWithRole(AppSidebar, 'Reviewer')
    const links = wrapper.findAll('nav a').map((link) => link.text())
    expect(links).toContain('Duyệt tài liệu')
    expect(links).toContain('Wiki')
    expect(links).not.toContain('Người dùng')
    expect(links).not.toContain('Reset dữ liệu')
  })

  it('admin thấy mục quản trị; tenant chỉ hiển thị, không có nút chuyển tenant', async () => {
    const wrapper = await mountWithRole(AppSidebar, 'Admin')
    expect(wrapper.text()).toContain('Người dùng')
    expect(wrapper.text()).toContain('acme')
    // Khu vực tenant không phải control tương tác
    expect(wrapper.find('[title="Tenant của phiên đăng nhập hiện tại"] button').exists()).toBe(false)
  })

  it('đánh dấu mục đang mở bằng aria-current', async () => {
    const wrapper = await mountWithRole(AppSidebar, 'User', '/documents')
    const active = wrapper.find('a[aria-current="page"]')
    expect(active.text()).toContain('Tài liệu')
  })
})

describe('AppTopbar (B1)', () => {
  afterEach(() => {
    localStorage.clear()
    useAppShell().isMobileNavOpen.value = false
  })

  it('hiện tiêu đề + mô tả trang và nút mở menu mobile', async () => {
    const wrapper = await mountWithRole(AppTopbar, 'User', '/ai')
    expect(wrapper.text()).toContain('AI Workspace')
    expect(wrapper.text()).toContain('Tra cứu và nhận câu trả lời có dẫn chứng')

    const menuButton = wrapper.get('button[aria-label="Mở menu điều hướng"]')
    expect(menuButton.attributes('aria-expanded')).toBe('false')
    await menuButton.trigger('click')
    expect(useAppShell().isMobileNavOpen.value).toBe(true)
  })
})
