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
import { ApiError } from '@/api/http'
import type { Tenant, TenantStatus } from '@/api/tenants'
import { createTenant, deleteTenant, getTenants, updateTenant } from '@/api/tenants'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useToast } from '@/composables/useToast'
import { formatDateTime } from '@/lib/format'

const authStore = useAuthStore()
const toast = useToast()
const { confirm } = useConfirm()

const tenants = ref<Tenant[]>([])
const selectedTenant = ref<Tenant | null>(null)
const isLoading = ref(false)
const isSaving = ref(false)
const loadError = ref('')
const formError = ref('')
const isDialogOpen = ref(false)

const form = ref({
  code: '',
  name: '',
  status: 'Active' as TenantStatus,
})

const columns: ColumnDef<Tenant>[] = [
  { accessorKey: 'code', header: 'Mã tenant' },
  { accessorKey: 'name', header: 'Tên' },
  {
    accessorKey: 'status',
    header: 'Trạng thái',
    cell: ({ row }) => h(StatusBadge, { status: row.original.status }),
  },
  {
    accessorKey: 'updatedAt',
    header: 'Cập nhật',
    cell: ({ row }) => formatDateTime(row.original.updatedAt),
  },
  {
    id: 'actions',
    header: '',
    enableSorting: false,
    cell: ({ row }) =>
      h(
        Button,
        {
          variant: 'ghost',
          size: 'sm',
          class: 'text-destructive hover:text-destructive',
          disabled: isSaving.value,
          onClick: (event: MouseEvent) => {
            event.stopPropagation()
            void archiveTenant(row.original)
          },
        },
        () => 'Lưu trữ',
      ),
  },
]

async function loadTenants() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    tenants.value = await getTenants(authStore.accessToken)
  } catch (error) {
    loadError.value = toMessage(error, 'Không thể tải danh sách tenant.')
  } finally {
    isLoading.value = false
  }
}

function openCreateDialog() {
  selectedTenant.value = null
  form.value = { code: '', name: '', status: 'Active' }
  formError.value = ''
  isDialogOpen.value = true
}

function openEditDialog(tenant: Tenant) {
  selectedTenant.value = tenant
  form.value = { code: tenant.code, name: tenant.name, status: tenant.status }
  formError.value = ''
  isDialogOpen.value = true
}

async function submitTenant() {
  if (!authStore.accessToken || isSaving.value) return
  formError.value = ''
  isSaving.value = true

  try {
    if (selectedTenant.value) {
      await updateTenant(
        selectedTenant.value.id,
        { name: form.value.name, status: form.value.status },
        authStore.accessToken,
      )
      toast.success('Đã cập nhật tenant.')
    } else {
      await createTenant(
        { code: form.value.code, name: form.value.name, status: form.value.status },
        authStore.accessToken,
      )
      toast.success('Đã tạo tenant.')
    }

    isDialogOpen.value = false
    await loadTenants()
  } catch (error) {
    formError.value = toMessage(error, 'Không thể lưu tenant.')
  } finally {
    isSaving.value = false
  }
}

async function archiveTenant(tenant: Tenant) {
  if (!authStore.accessToken || isSaving.value) return

  const confirmed = await confirm({
    title: `Lưu trữ tenant "${tenant.name}"?`,
    description: 'Tenant bị lưu trữ sẽ ngừng phục vụ mọi ứng dụng và người dùng thuộc tenant đó.',
    confirmText: 'Lưu trữ',
    destructive: true,
  })
  if (!confirmed) return

  isSaving.value = true
  try {
    await deleteTenant(tenant.id, authStore.accessToken)
    toast.success('Đã lưu trữ tenant.')
    await loadTenants()
  } catch (error) {
    toast.error(toMessage(error, 'Không thể lưu trữ tenant.'))
  } finally {
    isSaving.value = false
  }
}

function toMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback
}

onMounted(loadTenants)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader title="Quản trị tenant" description="Quản lý các tenant dùng chung nền tảng Copilot.">
      <template #actions>
        <Button @click="openCreateDialog">
          <PlusIcon class="size-4" />
          Tạo tenant
        </Button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadTenants" />

    <DataTable
      :columns="columns"
      :data="tenants"
      :is-loading="isLoading"
      clickable-rows
      empty-title="Chưa có tenant nào"
      empty-description="Bấm “Tạo tenant” để thêm tenant đầu tiên."
      @row-click="openEditDialog"
    />

    <Dialog v-model:open="isDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{{ selectedTenant ? 'Sửa tenant' : 'Tạo tenant' }}</DialogTitle>
          <DialogDescription>
            {{ selectedTenant ? `Đang sửa tenant "${selectedTenant.code}". Mã tenant không thể thay đổi.` : 'Mã tenant dùng để định danh khi đăng nhập và gọi API.' }}
          </DialogDescription>
        </DialogHeader>
        <form id="tenant-form" class="grid gap-4" @submit.prevent="submitTenant">
          <div class="grid gap-1.5">
            <Label for="tenant-code">Mã tenant</Label>
            <Input id="tenant-code" v-model="form.code" type="text" :disabled="Boolean(selectedTenant)" required />
          </div>
          <div class="grid gap-1.5">
            <Label for="tenant-name">Tên tenant</Label>
            <Input id="tenant-name" v-model="form.name" type="text" required />
          </div>
          <div class="grid gap-1.5">
            <Label for="tenant-status">Trạng thái</Label>
            <Select v-model="form.status">
              <SelectTrigger id="tenant-status" class="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Active">Hoạt động</SelectItem>
                <SelectItem value="Suspended">Tạm ngưng</SelectItem>
                <SelectItem value="Archived">Đã lưu trữ</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <p v-if="formError" class="text-sm text-destructive" role="alert">{{ formError }}</p>
        </form>
        <DialogFooter>
          <Button variant="outline" type="button" :disabled="isSaving" @click="isDialogOpen = false">Hủy</Button>
          <Button type="submit" form="tenant-form" :disabled="isSaving">
            {{ isSaving ? 'Đang lưu...' : selectedTenant ? 'Cập nhật' : 'Tạo tenant' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
