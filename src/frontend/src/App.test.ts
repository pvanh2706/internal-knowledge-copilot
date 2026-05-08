import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import App from './App.vue'
import router from './router'

describe('App', () => {
  it('renders the internal layout', async () => {
    router.push('/')
    await router.isReady()

    const wrapper = mount(App, {
      global: {
        plugins: [router],
      },
    })

    expect(wrapper.text()).toContain('Internal Knowledge Copilot')
    expect(wrapper.text()).toContain('Dashboard')
  })
})
