<script setup lang="ts">
import { onMounted, ref } from 'vue'
import type { AuditLogItem } from '../../api/audit'
import { getAuditLogs } from '../../api/audit'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const logs = ref<AuditLogItem[]>([])
const errorMessage = ref('')
const filters = ref({ action: '', entityType: '' })

async function loadLogs() {
  if (!authStore.accessToken) return
  errorMessage.value = ''

  try {
    logs.value = await getAuditLogs(authStore.accessToken, {
      action: filters.value.action || undefined,
      entityType: filters.value.entityType || undefined,
    })
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tải audit log.'
  }
}

onMounted(loadLogs)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Nhật ký audit</h2>
      <p>Theo dõi các hành động nghiệp vụ chính trong hệ thống.</p>
    </div>

    <form class="management-form" @submit.prevent="loadLogs">
      <input v-model="filters.action" type="text" placeholder="Action" />
      <input v-model="filters.entityType" type="text" placeholder="Loại entity" />
      <button type="submit">Lọc</button>
    </form>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>

    <table class="data-table">
      <thead>
        <tr>
          <th>Action</th>
          <th>Entity</th>
          <th>Người thực hiện</th>
          <th>Metadata</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="log in logs" :key="log.id">
          <td>{{ log.action }}</td>
          <td>{{ log.entityType }} / {{ log.entityId ?? '-' }}</td>
          <td>{{ log.actorDisplayName ?? '-' }}</td>
          <td>{{ log.metadataJson ?? '-' }}</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
