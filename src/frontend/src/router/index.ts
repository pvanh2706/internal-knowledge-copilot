import { createRouter, createWebHistory } from 'vue-router'
import DashboardPage from '../pages/DashboardPage.vue'
import FolderManagementPage from '../pages/admin/FolderManagementPage.vue'
import TeamManagementPage from '../pages/admin/TeamManagementPage.vue'
import UserManagementPage from '../pages/admin/UserManagementPage.vue'
import ChangePasswordPage from '../pages/auth/ChangePasswordPage.vue'
import AiQuestionPage from '../pages/ai/AiQuestionPage.vue'
import LoginPage from '../pages/auth/LoginPage.vue'
import DocumentListPage from '../pages/documents/DocumentListPage.vue'
import PlaceholderPage from '../pages/PlaceholderPage.vue'
import DocumentReviewPage from '../pages/review/DocumentReviewPage.vue'
import FeedbackReviewPage from '../pages/review/FeedbackReviewPage.vue'
import { useAuthStore } from '../stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'dashboard', component: DashboardPage, meta: { requiresAuth: true } },
    { path: '/login', name: 'login', component: LoginPage },
    { path: '/change-password', name: 'change-password', component: ChangePasswordPage, meta: { requiresAuth: true } },
    { path: '/documents', name: 'documents', component: DocumentListPage, meta: { requiresAuth: true } },
    { path: '/ai', name: 'ai', component: AiQuestionPage, meta: { requiresAuth: true } },
    { path: '/wiki', name: 'wiki', component: PlaceholderPage, meta: { title: 'Wiki', requiresAuth: true } },
    { path: '/review', name: 'review', component: DocumentReviewPage, meta: { requiresAuth: true, requiresReviewer: true } },
    { path: '/feedback', name: 'feedback', component: FeedbackReviewPage, meta: { requiresAuth: true, requiresReviewer: true } },
    { path: '/admin/users', name: 'admin-users', component: UserManagementPage, meta: { requiresAuth: true, requiresAdmin: true } },
    { path: '/admin/teams', name: 'admin-teams', component: TeamManagementPage, meta: { requiresAuth: true, requiresAdmin: true } },
    { path: '/admin/folders', name: 'admin-folders', component: FolderManagementPage, meta: { requiresAuth: true, requiresReviewer: true } },
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
