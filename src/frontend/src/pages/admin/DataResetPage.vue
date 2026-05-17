<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { getDataResetStatus, resetData, type DataResetResult, type DataResetStatus } from '../../api/dataReset'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const status = ref<DataResetStatus | null>(null)
const result = ref<DataResetResult | null>(null)
const confirmationPhrase = ref('')
const resetStorage = ref(true)
const resetVectorStore = ref(true)
const isLoading = ref(false)
const isResetting = ref(false)
const errorMessage = ref('')

const canSubmit = computed(() => {
  return Boolean(status.value?.enabled && confirmationPhrase.value === status.value.confirmationPhrase && !isResetting.value)
})

async function loadStatus() {
  if (!authStore.accessToken) return
  isLoading.value = true
  errorMessage.value = ''

  try {
    status.value = await getDataResetStatus(authStore.accessToken)
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Khong the tai trang thai reset.'
  } finally {
    isLoading.value = false
  }
}

async function submitReset() {
  if (!authStore.accessToken || !canSubmit.value) return
  isResetting.value = true
  errorMessage.value = ''
  result.value = null

  try {
    result.value = await resetData(authStore.accessToken, {
      confirmationPhrase: confirmationPhrase.value,
      resetStorage: resetStorage.value,
      resetVectorStore: resetVectorStore.value,
    })
    confirmationPhrase.value = ''
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Khong the reset du lieu.'
  } finally {
    isResetting.value = false
  }
}

onMounted(loadStatus)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Reset du lieu</h2>
      <p>Xoa du lieu nghiep vu, file upload va vector index sau khi test xong.</p>
    </div>

    <p v-if="isLoading">Dang tai cau hinh reset...</p>
    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>

    <div v-if="status" class="management-form">
      <p>
        <strong>Trang thai:</strong>
        {{ status.enabled ? 'Da bat' : 'Dang tat' }}
      </p>
      <p>
        <strong>Giu lai:</strong>
        {{ status.keepsUsersTeamsAndAiSettings ? 'Users, teams va cau hinh AI' : 'Khong' }}
      </p>
      <p>
        <strong>Chuoi xac nhan:</strong>
        <code>{{ status.confirmationPhrase }}</code>
      </p>
    </div>

    <form class="management-form" @submit.prevent="submitReset">
      <label>
        Nhap chuoi xac nhan
        <input v-model="confirmationPhrase" type="text" autocomplete="off" placeholder="RESET TEST DATA" />
      </label>

      <label>
        <input v-model="resetStorage" type="checkbox" />
        Xoa file trong storage
      </label>

      <label>
        <input v-model="resetVectorStore" type="checkbox" />
        Reset Chroma vector collection
      </label>

      <button type="submit" :disabled="!canSubmit">
        {{ isResetting ? 'Dang reset...' : 'Reset du lieu test' }}
      </button>
    </form>

    <div v-if="result" class="management-form">
      <h3>Ket qua</h3>
      <p><strong>Thoi gian:</strong> {{ new Date(result.completedAt).toLocaleString() }}</p>
      <p><strong>Dong database da xoa:</strong> {{ result.databaseRowsDeleted }}</p>
      <p><strong>File/folder storage da xoa:</strong> {{ result.storageItemsDeleted }}</p>
      <p><strong>Vector store:</strong> {{ result.vectorStoreReset ? 'Da reset' : 'Khong reset' }}</p>
      <p><strong>Users/teams/cau hinh AI:</strong> {{ result.usersTeamsAndAiSettingsPreserved ? 'Da giu lai' : 'Da xoa' }}</p>
    </div>
  </section>
</template>
