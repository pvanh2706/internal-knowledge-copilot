<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import { useAuthStore } from './stores/auth'

const authStore = useAuthStore()
</script>

<template>
  <div class="app-shell">
    <aside class="sidebar">
      <div class="brand">
        <span class="brand-mark">IK</span>
        <div>
          <strong>Knowledge Copilot</strong>
          <small>Nội bộ</small>
        </div>
      </div>

      <nav class="nav-list" aria-label="Điều hướng chính">
        <RouterLink to="/">Tổng quan</RouterLink>
        <RouterLink to="/documents">Tài liệu</RouterLink>
        <RouterLink to="/ai">Hỏi AI</RouterLink>
        <RouterLink v-if="authStore.isReviewer || authStore.isAdmin" to="/wiki">Wiki</RouterLink>
        <RouterLink v-if="authStore.isReviewer || authStore.isAdmin" to="/review">Duyệt tài liệu</RouterLink>
        <RouterLink v-if="authStore.isReviewer || authStore.isAdmin" to="/feedback">Phản hồi</RouterLink>
        <RouterLink v-if="authStore.isReviewer || authStore.isAdmin" to="/evaluation">Đánh giá</RouterLink>
        <RouterLink v-if="authStore.isReviewer || authStore.isAdmin" to="/retrieval-explain">Giải thích truy xuất</RouterLink>
        <RouterLink v-if="authStore.isReviewer || authStore.isAdmin" to="/knowledge-index">Kho tri thức</RouterLink>
        <RouterLink v-if="authStore.isReviewer || authStore.isAdmin" to="/admin/folders">Thư mục</RouterLink>
        <RouterLink v-if="authStore.isAdmin" to="/admin/users">Người dùng</RouterLink>
        <RouterLink v-if="authStore.isAdmin" to="/admin/teams">Team</RouterLink>
        <RouterLink v-if="authStore.isAdmin" to="/admin/ai-settings">Cấu hình AI</RouterLink>
        <RouterLink v-if="authStore.isAdmin" to="/admin/audit-logs">Nhật ký</RouterLink>
      </nav>
    </aside>

    <main class="main-content">
      <header class="topbar">
        <div>
          <h1>Internal Knowledge Copilot</h1>
          <p>Quản lý tri thức, hỏi đáp AI và chuẩn hóa wiki nội bộ.</p>
        </div>
        <button v-if="authStore.isAuthenticated" class="login-link" type="button" @click="authStore.logout()">
          Đăng xuất
        </button>
        <RouterLink v-else class="login-link" to="/login">Đăng nhập</RouterLink>
      </header>

      <RouterView />
    </main>
  </div>
</template>
