<script setup lang="ts">
import { onMounted, ref } from 'vue'
import type { UserRole } from '../../api/auth'
import { ApiError } from '../../api/http'
import type { Team } from '../../api/teams'
import { getTeams } from '../../api/teams'
import type { UserListItem } from '../../api/users'
import { createUser, getUsers } from '../../api/users'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const users = ref<UserListItem[]>([])
const teams = ref<Team[]>([])
const errorMessage = ref('')

const form = ref({
  email: '',
  displayName: '',
  role: 'User' as UserRole,
  primaryTeamId: '',
  initialPassword: 'ChangeMe123!',
})

async function loadData() {
  if (!authStore.accessToken) {
    return
  }

  users.value = await getUsers(authStore.accessToken)
  teams.value = await getTeams(authStore.accessToken)
}

async function submitCreateUser() {
  if (!authStore.accessToken) {
    return
  }

  errorMessage.value = ''
  try {
    await createUser(
      {
        ...form.value,
        primaryTeamId: form.value.primaryTeamId || undefined,
      },
      authStore.accessToken,
    )
    form.value.email = ''
    form.value.displayName = ''
    await loadData()
  } catch (error) {
    errorMessage.value = error instanceof ApiError ? error.message : 'Không thể tạo user.'
  }
}

onMounted(loadData)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Quản trị người dùng</h2>
      <p>Tạo user, gán role và team chính cho tài khoản nội bộ.</p>
    </div>

    <form class="management-form" @submit.prevent="submitCreateUser">
      <input v-model="form.email" type="email" placeholder="Email" required />
      <input v-model="form.displayName" type="text" placeholder="Tên hiển thị" required />
      <select v-model="form.role">
        <option value="User">User</option>
        <option value="Reviewer">Reviewer</option>
        <option value="Admin">Admin</option>
      </select>
      <select v-model="form.primaryTeamId">
        <option value="">Chưa chọn team</option>
        <option v-for="team in teams" :key="team.id" :value="team.id">{{ team.name }}</option>
      </select>
      <input v-model="form.initialPassword" type="text" placeholder="Mật khẩu ban đầu" required />
      <button type="submit">Tạo user</button>
    </form>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>

    <table class="data-table">
      <thead>
        <tr>
          <th>Email</th>
          <th>Tên</th>
          <th>Role</th>
          <th>Team</th>
          <th>Trạng thái</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="user in users" :key="user.id">
          <td>{{ user.email }}</td>
          <td>{{ user.displayName }}</td>
          <td>{{ user.role }}</td>
          <td>{{ user.primaryTeamName ?? 'Chưa gán' }}</td>
          <td>{{ user.isActive ? 'Hoạt động' : 'Tạm khóa' }}</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
