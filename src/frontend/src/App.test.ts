import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { describe, expect, it } from 'vitest'
import App from './App.vue'
import router from './router'

describe('App', () => {
  it('renders the internal layout', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    router.push('/')
    await router.isReady()

    const wrapper = mount(App, {
      global: {
        plugins: [pinia, router],
      },
    })

    expect(wrapper.text()).toContain('Internal Knowledge Copilot')
    expect(wrapper.text()).toContain('Tổng quan')
  })
})
