<script setup lang="ts">
import { h, onMounted, ref } from 'vue'
import type { ColumnDef } from '@tanstack/vue-table'
import { RefreshCwIcon, XIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
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
import JsonViewer from '@/components/app/JsonViewer.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import type { Application } from '@/api/applications'
import { getApplications } from '@/api/applications'
import { ApiError } from '@/api/http'
import type { ExternalObject, KnowledgeSource } from '@/api/knowledgeSources'
import { getExternalObjects, getKnowledgeSources } from '@/api/knowledgeSources'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/lib/format'

const authStore = useAuthStore()

const applications = ref<Application[]>([])
const sources = ref<KnowledgeSource[]>([])
const externalObjects = ref<ExternalObject[]>([])
// Sentinel 'all' vì SelectItem của reka-ui không nhận value rỗng
const selectedApplicationId = ref('all')
const selectedSourceId = ref('')
const selectedSource = ref<KnowledgeSource | null>(null)
const selectedObject = ref<ExternalObject | null>(null)
const isLoading = ref(false)
const errorMessage = ref('')

const sourceColumns: ColumnDef<KnowledgeSource>[] = [
  { accessorKey: 'applicationCode', header: 'Application' },
  { accessorKey: 'name', header: 'Tên nguồn' },
  {
    accessorKey: 'sourceType',
    header: 'Loại / Đồng bộ',
    cell: ({ row }) => `${row.original.sourceType} / ${row.original.syncMode}`,
  },
  {
    accessorKey: 'status',
    header: 'Trạng thái',
    cell: ({ row }) => h(StatusBadge, { status: row.original.status }),
  },
  {
    accessorKey: 'lastSyncCompletedAt',
    header: 'Đồng bộ gần nhất',
    cell: ({ row }) => formatDateTime(row.original.lastSyncCompletedAt),
  },
]

const objectColumns: ColumnDef<ExternalObject>[] = [
  {
    accessorKey: 'objectType',
    header: 'Đối tượng',
    cell: ({ row }) =>
      h('span', { class: 'font-mono text-xs' }, `${row.original.objectType} / ${row.original.externalObjectId}`),
  },
  { accessorKey: 'title', header: 'Tiêu đề' },
  {
    accessorKey: 'status',
    header: 'Trạng thái',
    cell: ({ row }) => h(StatusBadge, { status: row.original.status }),
  },
  {
    accessorKey: 'aclSyncedAt',
    header: 'Đồng bộ ACL',
    cell: ({ row }) => formatDateTime(row.original.aclSyncedAt),
  },
]

async function loadData() {
  if (!authStore.accessToken) return
  errorMessage.value = ''
  isLoading.value = true

  const applicationId = selectedApplicationId.value === 'all' ? undefined : selectedApplicationId.value
  try {
    applications.value = await getApplications(authStore.accessToken)
    sources.value = await getKnowledgeSources(authStore.accessToken, applicationId)
    externalObjects.value = await getExternalObjects(
      authStore.accessToken,
      applicationId,
      selectedSourceId.value || undefined,
    )
    if (selectedSource.value) {
      selectedSource.value = sources.value.find((source) => source.id === selectedSource.value?.id) ?? null
    }
    if (selectedObject.value) {
      selectedObject.value = externalObjects.value.find((item) => item.id === selectedObject.value?.id) ?? null
    }
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tải nguồn tri thức.'
  } finally {
    isLoading.value = false
  }
}

function selectSource(source: KnowledgeSource) {
  selectedSource.value = source
  selectedSourceId.value = source.id
  selectedObject.value = null
  void loadData()
}

function clearSourceFilter() {
  selectedSource.value = null
  selectedSourceId.value = ''
  void loadData()
}

onMounted(loadData)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader
      title="Nguồn tri thức"
      description="Theo dõi trạng thái đồng bộ nguồn và các đối tượng nghiệp vụ ngoài khả dụng cho truy xuất."
    >
      <template #actions>
        <Button variant="outline" :disabled="isLoading" @click="loadData">
          <RefreshCwIcon class="size-4" />
          Làm mới
        </Button>
      </template>
    </PageHeader>

    <ErrorState v-if="errorMessage" :message="errorMessage" class="mb-4" @retry="loadData" />

    <FilterBar>
      <div class="grid gap-1.5">
        <Label for="ks-application">Application</Label>
        <Select v-model="selectedApplicationId" @update:model-value="loadData">
          <SelectTrigger id="ks-application" class="w-64">
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
      <Button v-if="selectedSourceId" variant="outline" type="button" @click="clearSourceFilter">
        <XIcon class="size-4" />
        Bỏ lọc theo nguồn
      </Button>
    </FilterBar>

    <div class="grid items-start gap-6 xl:grid-cols-2">
      <div>
        <p class="mb-2 text-sm font-medium">Nguồn</p>
        <DataTable
          :columns="sourceColumns"
          :data="sources"
          :is-loading="isLoading"
          clickable-rows
          empty-title="Chưa có nguồn tri thức nào"
          @row-click="selectSource"
        />
      </div>
      <div>
        <p class="mb-2 text-sm font-medium">Đối tượng ngoài</p>
        <DataTable
          :columns="objectColumns"
          :data="externalObjects"
          :is-loading="isLoading"
          clickable-rows
          empty-title="Chưa có đối tượng ngoài nào"
          @row-click="(item) => (selectedObject = item)"
        />
      </div>
    </div>

    <Card v-if="selectedSource || selectedObject" class="mt-6">
      <CardHeader>
        <CardTitle class="text-lg">Chi tiết</CardTitle>
      </CardHeader>
      <CardContent class="grid gap-5">
        <div v-if="selectedSource" class="grid gap-3">
          <dl class="grid gap-1.5 text-sm">
            <div class="flex gap-2">
              <dt class="w-36 shrink-0 text-muted-foreground">Nguồn</dt>
              <dd>{{ selectedSource.name }}</dd>
            </div>
            <div class="flex gap-2">
              <dt class="w-36 shrink-0 text-muted-foreground">Mã nguồn ngoài</dt>
              <dd class="font-mono text-xs">{{ selectedSource.externalSourceId }}</dd>
            </div>
            <div class="flex gap-2">
              <dt class="w-36 shrink-0 text-muted-foreground">Trạng thái đồng bộ</dt>
              <dd>{{ selectedSource.lastSyncStatus ?? '—' }}</dd>
            </div>
            <div class="flex gap-2">
              <dt class="w-36 shrink-0 text-muted-foreground">Lỗi đồng bộ</dt>
              <dd :class="selectedSource.lastSyncError ? 'text-destructive' : ''">
                {{ selectedSource.lastSyncError ?? '—' }}
              </dd>
            </div>
          </dl>
          <JsonViewer v-if="selectedSource.metadataJson" title="Metadata nguồn" :value="selectedSource.metadataJson" />
        </div>

        <div v-if="selectedObject" class="grid gap-3">
          <dl class="grid gap-1.5 text-sm">
            <div class="flex gap-2">
              <dt class="w-36 shrink-0 text-muted-foreground">Đối tượng</dt>
              <dd class="font-mono text-xs">{{ selectedObject.objectType }} / {{ selectedObject.externalObjectId }}</dd>
            </div>
            <div class="flex gap-2">
              <dt class="w-36 shrink-0 text-muted-foreground">URL</dt>
              <dd class="truncate font-mono text-xs">{{ selectedObject.url ?? '—' }}</dd>
            </div>
            <div class="flex gap-2">
              <dt class="w-36 shrink-0 text-muted-foreground">Hash nội dung</dt>
              <dd class="font-mono text-xs">{{ selectedObject.contentHash ?? '—' }}</dd>
            </div>
            <div class="flex gap-2">
              <dt class="w-36 shrink-0 text-muted-foreground">Hash ACL</dt>
              <dd class="font-mono text-xs">{{ selectedObject.aclHash ?? '—' }}</dd>
            </div>
          </dl>
          <JsonViewer v-if="selectedObject.metadataJson" title="Metadata đối tượng" :value="selectedObject.metadataJson" />
        </div>
      </CardContent>
    </Card>
  </div>
</template>
