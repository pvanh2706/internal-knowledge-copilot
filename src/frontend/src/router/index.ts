import { createRouter, createWebHistory } from 'vue-router'
import DashboardPage from '../pages/DashboardPage.vue'
import ActionApprovalQueuePage from '../pages/workflow/ActionApprovalQueuePage.vue'
import AuditLogPage from '../pages/admin/AuditLogPage.vue'
import AiSettingsPage from '../pages/admin/AiSettingsPage.vue'
import ApplicationManagementPage from '../pages/admin/ApplicationManagementPage.vue'
import DataResetPage from '../pages/admin/DataResetPage.vue'
import FolderManagementPage from '../pages/admin/FolderManagementPage.vue'
import IntegrationManagementPage from '../pages/admin/IntegrationManagementPage.vue'
import TeamManagementPage from '../pages/admin/TeamManagementPage.vue'
import TenantManagementPage from '../pages/admin/TenantManagementPage.vue'
import UserManagementPage from '../pages/admin/UserManagementPage.vue'
import ChangePasswordPage from '../pages/auth/ChangePasswordPage.vue'
import AiQuestionPage from '../pages/ai/AiQuestionPage.vue'
import LoginPage from '../pages/auth/LoginPage.vue'
import DocumentListPage from '../pages/documents/DocumentListPage.vue'
import DocumentReviewPage from '../pages/review/DocumentReviewPage.vue'
import EvaluationPage from '../pages/review/EvaluationPage.vue'
import FeedbackReviewPage from '../pages/review/FeedbackReviewPage.vue'
import KnowledgeIndexPage from '../pages/review/KnowledgeIndexPage.vue'
import KnowledgeSourcePage from '../pages/review/KnowledgeSourcePage.vue'
import RecommendationListPage from '../pages/workflow/RecommendationListPage.vue'
import RetrievalExplainPage from '../pages/review/RetrievalExplainPage.vue'
import WikiDraftPage from '../pages/wiki/WikiDraftPage.vue'
import { useAuthStore } from '../stores/auth'

// Layout khai báo qua meta.layout, App.vue switch component theo giá trị này.
// (Phương án nested 3 record cha cùng path '/' bị matcher vue-router khớp nhầm record cha —
// đây là phương án dự phòng chính thức của kế hoạch redesign, xem mục 5.3.)
// Migrate 1 trang = đổi meta.layout của trang đó từ 'legacy' sang 'app'.
export type LayoutName = 'app' | 'auth' | 'legacy'

declare module 'vue-router' {
  interface RouteMeta {
    layout?: LayoutName
  }
}

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'dashboard', component: DashboardPage, meta: { requiresAuth: true, layout: 'app' } },
    { path: '/login', name: 'login', component: LoginPage, meta: { layout: 'auth' } },
    { path: '/change-password', name: 'change-password', component: ChangePasswordPage, meta: { requiresAuth: true, layout: 'auth' } },
    { path: '/documents', name: 'documents', component: DocumentListPage, meta: { requiresAuth: true, layout: 'app' } },
    { path: '/ai', name: 'ai', component: AiQuestionPage, meta: { requiresAuth: true, layout: 'app' } },
    { path: '/wiki', name: 'wiki', component: WikiDraftPage, meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/review', name: 'review', component: DocumentReviewPage, meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/feedback', name: 'feedback', component: FeedbackReviewPage, meta: { requiresAuth: true, requiresReviewer: true, layout: 'legacy' } },
    { path: '/evaluation', name: 'evaluation', component: EvaluationPage, meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/retrieval-explain', name: 'retrieval-explain', component: RetrievalExplainPage, meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/knowledge-index', name: 'knowledge-index', component: KnowledgeIndexPage, meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/knowledge-sources', name: 'knowledge-sources', component: KnowledgeSourcePage, meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/workflow/recommendations', name: 'workflow-recommendations', component: RecommendationListPage, meta: { requiresAuth: true, layout: 'app' } },
    { path: '/workflow/actions', name: 'workflow-actions', component: ActionApprovalQueuePage, meta: { requiresAuth: true, layout: 'app' } },
    { path: '/admin/tenants', name: 'admin-tenants', component: TenantManagementPage, meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
    { path: '/admin/applications', name: 'admin-applications', component: ApplicationManagementPage, meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
    { path: '/admin/integrations', name: 'admin-integrations', component: IntegrationManagementPage, meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
    { path: '/admin/users', name: 'admin-users', component: UserManagementPage, meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
    { path: '/admin/teams', name: 'admin-teams', component: TeamManagementPage, meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
    { path: '/admin/folders', name: 'admin-folders', component: FolderManagementPage, meta: { requiresAuth: true, requiresReviewer: true, layout: 'app' } },
    { path: '/admin/ai-settings', name: 'admin-ai-settings', component: AiSettingsPage, meta: { requiresAuth: true, requiresAdmin: true, layout: 'legacy' } },
    { path: '/admin/data-reset', name: 'admin-data-reset', component: DataResetPage, meta: { requiresAuth: true, requiresAdmin: true, layout: 'legacy' } },
    { path: '/admin/audit-logs', name: 'admin-audit-logs', component: AuditLogPage, meta: { requiresAuth: true, requiresAdmin: true, layout: 'app' } },
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
