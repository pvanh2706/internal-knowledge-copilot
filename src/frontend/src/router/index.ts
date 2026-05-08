import { createRouter, createWebHistory } from 'vue-router'
import DashboardPage from '../pages/DashboardPage.vue'
import LoginPage from '../pages/auth/LoginPage.vue'
import PlaceholderPage from '../pages/PlaceholderPage.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'dashboard', component: DashboardPage },
    { path: '/login', name: 'login', component: LoginPage },
    {
      path: '/documents',
      name: 'documents',
      component: PlaceholderPage,
      meta: { title: 'Tài liệu' },
    },
    {
      path: '/ai',
      name: 'ai',
      component: PlaceholderPage,
      meta: { title: 'Hỏi AI' },
    },
    {
      path: '/wiki',
      name: 'wiki',
      component: PlaceholderPage,
      meta: { title: 'Wiki' },
    },
    {
      path: '/review',
      name: 'review',
      component: PlaceholderPage,
      meta: { title: 'Review' },
    },
    {
      path: '/admin/users',
      name: 'admin-users',
      component: PlaceholderPage,
      meta: { title: 'Quản trị người dùng' },
    },
  ],
})

export default router
