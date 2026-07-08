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
import type { Application } from '@/api/applications'
import { getApplications } from '@/api/applications'
import { ApiError } from '@/api/http'
import type { IntegrationAuthMode, IntegrationConnection, IntegrationConnectionStatus } from '@/api/integrations'
import { createIntegrationConnection, getIntegrationConnections } from '@/api/integrations'
import { useAuthStore } from '@/stores/auth'
import { useToast } from '@/composables/useToast'
import { formatDateTime } from '@/lib/format'

const authStore = useAuthStore()
const toast = useToast()

const applications = ref<Application[]>([])
const connections = ref<IntegrationConnection[]>([])
// Sentinel 'all' vì SelectItem của reka-ui không nhận value rỗng
const applicationFilter = ref('all')
const isLoading = ref(false)
const isSaving = ref(false)
const loadError = ref('')
const formError = ref('')
const isDialogOpen = ref(false)

const form = ref({
  applicationId: '',
  name: '',
  baseUrl: '',
  authMode: 'InternalApiKey' as IntegrationAuthMode,
  secretReference: '',
  secretValue: '',
  status: 'Active' as IntegrationConnectionStatus,
  metadataJson: '',
})

const columns: ColumnDef<IntegrationConnection>[] = [
  { accessorKey: 'applicationCode', header: 'Application' },
  { accessorKey: 'name', header: 'Tên kết nối' },
  { accessorKey: 'baseUrl', header: 'Base URL' },
  {
    accessorKey: 'authMode',
    header: 'Xác thực',
    cell: ({ row }) => (row.original.authMode === 'InternalApiKey' ? 'API key nội bộ' : 'Không'),
  },
  {
    accessorKey: 'status',
    header: 'Trạng thái',
    cell: ({ row }) => h(StatusBadge, { status: row.original.status }),
  },
  {
    accessorKey: 'secretReference',
    header: 'Secret',
    cell: ({ row }) =>
      h('span', { class: 'inline-flex items-center gap-1.5' }, [
        h('span', { class: 'font-mono text-xs' }, row.original.secretReference),
        h(StatusBadge, {
          status: row.original.secretConfigured ? 'Active' : 'Failed',
          label: row.original.secretConfigured ? 'Đã cấu hình' : 'Thiếu',
        }),
      ]),
  },
  {
    accessorKey: 'secretRotatedAt',
    header: 'Xoay secret',
    cell: ({ row }) => formatDateTime(row.original.secretRotatedAt),
  },
]

async function loadData() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    applications.value = await getApplications(authStore.accessToken)
    if (!form.value.applicationId && applications.value.length > 0) {
      form.value.applicationId = applications.value[0].id
    }
    connections.value = await getIntegrationConnections(
      authStore.accessToken,
      applicationFilter.value === 'all' ? undefined : applicationFilter.value,
    )
  } catch (error) {
    loadError.value = toMessage(error, 'Không thể tải cấu hình tích hợp.')
  } finally {
    isLoading.value = false
  }
}

function openCreateDialog() {
  form.value = {
    applicationId: applications.value[0]?.id ?? '',
    name: '',
    baseUrl: '',
    authMode: 'InternalApiKey',
    secretReference: '',
    secretValue: '',
    status: 'Active',
    metadataJson: '',
  }
  formError.value = ''
  isDialogOpen.value = true
}

