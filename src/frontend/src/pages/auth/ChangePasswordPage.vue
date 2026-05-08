<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { changePassword } from '../../api/auth'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const currentPassword = ref('')
const newPassword = ref('')
const message = ref('')
const errorMessage = ref('')

async function submitChangePassword() {
  if (!authStore.accessToken) {
    return
  }

  message.value = ''
  errorMessage.value = ''

  try {
    await changePassword(currentPassword.value, newPassword.value, authStore.accessToken)
    message.value = 'Đã đổi mật khẩu.'
    await router.push('/')
  } catch (error) {
    errorMessage.value = error instanceof ApiError ? error.message : 'Không thể đổi mật khẩu.'
  }
}
</script>

<template>
  <section class="auth-panel">
    <form class="login-form" @submit.prevent="submitChangePassword">
      <h2>Đổi mật khẩu</h2>

      <label>
        Mật khẩu hiện tại
        <input v-model="currentPassword" type="password" autocomplete="current-password" required />
      </label>

      <label>
        Mật khẩu mới
        <input v-model="newPassword" type="password" autocomplete="new-password" required />
      </label>

      <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
      <p v-if="message" class="form-success">{{ message }}</p>

      <button type="submit">Lưu mật khẩu</button>
    </form>
  </section>
</template>
