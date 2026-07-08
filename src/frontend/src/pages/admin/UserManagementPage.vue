<script setup lang="ts">
import { h, onMounted, ref } from 'vue'
import type { ColumnDef } from '@tanstack/vue-table'
import { PlusIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import DataTable from '@/components/app/DataTable.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import type { UserRole } from '@/api/auth'
import { ApiError } from '@/api/http'
import type { Team } from '@/api/teams'
import { getTeams } from '@/api/teams'
import type { UserListItem } from '@/api/users'
import { createUser, getUsers } from '@/api/users'
import { useAuthStore } from '@/stores/auth'
import { useToast } from '@/composables/useToast'

const authStore = useAuthStore()
const toast = useToast()

const users = ref<UserListItem[]>([])
const teams = ref<Team[]>([])
const isLoading = ref(false)
const isSaving = ref(false)
const loadError = ref('')
const formError = ref('')
const isDialogOpen = ref(false)

const roleLabels: Record<UserRole, string> = {
  Admin: 'Quản trị viên',
  Reviewer: 'Người duyệt',
  User: 'Nhân viên',
}

const emptyForm = {
  email: '',
  displayName: '',
  role: 'User' as UserRole,
  primaryTeamId: '',
  // Không hardcode mật khẩu mặc định — admin phải tự nhập mật khẩu ban đầu
  initialPassword: '',
}
const form = ref({ ...emptyForm })

const columns: ColumnDef<UserListItem>[] = [
  { accessorKey: 'email', header: 'Email' },
  { accessorKey: 'displayName', header: 'Tên hiển thị' },
  { accessorKey: 'role', header: 'Vai trò', cell: ({ row }) => roleLabels[row.original.role] },
  {
    accessorKey: 'primaryTeamName',
    header: 'Team',
    cell: ({ row }) => row.original.primaryTeamName ?? 'Chưa gán',
  },
  {
    accessorKey: 'isActive',
    header: 'Trạng thái',
    cell: ({ row }) =>
      h(StatusBadge, {
        status: row.original.isActive ? 'Active' : 'Disabled',
        label: row.original.isActive ? 'Hoạt động' : 'Tạm khóa',
      }),
  },
]

async function loadData() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    users.value = await getUsers(authStore.accessToken)
    teams.value = await getTeams(authStore.accessToken)
  } catch (error) {
    loadError.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tải danh sách người dùng.'
  } finally {
    isLoading.value = false
  }
}

function openCreateDialog() {
  form.value = { ...emptyForm }
  formError.value = ''
  isDialogOpen.value = true
}

async function submitCreateUser() {
  if (!authStore.accessToken || isSaving.value) return
  formError.value = ''
  isSaving.value = true

  try {
    await createUser(
      {
        ...form.value,
        primaryTeamId: form.value.primaryTeamId || undefined,
      },
      authStore.accessToken,
    )
    isDialogOpen.value = false
    toast.success('Đã tạo người dùng.')
    await loadData()
  } catch (error) {
    formError.value = error instanceof ApiError ? error.message : 'Không thể tạo người dùng.'
  } finally {
    isSaving.value = false
  }
}

onMounted(loadData)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader title="Người dùng" description="Tạo tài khoản, gán vai trò và team chính cho nhân sự nội bộ.">
      <template #actions>
        <Button @click="openCreateDialog">
          <PlusIcon class="size-4" />
          Tạo người dùng
        </Button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadData" />

    <DataTable
      :columns="columns"
      :data="users"
      :is-loading="isLoading"
      empty-title="Chưa có người dùng nào"
      empty-description="Bấm “Tạo người dùng” để thêm tài khoản đầu tiên."
    />

    <Dialog v-model:open="isDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Tạo người dùng</DialogTitle>
          <DialogDescription>Tài khoản mới sẽ phải đổi mật khẩu ở lần đăng nhập đầu tiên.</DialogDescription>
        </DialogHeader>
        <form id="create-user-form" class="grid gap-4" @submit.prevent="submitCreateUser">
          <div class="grid gap-1.5">
            <Label for="user-email">Email</Label>
            <Input id="user-email" v-model="form.email" type="email" autocomplete="off" required />
          </div>
          <div class="grid gap-1.5">
            <Label for="user-display-name">Tên hiển thị</Label>
            <Input id="user-display-name" v-model="form.displayName" type="text" required />
          </div>
          <div class="grid gap-1.5">
            <Label for="user-role">Vai trò</Label>
            <Select v-model="form.role">
              <SelectTrigger id="user-role" class="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="User">Nhân viên</SelectItem>
                <SelectItem value="Reviewer">Người duyệt</SelectItem>
                <SelectItem value="Admin">Quản trị viên</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="grid gap-1.5">
            <Label for="user-team">Team chính</Label>
            <Select v-model="form.primaryTeamId">
              <SelectTrigger id="user-team" class="w-full">
                <SelectValue placeholder="Chưa chọn team" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem v-for="team in teams" :key="team.id" :value="team.id">{{ team.name }}</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="grid gap-1.5">
            <Label for="user-password">Mật khẩu ban đầu</Label>
            <Input id="user-password" v-model="form.initialPassword" type="text" autocomplete="off" required />
          </div>
          <p v-if="formError" class="text-sm text-destructive" role="alert">{{ formError }}</p>
        </form>
        <DialogFooter>
          <Button variant="outline" type="button" :disabled="isSaving" @click="isDialogOpen = false">Hủy</Button>
          <Button type="submit" form="create-user-form" :disabled="isSaving">
            {{ isSaving ? 'Đang tạo...' : 'Tạo người dùng' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
