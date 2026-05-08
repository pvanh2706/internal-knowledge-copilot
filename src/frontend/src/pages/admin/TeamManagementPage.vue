<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ApiError } from '../../api/http'
import type { Team } from '../../api/teams'
import { createTeam, getTeams } from '../../api/teams'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const teams = ref<Team[]>([])
const name = ref('')
const description = ref('')
const errorMessage = ref('')

async function loadTeams() {
  if (!authStore.accessToken) {
    return
  }

  teams.value = await getTeams(authStore.accessToken)
}

async function submitCreateTeam() {
  if (!authStore.accessToken) {
    return
  }

  errorMessage.value = ''
  try {
    await createTeam({ name: name.value, description: description.value }, authStore.accessToken)
    name.value = ''
    description.value = ''
    await loadTeams()
  } catch (error) {
    errorMessage.value = error instanceof ApiError ? error.message : 'Không thể tạo team.'
  }
}

onMounted(loadTeams)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Quản trị team</h2>
      <p>Tạo team/phòng ban dùng cho phân quyền tài liệu.</p>
    </div>

    <form class="management-form" @submit.prevent="submitCreateTeam">
      <input v-model="name" type="text" placeholder="Tên team" required />
      <input v-model="description" type="text" placeholder="Mô tả" />
      <button type="submit">Tạo team</button>
    </form>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>

    <table class="data-table">
      <thead>
        <tr>
          <th>Tên team</th>
          <th>Mô tả</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="team in teams" :key="team.id">
          <td>{{ team.name }}</td>
          <td>{{ team.description ?? '' }}</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
