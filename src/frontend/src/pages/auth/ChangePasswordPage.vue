<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { changePassword } from '@/api/auth'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import { useToast } from '@/composables/useToast'

const router = useRouter()
const authStore = useAuthStore()
const toast = useToast()

const currentPassword = ref('')
const newPassword = ref('')
const errorMessage = ref('')
const isSubmitting = ref(false)

async function submitChangePassword() {
  if (!authStore.accessToken) {
    return
  }

  errorMessage.value = ''
  isSubmitting.value = true

  try {
    await changePassword(currentPassword.value, newPassword.value, authStore.accessToken)
    toast.success('Đã đổi mật khẩu.')
    await router.push('/')
  } catch (error) {
    errorMessage.value = error instanceof ApiError ? error.message : 'Không thể đổi mật khẩu.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <Card class="w-full max-w-sm">
    <CardHeader>
      <CardTitle class="text-lg">Đổi mật khẩu</CardTitle>
      <CardDescription>Đặt mật khẩu mới trước khi tiếp tục sử dụng hệ thống.</CardDescription>
    </CardHeader>
    <CardContent>
      <form class="grid gap-4" @submit.prevent="submitChangePassword">
        <div class="grid gap-1.5">
          <Label for="current-password">Mật khẩu hiện tại</Label>
          <Input
            id="current-password"
            v-model="currentPassword"
            type="password"
            autocomplete="current-password"
            required
          />
        </div>

        <div class="grid gap-1.5">
          <Label for="new-password">Mật khẩu mới</Label>
          <Input id="new-password" v-model="newPassword" type="password" autocomplete="new-password" required />
        </div>

        <p
          v-if="errorMessage"
          class="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive"
          role="alert"
        >
          {{ errorMessage }}
        </p>

        <Button type="submit" :disabled="isSubmitting">
          {{ isSubmitting ? 'Đang lưu...' : 'Lưu mật khẩu' }}
        </Button>
      </form>
    </CardContent>
  </Card>
</template>
