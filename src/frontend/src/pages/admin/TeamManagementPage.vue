<script setup lang="ts">
import { onMounted, ref } from 'vue'
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
import DataTable from '@/components/app/DataTable.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import { ApiError } from '@/api/http'
import type { Team } from '@/api/teams'
import { createTeam, getTeams } from '@/api/teams'
import { useAuthStore } from '@/stores/auth'
import { useToast } from '@/composables/useToast'

const authStore = useAuthStore()
const toast = useToast()

const teams = ref<Team[]>([])
const isLoading = ref(false)
const isSaving = ref(false)
const loadError = ref('')
const formError = ref('')
const isDialogOpen = ref(false)

const form = ref({ name: '', description: '' })

const columns: ColumnDef<Team>[] = [
  { accessorKey: 'name', header: 'Tên team' },
  {
    accessorKey: 'description',
    header: 'Mô tả',
    enableSorting: false,
    cell: ({ row }) => row.original.description ?? '—',
  },
]

async function loadTeams() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    teams.value = await getTeams(authStore.accessToken)
  } catch (error) {
    loadError.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tải danh sách team.'
  } finally {
    isLoading.value = false
  }
}

function openCreateDialog() {
  form.value = { name: '', description: '' }
  formError.value = ''
  isDialogOpen.value = true
}

async function submitCreateTeam() {
  if (!authStore.accessToken || isSaving.value) return
  formError.value = ''
  isSaving.value = true

  try {
    await createTeam({ name: form.value.name, description: form.value.description }, authStore.accessToken)
    isDialogOpen.value = false
    toast.success('Đã tạo team.')
    await loadTeams()
  } catch (error) {
    formError.value = error instanceof ApiError ? error.message : 'Không thể tạo team.'
  } finally {
    isSaving.value = false
  }
}

onMounted(loadTeams)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader title="Team" description="Tạo team/phòng ban dùng cho phân quyền tài liệu.">
      <template #actions>
        <Button @click="openCreateDialog">
          <PlusIcon class="size-4" />
          Tạo team
        </Button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadTeams" />

    <DataTable
      :columns="columns"
      :data="teams"
      :is-loading="isLoading"
      empty-title="Chưa có team nào"
      empty-description="Bấm “Tạo team” để thêm phòng ban đầu tiên."
    />

    <Dialog v-model:open="isDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Tạo team</DialogTitle>
          <DialogDescription>Team dùng để gán quyền truy cập thư mục tài liệu.</DialogDescription>
        </DialogHeader>
        <form id="create-team-form" class="grid gap-4" @submit.prevent="submitCreateTeam">
          <div class="grid gap-1.5">
            <Label for="team-name">Tên team</Label>
            <Input id="team-name" v-model="form.name" type="text" required />
          </div>
          <div class="grid gap-1.5">
            <Label for="team-description">Mô tả</Label>
            <Input id="team-description" v-model="form.description" type="text" />
          </div>
          <p v-if="formError" class="text-sm text-destructive" role="alert">{{ formError }}</p>
        </form>
        <DialogFooter>
          <Button variant="outline" type="button" :disabled="isSaving" @click="isDialogOpen = false">Hủy</Button>
          <Button type="submit" form="create-team-form" :disabled="isSaving">
            {{ isSaving ? 'Đang tạo...' : 'Tạo team' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