async function submitConnection() {
  if (!authStore.accessToken || isSaving.value) return
  formError.value = ''
  isSaving.value = true

  try {
    await createIntegrationConnection(
      {
        applicationId: form.value.applicationId,
        name: form.value.name,
        baseUrl: form.value.baseUrl,
        authMode: form.value.authMode,
        secretReference: form.value.secretReference,
        secretValue: form.value.secretValue || null,
        status: form.value.status,
        metadataJson: form.value.metadataJson || null,
      },
      authStore.accessToken,
    )
    isDialogOpen.value = false
    toast.success('Đã tạo kết nối tích hợp.')
    await loadData()
  } catch (error) {
    formError.value = toMessage(error, 'Không thể tạo kết nối tích hợp.')
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
      title="Tích hợp"
      description="Cấu hình endpoint connector dùng cho ngữ cảnh, kiểm tra quyền, xác thực và thực thi hành động."
    >
      <template #actions>
        <Button @click="openCreateDialog">
          <PlusIcon class="size-4" />
          Tạo kết nối
        </Button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadData" />

    <FilterBar>
      <div class="grid gap-1.5">
        <Label for="integration-app-filter">Lọc theo application</Label>
        <Select v-model="applicationFilter" @update:model-value="loadData">
          <SelectTrigger id="integration-app-filter" class="w-64">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Tất cả application</SelectItem>
            <SelectItem v-for="application in applications" :key="application.id" :value="application.id">
              {{ application.tenantCode }} / {{ application.code }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>
    </FilterBar>

    <DataTable
      :columns="columns"
      :data="connections"
      :is-loading="isLoading"
      empty-title="Chưa có kết nối tích hợp nào"
      empty-description="Bấm “Tạo kết nối” để cấu hình connector đầu tiên."
    />

    <Dialog v-model:open="isDialogOpen">
      <DialogContent class="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Tạo kết nối tích hợp</DialogTitle>
          <DialogDescription>Secret được mã hóa phía server; giá trị chỉ nhập khi tạo hoặc xoay khóa.</DialogDescription>
        </DialogHeader>
        <form id="integration-form" class="grid gap-4" @submit.prevent="submitConnection">
          <div class="grid gap-1.5">
            <Label for="integration-app">Application</Label>
            <Select v-model="form.applicationId" required>
              <SelectTrigger id="integration-app" class="w-full">
                <SelectValue placeholder="Chọn application" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem v-for="application in applications" :key="application.id" :value="application.id">
                  {{ application.tenantCode }} / {{ application.code }}
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="grid gap-1.5">
            <Label for="integration-name">Tên kết nối</Label>
            <Input id="integration-name" v-model="form.name" type="text" required />
          </div>
          <div class="grid gap-1.5">
            <Label for="integration-base-url">Base URL</Label>
            <Input id="integration-base-url" v-model="form.baseUrl" type="url" placeholder="https://..." required />
          </div>
          <div class="grid gap-1.5">
            <Label for="integration-auth-mode">Chế độ xác thực</Label>
            <Select v-model="form.authMode">
              <SelectTrigger id="integration-auth-mode" class="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="InternalApiKey">API key nội bộ</SelectItem>
                <SelectItem value="None">Không</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="grid gap-1.5">
            <Label for="integration-secret-ref">Mã định danh secret</Label>
            <Input id="integration-secret-ref" v-model="form.secretReference" type="text" required />
          </div>
          <div class="grid gap-1.5">
            <Label for="integration-secret-value">Giá trị secret</Label>
            <Input id="integration-secret-value" v-model="form.secretValue" type="password" autocomplete="new-password" />
          </div>
          <div class="grid gap-1.5">
            <Label for="integration-status">Trạng thái</Label>
            <Select v-model="form.status">
              <SelectTrigger id="integration-status" class="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Active">Hoạt động</SelectItem>
                <SelectItem value="Disabled">Vô hiệu</SelectItem>
                <SelectItem value="Archived">Đã lưu trữ</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="grid gap-1.5">
            <Label for="integration-metadata">Metadata JSON</Label>
            <Input id="integration-metadata" v-model="form.metadataJson" type="text" placeholder="{&quot;owner&quot;:&quot;sales&quot;}" />
          </div>
          <p v-if="formError" class="text-sm text-destructive" role="alert">{{ formError }}</p>
        </form>
        <DialogFooter>
          <Button variant="outline" type="button" :disabled="isSaving" @click="isDialogOpen = false">Hủy</Button>
          <Button type="submit" form="integration-form" :disabled="isSaving">
            {{ isSaving ? 'Đang tạo...' : 'Tạo kết nối' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
