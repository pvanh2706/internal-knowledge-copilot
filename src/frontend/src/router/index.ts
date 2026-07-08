import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

// Layout khai báo qua meta.layout, App.vue switch component theo giá trị này.
// (Phương án nested 3 record cha cùng path '/' bị matcher vue-router khớp nhầm record cha —
// đây là phương án dự phòng chính thức của kế hoạch redesign, xem mục 5.3.)
export type LayoutName = 'app' | 'auth'

declare module 'vue-router' {
  interface RouteMeta {
    layout?: LayoutName
  }
}

// Route lazy-load để tách bundle theo trang
const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'dashboard', component: () => import('../pages/DashboardPage.vue'), meta: { requiresAuth: true, layout: 'app' } },
    { path: '/login', name: 'login', component: () => import('../pages/auth/LoginPage.vue'), meta: { layout: 'auth' } },
    { path: '/change-password', name: 'change-password', component: () => import('../pages/auth/ChangePasswordPage.vue'), meta: { requiresAuth: true, layout: 'auth' } },
    { path: '/documents', name: 'documents', component: () => import('../pages/documents/DocumentListPage.vue'), meta: { requiresAuth: true, layout: 'app' } },
    { path: '/ai', name: 'ai', component: () => import('../pages/ai/AiQuestionPage.vue'), meta: { requiresAuth: true, layout: 'app' } },
    { path: '/wiki', name: 'wiki', component: () => import('../pages/wiki/WikiDraftPage.vue'), meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/review', name: 'review', component: () => import('../pages/review/DocumentReviewPage.vue'), meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/feedback', name: 'feedback', component: () => import('../pages/review/FeedbackReviewPage.vue'), meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/evaluation', name: 'evaluation', component: () => import('../pages/review/EvaluationPage.vue'), meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/retrieval-explain', name: 'retrieval-explain', component: () => import('../pages/review/RetrievalExplainPage.vue'), meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/knowledge-index', name: 'knowledge-index', component: () => import('../pages/review/KnowledgeIndexPage.vue'), meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/knowledge-sources', name: 'knowledge-sources', component: () => import('../pages/review/KnowledgeSourcePage.vue'), meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/workflow/recommendations', name: 'workflow-recommendations', component: () => import('../pages/workflow/RecommendationListPage.vue'), meta: { requiresAuth: true, layout: 'app' } },
    { path: '/workflow/actions', name: 'workflow-actions', component: () => import('../pages/workflow/ActionApprovalQueuePage.vue'), meta: { requiresAuth: true, layout: 'app' } },
    { path: '/admin/tenants', name: 'admin-tenants', component: () => import('../pages/admin/TenantManagementPage.vue'), meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
    { path: '/admin/applications', name: 'admin-applications', component: () => import('../pages/admin/ApplicationManagementPage.vue'), meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
    { path: '/admin/integrations', name: 'admin-integrations', component: () => import('../pages/admin/IntegrationManagementPage.vue'), meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
    { path: '/admin/users', name: 'admin-users', component: () => import('../pages/admin/UserManagementPage.vue'), meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
    { path: '/admin/teams', name: 'admin-teams', component: () => import('../pages/admin/TeamManagementPage.vue'), meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
    { path: '/admin/folders', name: 'admin-folders', component: () => import('../pages/admin/FolderManagementPage.vue'), meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/admin/ai-settings', name: 'admin-ai-settings', component: () => import('../pages/admin/AiSettingsPage.vue'), meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
    { path: '/admin/data-reset', name: 'admin-data-reset', component: () => import('../pages/admin/DataResetPage.vue'), meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
    { path: '/admin/audit-logs', name: 'admin-audit-logs', component: () => import('../pages/admin/AuditLogPage.vue'), meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
  ],
})

router.beforeEach((to) => {
  const authStore = useAuthStore()
  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    return { name: 'login' }
  }

  if (to.meta.requiresAdmin && !authStore.isAdmin) {
    return { name: 'dashboard' }
  }

  if (to.meta.requiresReviewer && !authStore.isAdmin && !authStore.isReviewer) {
    return { name: 'dashboard' }
  }

  return true
})

export default router
