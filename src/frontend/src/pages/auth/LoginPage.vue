<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const email = ref('')
const password = ref('')
const errorMessage = ref('')
const isSubmitting = ref(false)

// Hiện thông báo khi bị chuyển về từ xử lý 401 tập trung (query ?expired=1)
const sessionExpired = computed(() => route.query.expired === '1')

async function submitLogin() {
  errorMessage.value = ''
  isSubmitting.value = true

  try {
    const response = await authStore.login(email.value, password.value)
    await router.push(response.mustChangePassword ? '/change-password' : '/')
  } catch (error) {
    errorMessage.value = error instanceof ApiError ? error.message : 'Không thể đăng nhập.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <section class="auth-panel">
    <form class="login-form" @submit.prevent="submitLogin">
      <h2>Đăng nhập</h2>

      <p v-if="sessionExpired" class="form-error">Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.</p>

      <label>
        Tenant code
        <input
          v-model="authStore.tenantCode"
          type="text"
          autocomplete="organization"
          required
          @change="authStore.setTenantCode(authStore.tenantCode)"
        />
      </label>

      <label>
        Email
        <input v-model="email" type="email" autocomplete="email" required />
      </label>

      <label>
        Mật khẩu
        <input v-model="password" type="password" autocomplete="current-password" required />
      </label>

      <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>

      <button type="submit" :disabled="isSubmitting">
        {{ isSubmitting ? 'Đang đăng nhập...' : 'Đăng nhập' }}
      </button>
    </form>
  </section>
</template>
