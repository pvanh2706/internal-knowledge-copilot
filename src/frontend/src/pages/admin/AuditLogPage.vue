<script setup lang="ts">
import { h, onMounted, ref } from 'vue'
import type { ColumnDef } from '@tanstack/vue-table'
import { SearchIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import DataTable from '@/components/app/DataTable.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import FilterBar from '@/components/app/FilterBar.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import type { AuditLogItem } from '@/api/audit'
import { getAuditLogs } from '@/api/audit'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/lib/format'

const authStore = useAuthStore()
const logs = ref<AuditLogItem[]>([])
const isLoading = ref(false)
const loadError = ref('')
const filters = ref({ action: '', entityType: '' })

const columns: ColumnDef<AuditLogItem>[] = [
  { accessorKey: 'action', header: 'Hành động' },
  {
    accessorKey: 'entityType',
    header: 'Đối tượng',
    cell: ({ row }) =>
      h('span', [
        row.original.entityType,
        row.original.entityId
          ? h('span', { class: 'ml-1 font-mono text-xs text-muted-foreground' }, row.original.entityId)
          : null,
      ]),
  },
  {
    accessorKey: 'actorDisplayName',
    header: 'Người thực hiện',
    cell: ({ row }) => row.original.actorDisplayName ?? '—',
  },
  {
    accessorKey: 'createdAt',
    header: 'Thời điểm',
    cell: ({ row }) => formatDateTime(row.original.createdAt),
  },
  {
    accessorKey: 'metadataJson',
    header: 'Metadata',
    enableSorting: false,
    cell: ({ row }) =>
      row.original.metadataJson
        ? h(
            'code',
            {
              class: 'block max-w-[320px] truncate font-mono text-xs text-muted-foreground',
              title: row.original.metadataJson,
            },
            row.original.metadataJson,
          )
        : '—',
  },
]

async function loadLogs() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    logs.value = await getAuditLogs(authStore.accessToken, {
      action: filters.value.action || undefined,
      entityType: filters.value.entityType || undefined,
    })
  } catch (error) {
    loadError.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tải nhật ký hệ thống.'
  } finally {
    isLoading.value = false
  }
}

onMounted(loadLogs)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader title="Nhật ký hệ thống" description="Theo dõi các hành động nghiệp vụ chính trong hệ thống." />

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadLogs" />

    <form @submit.prevent="loadLogs">
      <FilterBar>
        <div class="grid gap-1.5">
          <Label for="audit-action">Hành động</Label>
          <Input id="audit-action" v-model="filters.action" type="text" class="w-56" placeholder="Ví dụ: document.approve" />
        </div>
        <div class="grid gap-1.5">
          <Label for="audit-entity-type">Loại đối tượng</Label>
          <Input id="audit-entity-type" v-model="filters.entityType" type="text" class="w-56" placeholder="Ví dụ: Document" />
        </div>
        <Button type="submit" variant="outline" :disabled="isLoading">
          <SearchIcon class="size-4" />
          Lọc
        </Button>
      </FilterBar>
    </form>

    <DataTable
      :columns="columns"
      :data="logs"
      :is-loading="isLoading"
      empty-title="Không có bản ghi nào"
      empty-description="Thử bỏ bớt điều kiện lọc hoặc thao tác trên hệ thống rồi tải lại."
    />
  </div>
</template>
