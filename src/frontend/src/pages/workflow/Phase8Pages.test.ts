import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { describe, expect, it } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'
import TenantManagementPage from '../admin/TenantManagementPage.vue'
import KnowledgeSourcePage from '../review/KnowledgeSourcePage.vue'
import ActionApprovalQueuePage from './ActionApprovalQueuePage.vue'
import RecommendationListPage from './RecommendationListPage.vue'

describe('Phase 8 pages', () => {
  it('renders recommendation and action workflow pages', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const router = createTestRouter()
    router.push('/workflow/recommendations')
    await router.isReady()

    const recommendations = mount(RecommendationListPage, {
      global: {
        plugins: [pinia, router],
      },
    })
    const actions = mount(ActionApprovalQueuePage, {
      global: {
        plugins: [pinia, router],
      },
    })

    expect(recommendations.text()).toContain('Gợi ý quy trình')
    expect(actions.text()).toContain('Hàng đợi hành động')
  })

  it('renders admin and review platform pages', () => {
    const pinia = createPinia()
    setActivePinia(pinia)

    const tenants = mount(TenantManagementPage, {
      global: {
        plugins: [pinia],
      },
    })
    const sources = mount(KnowledgeSourcePage, {
      global: {
        plugins: [pinia],
      },
    })

    expect(tenants.text()).toContain('Quản trị tenant')
    expect(sources.text()).toContain('Nguồn tri thức')
  })
})

function createTestRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/workflow/recommendations', component: RecommendationListPage },
      { path: '/workflow/actions', component: ActionApprovalQueuePage },
    ],
  })
}
