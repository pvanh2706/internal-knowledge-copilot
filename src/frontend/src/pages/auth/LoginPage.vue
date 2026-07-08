<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'

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
  <Card class="w-full max-w-sm">
    <CardHeader>
      <CardTitle class="text-lg">Đăng nhập</CardTitle>
      <CardDescription>Truy cập kho tri thức nội bộ của công ty bạn.</CardDescription>
    </CardHeader>
    <CardContent>
      <form class="grid gap-4" @submit.prevent="submitLogin">
        <p
          v-if="sessionExpired"
          class="rounded-md border border-warning/30 bg-warning/10 p-3 text-sm"
          role="status"
        >
          Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.
        </p>

        <div class="grid gap-1.5">
          <Label for="login-tenant">Mã tenant</Label>
          <Input
            id="login-tenant"
            v-model="authStore.tenantCode"
            type="text"
            autocomplete="organization"
            required
            @change="authStore.setTenantCode(authStore.tenantCode)"
          />
        </div>

        <div class="grid gap-1.5">
          <Label for="login-email">Email</Label>
          <Input id="login-email" v-model="email" type="email" autocomplete="email" required />
        </div>

        <div class="grid gap-1.5">
          <Label for="login-password">Mật khẩu</Label>
          <Input id="login-password" v-model="password" type="password" autocomplete="current-password" required />
        </div>

        <p
          v-if="errorMessage"
          class="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive"
          role="alert"
        >
          {{ errorMessage }}
        </p>

        <Button type="submit" :disabled="isSubmitting">
          {{ isSubmitting ? 'Đang đăng nhập...' : 'Đăng nhập' }}
        </Button>
      </form>
    </CardContent>
  </Card>
</template>
