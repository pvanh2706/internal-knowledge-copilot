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
import FilterBar from '@/components/app/FilterBar.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import type { Application, ApplicationStatus, ApplicationType } from '@/api/applications'
import { createApplication, deleteApplication, getApplications, updateApplication } from '@/api/applications'
import { ApiError } from '@/api/http'
import type { Tenant } from '@/api/tenants'
import { getTenants } from '@/api/tenants'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useToast } from '@/composables/useToast'
import { formatDateTime } from '@/lib/format'

const authStore = useAuthStore()
const toast = useToast()
const { confirm } = useConfirm()

const tenants = ref<Tenant[]>([])
const applications = ref<Application[]>([])
const selectedApplication = ref<Application | null>(null)
// Sentinel 'all' vì SelectItem của reka-ui không nhận value rỗng
const tenantFilter = ref('all')
const isLoading = ref(false)
const isSaving = ref(false)
const loadError = ref('')
const formError = ref('')
const isDialogOpen = ref(false)

const typeLabels: Record<ApplicationType, string> = {
  Internal: 'Nội bộ',
  Crm: 'CRM',
  Sales: 'Bán hàng',
  Support: 'Hỗ trợ',
  Other: 'Khác',
}

const form = ref({
  tenantId: '',
  code: '',
  name: '',
  applicationType: 'Crm' as ApplicationType,
  baseUrl: '',
  status: 'Active' as ApplicationStatus,
})

const columns: ColumnDef<Application>[] = [
  { accessorKey: 'tenantCode', header: 'Tenant' },
  { accessorKey: 'code', header: 'Mã' },
  { accessorKey: 'name', header: 'Tên' },
  {
    accessorKey: 'applicationType',
    header: 'Loại',
    cell: ({ row }) => typeLabels[row.original.applicationType],
  },
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
            void archiveApplication(row.original)
          },
        },
        () => 'Lưu trữ',
      ),
  },
]

async function loadData() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    tenants.value = await getTenants(authStore.accessToken)
    applications.value = await getApplications(
      authStore.accessToken,
      tenantFilter.value === 'all' ? undefined : tenantFilter.value,
    )
    if (!form.value.tenantId && tenants.value.length > 0) {
      form.value.tenantId = tenants.value[0].id
    }
  } catch (error) {
    loadError.value = toMessage(error, 'Không thể tải danh sách application.')
  } finally {
    isLoading.value = false
  }
}

function openCreateDialog() {
  selectedApplication.value = null
  form.value = {
    tenantId: (tenantFilter.value !== 'all' ? tenantFilter.value : '') || tenants.value[0]?.id || '',
    code: '',
    name: '',
    applicationType: 'Crm',
    baseUrl: '',
    status: 'Active',
  }
  formError.value = ''
  isDialogOpen.value = true
}

function openEditDialog(application: Application) {
  selectedApplication.value = application
  form.value = {
    tenantId: application.tenantId,
    code: application.code,
    name: application.name,
    applicationType: application.applicationType,
    baseUrl: application.baseUrl ?? '',
    status: application.status,
  }
  formError.value = ''
  isDialogOpen.value = true
}

async function submitApplication() {
  if (!authStore.accessToken || isSaving.value) return
  formError.value = ''
  isSaving.value = true

  try {
    if (selectedApplication.value) {
      await updateApplication(
        selectedApplication.value.id,
        {
          name: form.value.name,
          applicationType: form.value.applicationType,
          baseUrl: form.value.baseUrl || null,
          status: form.value.status,
        },
        authStore.accessToken,
      )
      toast.success('Đã cập nhật application.')
    } else {
      await createApplication(
        {
          tenantId: form.value.tenantId,
          code: form.value.code,
          name: form.value.name,
          applicationType: form.value.applicationType,
          baseUrl: form.value.baseUrl || null,
          status: form.value.status,
        },
        authStore.accessToken,
      )
      toast.success('Đã tạo application.')
    }

    isDialogOpen.value = false
    await loadData()
  } catch (error) {
    formError.value = toMessage(error, 'Không thể lưu application.')
  } finally {
    isSaving.value = false
  }
}

