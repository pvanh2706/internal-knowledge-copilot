<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
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
        <RouterLink to="/">Dashboard</RouterLink>
        <RouterLink to="/documents">Tài liệu</RouterLink>
        <RouterLink to="/ai">Hỏi AI</RouterLink>
        <RouterLink to="/wiki">Wiki</RouterLink>
        <RouterLink to="/review">Review</RouterLink>
        <RouterLink to="/admin/users">Quản trị</RouterLink>
      </nav>
    </aside>

    <main class="main-content">
      <header class="topbar">
        <div>
          <h1>Internal Knowledge Copilot</h1>
          <p>Quản lý tri thức, hỏi đáp AI và chuẩn hóa wiki nội bộ.</p>
        </div>
        <RouterLink class="login-link" to="/login">Đăng nhập</RouterLink>
      </header>

      <RouterView />
    </main>
  </div>
</template>
