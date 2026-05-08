<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const email = ref('admin@example.local')
const password = ref('ChangeMe123!')
const errorMessage = ref('')
const isSubmitting = ref(false)

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