async function archiveApplication(application: Application) {
  if (!authStore.accessToken || isSaving.value) return

  const confirmed = await confirm({
    title: `Lưu trữ application "${application.name}"?`,
    description: 'Application bị lưu trữ sẽ ngừng nhận gợi ý quy trình và hành động tự động.',
    confirmText: 'Lưu trữ',
    destructive: true,
  })
  if (!confirmed) return

  isSaving.value = true
  try {
    await deleteApplication(application.id, authStore.accessToken)
    toast.success('Đã lưu trữ application.')
    await loadData()
  } catch (error) {
    toast.error(toMessage(error, 'Không thể lưu trữ application.'))
  } finally {
    isSaving.value = false
  }
}

function toMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback
}

onMounted(loadData)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader
      title="Application"
      description="Đăng ký các ứng dụng nội bộ: CRM, bán hàng, hỗ trợ và nền tảng."
    >
      <template #actions>
        <Button @click="openCreateDialog">
          <PlusIcon class="size-4" />
          Tạo application
        </Button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadData" />

    <FilterBar>
      <div class="grid gap-1.5">
        <Label for="app-tenant-filter">Lọc theo tenant</Label>
        <Select v-model="tenantFilter" @update:model-value="loadData">
          <SelectTrigger id="app-tenant-filter" class="w-56">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Tất cả tenant</SelectItem>
            <SelectItem v-for="tenant in tenants" :key="tenant.id" :value="tenant.id">{{ tenant.code }}</SelectItem>
          </SelectContent>
        </Select>
      </div>
    </FilterBar>

    <DataTable
      :columns="columns"
      :data="applications"
      :is-loading="isLoading"
      clickable-rows
      empty-title="Chưa có application nào"
      empty-description="Bấm “Tạo application” để đăng ký ứng dụng đầu tiên."
      @row-click="openEditDialog"
    />

    <Dialog v-model:open="isDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{{ selectedApplication ? 'Sửa application' : 'Tạo application' }}</DialogTitle>
          <DialogDescription>
            {{ selectedApplication ? `Đang sửa "${selectedApplication.code}". Tenant và mã không thể thay đổi.` : 'Application đại diện một hệ thống nội bộ kết nối với Copilot.' }}
          </DialogDescription>
        </DialogHeader>
        <form id="application-form" class="grid gap-4" @submit.prevent="submitApplication">
          <div class="grid gap-1.5">
            <Label for="app-tenant">Tenant</Label>
            <Select v-model="form.tenantId" :disabled="Boolean(selectedApplication)" required>
              <SelectTrigger id="app-tenant" class="w-full">
                <SelectValue placeholder="Chọn tenant" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem v-for="tenant in tenants" :key="tenant.id" :value="tenant.id">{{ tenant.code }}</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="grid gap-1.5">
            <Label for="app-code">Mã application</Label>
            <Input id="app-code" v-model="form.code" type="text" :disabled="Boolean(selectedApplication)" required />
          </div>
          <div class="grid gap-1.5">
            <Label for="app-name">Tên application</Label>
            <Input id="app-name" v-model="form.name" type="text" required />
          </div>
          <div class="grid gap-1.5">
            <Label for="app-type">Loại</Label>
            <Select v-model="form.applicationType">
              <SelectTrigger id="app-type" class="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Internal">Nội bộ</SelectItem>
                <SelectItem value="Crm">CRM</SelectItem>
                <SelectItem value="Sales">Bán hàng</SelectItem>
                <SelectItem value="Support">Hỗ trợ</SelectItem>
                <SelectItem value="Other">Khác</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="grid gap-1.5">
            <Label for="app-base-url">Base URL</Label>
            <Input id="app-base-url" v-model="form.baseUrl" type="url" placeholder="https://..." />
          </div>
          <div class="grid gap-1.5">
            <Label for="app-status">Trạng thái</Label>
            <Select v-model="form.status">
              <SelectTrigger id="app-status" class="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Active">Hoạt động</SelectItem>
                <SelectItem value="Disabled">Vô hiệu</SelectItem>
                <SelectItem value="Archived">Đã lưu trữ</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <p v-if="formError" class="text-sm text-destructive" role="alert">{{ formError }}</p>
        </form>
        <DialogFooter>
          <Button variant="outline" type="button" :disabled="isSaving" @click="isDialogOpen = false">Hủy</Button>
          <Button type="submit" form="application-form" :disabled="isSaving">
            {{ isSaving ? 'Đang lưu...' : selectedApplication ? 'Cập nhật' : 'Tạo application' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
