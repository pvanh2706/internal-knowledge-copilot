<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { TriangleAlertIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import ErrorState from '@/components/app/ErrorState.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import { getDataResetStatus, resetData, type DataResetResult, type DataResetStatus } from '@/api/dataReset'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import { useToast } from '@/composables/useToast'
import { formatDateTime } from '@/lib/format'

const authStore = useAuthStore()
const toast = useToast()

const status = ref<DataResetStatus | null>(null)
const result = ref<DataResetResult | null>(null)
const confirmationPhrase = ref('')
const resetStorage = ref(true)
const resetVectorStore = ref(true)
const isLoading = ref(false)
const isResetting = ref(false)
const loadError = ref('')

// Pattern DangerZone: nút chỉ mở khóa khi gõ đúng confirmation phrase
const canSubmit = computed(() => {
  return Boolean(
    status.value?.enabled && confirmationPhrase.value === status.value.confirmationPhrase && !isResetting.value,
  )
})

async function loadStatus() {
  if (!authStore.accessToken) return
  isLoading.value = true
  loadError.value = ''

  try {
    status.value = await getDataResetStatus(authStore.accessToken)
  } catch (error) {
    loadError.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tải trạng thái reset.'
  } finally {
    isLoading.value = false
  }
}

async function submitReset() {
  if (!authStore.accessToken || !canSubmit.value) return
  isResetting.value = true
  result.value = null

  try {
    result.value = await resetData(authStore.accessToken, {
      confirmationPhrase: confirmationPhrase.value,
      resetStorage: resetStorage.value,
      resetVectorStore: resetVectorStore.value,
    })
    confirmationPhrase.value = ''
    toast.success('Đã reset dữ liệu test.')
  } catch (error) {
    toast.error(error instanceof ApiError || error instanceof Error ? error.message : 'Không thể reset dữ liệu.')
  } finally {
    isResetting.value = false
  }
}

onMounted(loadStatus)
</script>

<template>
  <div class="mx-auto max-w-3xl">
    <PageHeader
      title="Reset dữ liệu"
      description="Xóa dữ liệu nghiệp vụ, file upload và vector index sau khi test xong."
    />

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadStatus" />

    <Skeleton v-if="isLoading && !status" class="h-64 rounded-lg" />

    <Card v-else-if="status" class="border-destructive/40">
      <CardHeader>
        <CardTitle class="flex items-center gap-2 text-lg text-destructive">
          <TriangleAlertIcon class="size-5" aria-hidden="true" />
          Vùng nguy hiểm
        </CardTitle>
        <CardDescription>
          Hành động này xóa dữ liệu nghiệp vụ và không thể hoàn tác. Chỉ dùng trên môi trường test.
        </CardDescription>
      </CardHeader>
      <CardContent class="grid gap-5">
        <dl class="grid gap-1.5 text-sm">
          <div class="flex items-center gap-2">
            <dt class="w-36 shrink-0 text-muted-foreground">Trạng thái</dt>
            <dd>
              <StatusBadge
                :status="status.enabled ? 'Active' : 'Disabled'"
                :label="status.enabled ? 'Đã bật' : 'Đang tắt'"
              />
            </dd>
          </div>
          <div class="flex gap-2">
            <dt class="w-36 shrink-0 text-muted-foreground">Giữ lại</dt>
            <dd>{{ status.keepsUsersTeamsAndAiSettings ? 'Người dùng, team và cấu hình AI' : 'Không giữ gì' }}</dd>
          </div>
          <div class="flex items-center gap-2">
            <dt class="w-36 shrink-0 text-muted-foreground">Chuỗi xác nhận</dt>
            <dd><code class="rounded bg-muted px-1.5 py-0.5 font-mono text-xs">{{ status.confirmationPhrase }}</code></dd>
          </div>
        </dl>

        <form class="grid gap-4" @submit.prevent="submitReset">
          <div class="grid gap-1.5">
            <Label for="reset-confirmation">Nhập chuỗi xác nhận để mở khóa</Label>
            <Input
              id="reset-confirmation"
              v-model="confirmationPhrase"
              type="text"
              autocomplete="off"
              :placeholder="status.confirmationPhrase"
            />
          </div>

          <label class="flex cursor-pointer items-center gap-2.5 text-sm">
            <Checkbox v-model="resetStorage" />
            Xóa file trong storage
          </label>

          <label class="flex cursor-pointer items-center gap-2.5 text-sm">
            <Checkbox v-model="resetVectorStore" />
            Reset Chroma vector collection
          </label>

          <Button variant="destructive" type="submit" class="justify-self-start" :disabled="!canSubmit">
            {{ isResetting ? 'Đang reset...' : 'Reset dữ liệu test' }}
          </Button>
        </form>
      </CardContent>
    </Card>

    <Card v-if="result" class="mt-6">
      <CardHeader>
        <CardTitle class="text-lg">Kết quả</CardTitle>
      </CardHeader>
      <CardContent>
        <dl class="grid gap-1.5 text-sm">
          <div class="flex gap-2">
            <dt class="w-56 shrink-0 text-muted-foreground">Thời gian</dt>
            <dd>{{ formatDateTime(result.completedAt) }}</dd>
          </div>
          <div class="flex gap-2">
            <dt class="w-56 shrink-0 text-muted-foreground">Dòng database đã xóa</dt>
            <dd class="tabular-nums">{{ result.databaseRowsDeleted }}</dd>
          </div>
          <div class="flex gap-2">
            <dt class="w-56 shrink-0 text-muted-foreground">File/thư mục storage đã xóa</dt>
            <dd class="tabular-nums">{{ result.storageItemsDeleted }}</dd>
          </div>
          <div class="flex gap-2">
            <dt class="w-56 shrink-0 text-muted-foreground">Vector store</dt>
            <dd>{{ result.vectorStoreReset ? 'Đã reset' : 'Không reset' }}</dd>
          </div>
          <div class="flex gap-2">
            <dt class="w-56 shrink-0 text-muted-foreground">Người dùng/team/cấu hình AI</dt>
            <dd>{{ result.usersTeamsAndAiSettingsPreserved ? 'Đã giữ lại' : 'Đã xóa' }}</dd>
          </div>
        </dl>
      </CardContent>
    </Card>
  </div>
</template>
