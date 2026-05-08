import { createRouter, createWebHistory } from 'vue-router'
import DashboardPage from '../pages/DashboardPage.vue'
import TeamManagementPage from '../pages/admin/TeamManagementPage.vue'
import UserManagementPage from '../pages/admin/UserManagementPage.vue'
import ChangePasswordPage from '../pages/auth/ChangePasswordPage.vue'
import LoginPage from '../pages/auth/LoginPage.vue'
import PlaceholderPage from '../pages/PlaceholderPage.vue'
import { useAuthStore } from '../stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'dashboard', component: DashboardPage, meta: { requiresAuth: true } },
    { path: '/login', name: 'login', component: LoginPage },
    { path: '/change-password', name: 'change-password', component: ChangePasswordPage, meta: { requiresAuth: true } },
    {
      path: '/documents',
      name: 'documents',
      component: PlaceholderPage,
      meta: { title: 'Tài liệu', requiresAuth: true },
    },
    {
      path: '/ai',
      name: 'ai',
      component: PlaceholderPage,
      meta: { title: 'Hỏi AI', requiresAuth: true },
    },
    {
      path: '/wiki',
      name: 'wiki',
      component: PlaceholderPage,
      meta: { title: 'Wiki', requiresAuth: true },
    },
    {
      path: '/review',
      name: 'review',
      component: PlaceholderPage,
      meta: { title: 'Review', requiresAuth: true },
    },
    {
      path: '/admin/users',
      name: 'admin-users',
      component: UserManagementPage,
      meta: { requiresAuth: true, requiresAdmin: true },
    },
    {
      path: '/admin/teams',
      name: 'admin-teams',
      component: TeamManagementPage,
      meta: { requiresAuth: true, requiresAdmin: true },
    },
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

  return true
})

export default router
