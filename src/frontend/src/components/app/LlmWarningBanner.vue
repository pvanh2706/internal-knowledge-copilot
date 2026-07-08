<script setup lang="ts">
import { ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { TriangleAlertIcon, XIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { getAiProviderConfigurationStatus } from '@/api/aiSettings'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()

const showWarning = ref(false)
const providerName = ref('')

async function checkLlmApiKey() {
  if (!authStore.accessToken) {
    return
  }

  try {
    const status = await getAiProviderConfigurationStatus(authStore.accessToken)
    if (!status.hasLlmApiKey || status.usesMockLlmProvider) {
      providerName.value = status.providerName
      showWarning.value = true
    }
  } catch {
    // Không chặn đăng nhập nếu API kiểm tra cấu hình không khả dụng
  }
}

watch(
  () => authStore.loginCompletedAt,
  (loginCompletedAt) => {
    if (loginCompletedAt) {
      void checkLlmApiKey()
    }
  },
  { immediate: true },
)
</script>

<template>
  <div
    v-if="showWarning"
    class="flex items-center gap-2.5 border-b border-warning/30 bg-warning/10 px-4 py-2 text-sm text-foreground"
    role="status"
  >
    <TriangleAlertIcon class="size-4 shrink-0 text-warning" aria-hidden="true" />
    <p class="min-w-0 flex-1">
      Chưa phát hiện API key cho LLM ({{ providerName }}). Câu trả lời AI có thể chỉ là dữ liệu mô phỏng.
      <RouterLink
        v-if="authStore.isAdmin"
        to="/admin/ai-settings"
        class="font-medium text-primary underline-offset-4 hover:underline"
        @click="showWarning = false"
      >
        Cấu hình ngay
      </RouterLink>
    </p>
    <Button variant="ghost" size="icon-sm" aria-label="Đóng cảnh báo" @click="showWarning = false">
      <XIcon class="size-4" />
    </Button>
  </div>
</template>
