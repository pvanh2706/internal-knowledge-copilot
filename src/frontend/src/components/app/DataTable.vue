<script setup lang="ts" generic="TData">
import {
  FlexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useVueTable,
  type ColumnDef,
  type SortingState,
} from '@tanstack/vue-table'
import { ref } from 'vue'
import { ArrowDownIcon, ArrowUpDownIcon, ArrowUpIcon } from '@lucide/vue'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import EmptyState from './EmptyState.vue'
import DataTablePagination from './DataTablePagination.vue'

const props = withDefaults(
  defineProps<{
    // eslint-disable-next-line @typescript-eslint/no-explicit-any -- ColumnDef của tanstack cần any cho value type hỗn hợp giữa các cột
    columns: ColumnDef<TData, any>[]
    data: TData[]
    isLoading?: boolean
    /** Từ khóa lọc client-side trên mọi cột (nối với FilterBar) */
    globalFilter?: string
    emptyTitle?: string
    emptyDescription?: string
    /** Bật hover + cursor pointer và emit row-click (pattern click row đổ vào form) */
    clickableRows?: boolean
    pageSize?: number
  }>(),
  {
    isLoading: false,
    globalFilter: '',
    emptyTitle: 'Chưa có dữ liệu',
    emptyDescription: undefined,
    clickableRows: false,
    pageSize: 10,
  },
)

const emit = defineEmits<{
  rowClick: [row: TData]
}>()

const sorting = ref<SortingState>([])

const table = useVueTable({
  get data() {
    return props.data
  },
  get columns() {
    return props.columns
  },
  state: {
    get sorting() {
      return sorting.value
    },
    get globalFilter() {
      return props.globalFilter
    },
  },
  onSortingChange: (updater) => {
    sorting.value = typeof updater === 'function' ? updater(sorting.value) : updater
  },
  getCoreRowModel: getCoreRowModel(),
  getSortedRowModel: getSortedRowModel(),
  getFilteredRowModel: getFilteredRowModel(),
  // Phân trang client-side trên dữ liệu đã tải — API list hiện không có tham số phân trang
  getPaginationRowModel: getPaginationRowModel(),
  initialState: {
    pagination: { pageSize: props.pageSize },
  },
})

defineExpose({ table })
</script>

<template>
  <div>
    <div class="overflow-x-auto rounded-lg border">
      <Table>
        <TableHeader>
          <TableRow v-for="headerGroup in table.getHeaderGroups()" :key="headerGroup.id">
            <TableHead v-for="header in headerGroup.headers" :key="header.id" class="whitespace-nowrap">
              <button
                v-if="!header.isPlaceholder && header.column.getCanSort()"
                type="button"
                class="inline-flex items-center gap-1.5 rounded-sm text-sm font-medium outline-none focus-visible:ring-2 focus-visible:ring-ring"
                @click="header.column.getToggleSortingHandler()?.($event)"
              >
                <FlexRender :render="header.column.columnDef.header" :props="header.getContext()" />
                <ArrowUpIcon v-if="header.column.getIsSorted() === 'asc'" class="size-3.5" aria-hidden="true" />
                <ArrowDownIcon v-else-if="header.column.getIsSorted() === 'desc'" class="size-3.5" aria-hidden="true" />
                <ArrowUpDownIcon v-else class="size-3.5 text-muted-foreground/60" aria-hidden="true" />
              </button>
              <FlexRender
                v-else-if="!header.isPlaceholder"
                :render="header.column.columnDef.header"
                :props="header.getContext()"
              />
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <template v-if="isLoading">
            <TableRow v-for="n in 5" :key="`skeleton-${n}`">
              <TableCell :colspan="columns.length">
                <Skeleton class="h-5 w-full" />
              </TableCell>
            </TableRow>
          </template>
          <template v-else-if="table.getRowModel().rows.length">
            <TableRow
              v-for="row in table.getRowModel().rows"
              :key="row.id"
              :class="clickableRows ? 'cursor-pointer' : ''"
              :tabindex="clickableRows ? 0 : undefined"
              @click="clickableRows && emit('rowClick', row.original)"
              @keydown.enter="clickableRows && emit('rowClick', row.original)"
            >
              <TableCell v-for="cell in row.getVisibleCells()" :key="cell.id">
                <FlexRender :render="cell.column.columnDef.cell" :props="cell.getContext()" />
              </TableCell>
            </TableRow>
          </template>
          <template v-else>
            <TableRow>
              <TableCell :colspan="columns.length" class="p-0">
                <EmptyState :title="emptyTitle" :description="emptyDescription" class="border-0" />
              </TableCell>
            </TableRow>
          </template>
        </TableBody>
      </Table>
    </div>
    <DataTablePagination v-if="!isLoading" :table="table" />
  </div>
</template>
