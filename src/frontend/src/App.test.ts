import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, describe, expect, it } from 'vitest'
import App from './App.vue'
import router, { type LayoutName } from './router'
import AppLayout from './layouts/AppLayout.vue'
import AuthLayout from './layouts/AuthLayout.vue'
import { accessTokenStorageKey, userStorageKey } from './api/http'

// Cặp (route name, layout) kỳ vọng — cập nhật khi migrate trang từ 'legacy' sang 'app'
const expectedLayoutByRoute: Record<string, LayoutName> = {
  'login': 'auth',
  'change-password': 'auth',
  'dashboard': 'app',
  'documents': 'legacy',
  'ai': 'legacy',
  'wiki': 'legacy',
  'review': 'legacy',
  'feedback': 'legacy',
  'evaluation': 'legacy',
  'retrieval-explain': 'legacy',
  'knowledge-index': 'legacy',
  'knowledge-sources': 'legacy',
  'workflow-recommendations': 'legacy',
  'workflow-actions': 'legacy',
  'admin-tenants': 'app',
  'admin-applications': 'app',
  'admin-integrations': 'app',
  'admin-users': 'app',
  'admin-teams': 'app',
  'admin-folders': 'legacy',
  'admin-ai-settings': 'legacy',
  'admin-data-reset': 'legacy',
  'admin-audit-logs': 'app',
}

describe('router layouts', () => {
  it('mỗi route name resolve về đúng layout khai báo trong meta', () => {
    const allRouteNames = router
      .getRoutes()
      .map((record) => String(record.name))
      .sort()
    expect(allRouteNames).toEqual(Object.keys(expectedLayoutByRoute).sort())

    for (const [name, layout] of Object.entries(expectedLayoutByRoute)) {
      const resolved = router.resolve({ name })
      expect(resolved.meta.layout, `route ${name} sai layout`).toBe(layout)
    }
  })

  it('resolve "/" theo path về dashboard', () => {
    const resolved = router.resolve('/')
    expect(resolved.name).toBe('dashboard')
    expect(resolved.meta.layout).toBe('app')
  })

  it('resolve "/login" theo path về layout auth', () => {
    const resolved = router.resolve('/login')
    expect(resolved.name).toBe('login')
    expect(resolved.meta.layout).toBe('auth')
  })
})

describe('App', () => {
  afterEach(() => {
    localStorage.clear()
  })

  it('render AppLayout khi vào dashboard', async () => {
    localStorage.setItem(accessTokenStorageKey, 'token')
    localStorage.setItem(userStorageKey, JSON.stringify({ id: '1', displayName: 'Test', role: 'Admin' }))
    const pinia = createPinia()
    setActivePinia(pinia)

    await router.push('/')
    await router.isReady()

    const wrapper = mount(App, {
      global: {
        plugins: [pinia, router],
      },
    })

    expect(wrapper.findComponent(AppLayout).exists()).toBe(true)
    expect(wrapper.text()).toContain('Knowledge Copilot')
    expect(wrapper.text()).toContain('Tổng quan')
  })

  it('render AuthLayout khi vào trang đăng nhập', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)

    await router.push('/login')
    await router.isReady()

    const wrapper = mount(App, {
      global: {
        plugins: [pinia, router],
      },
    })

    expect(wrapper.findComponent(AuthLayout).exists()).toBe(true)
    expect(wrapper.text()).toContain('Đăng nhập')
  })
})
