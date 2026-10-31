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
import { cn } from '@/lib/utils'
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
    /** Khóa định danh dòng — cần khi dùng selectedRowKey */
    rowKey?: (row: TData) => string
    /** Dòng đang chọn trong layout master-detail — tô nền + aria-current */
    selectedRowKey?: string | null
    /** Nhãn đọc cho trình đọc màn hình của dòng bấm được, vd: "Xem tài liệu X" */
    rowLabel?: (row: TData) => string
    /** Bỏ viền/bo ngoài khi bảng nằm trong PanelCard; chân trang thành table-foot của card */
    bare?: boolean
    /** Đơn vị đếm ở chân bảng, vd: "tài liệu" → "6 tài liệu" */
    countLabel?: string
    /** Class thêm cho <table>, vd: min-width để cuộn ngang trên màn hẹp thay vì bóp cột */
    tableClass?: string
  }>(),
  {
    isLoading: false,
    globalFilter: '',
    emptyTitle: 'Chưa có dữ liệu',
    emptyDescription: undefined,
    clickableRows: false,
    pageSize: 10,
    rowKey: undefined,
    selectedRowKey: null,
    rowLabel: undefined,
    bare: false,
    countLabel: 'dòng',
    tableClass: undefined,
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

function isSelected(row: TData) {
  return Boolean(props.rowKey && props.selectedRowKey && props.rowKey(row) === props.selectedRowKey)
}

function activate(row: TData) {
  if (props.clickableRows) {
    emit('rowClick', row)
  }
}

// Enter/Space chỉ chọn dòng khi focus đang ở chính dòng — không nuốt phím của nút/link lồng trong ô
function onRowKeydown(event: KeyboardEvent, row: TData) {
  if (event.target !== event.currentTarget || (event.key !== 'Enter' && event.key !== ' ')) return
  event.preventDefault()
  activate(row)
}

defineExpose({ table })
</script>

<template>
  <div>
    <div :class="bare ? 'overflow-x-auto' : 'overflow-x-auto rounded-xl border bg-card'">
      <Table :class="cn('text-xs', tableClass)">
        <TableHeader>
          <TableRow v-for="headerGroup in table.getHeaderGroups()" :key="headerGroup.id" class="hover:bg-transparent">
            <TableHead
              v-for="header in headerGroup.headers"
              :key="header.id"
              :class="cn('h-auto whitespace-nowrap bg-surface px-4 py-3 text-[10px] font-bold uppercase tracking-wide text-muted-foreground', header.column.columnDef.meta?.headClass)"
            >
              <button
                v-if="!header.isPlaceholder && header.column.getCanSort()"
                type="button"
                class="inline-flex items-center gap-1.5 rounded-sm uppercase outline-none focus-visible:ring-2 focus-visible:ring-ring"
                @click="header.column.getToggleSortingHandler()?.($event)"
              >
                <FlexRender :render="header.column.columnDef.header" :props="header.getContext()" />
                <ArrowUpIcon v-if="header.column.getIsSorted() === 'asc'" class="size-3" aria-hidden="true" />
                <ArrowDownIcon v-else-if="header.column.getIsSorted() === 'desc'" class="size-3" aria-hidden="true" />
                <ArrowUpDownIcon v-else class="size-3 opacity-50" aria-hidden="true" />
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
            <TableRow v-for="n in 5" :key="`skeleton-${n}`" class="hover:bg-transparent">
              <TableCell :colspan="columns.length" class="px-4 py-3">
                <Skeleton class="h-5 w-full" />
              </TableCell>
            </TableRow>
          </template>
          <template v-else-if="table.getRowModel().rows.length">
            <TableRow
              v-for="row in table.getRowModel().rows"
              :key="row.id"
              :class="clickableRows ? 'cursor-pointer outline-none hover:bg-accent/60 focus-visible:bg-accent/60 focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring data-[state=selected]:bg-primary-soft' : ''"
              :data-state="isSelected(row.original) ? 'selected' : undefined"
              :aria-current="isSelected(row.original) ? 'true' : undefined"
              :aria-label="clickableRows && rowLabel ? rowLabel(row.original) : undefined"
              :tabindex="clickableRows ? 0 : undefined"
              @click="activate(row.original)"
              @keydown="onRowKeydown($event, row.original)"
            >
              <TableCell
                v-for="cell in row.getVisibleCells()"
                :key="cell.id"
                :class="cn('px-4 py-3 text-foreground/80', cell.column.columnDef.meta?.cellClass)"
              >
                <FlexRender :render="cell.column.columnDef.cell" :props="cell.getContext()" />
              </TableCell>
            </TableRow>
          </template>
          <template v-else>
            <TableRow class="hover:bg-transparent">
              <TableCell :colspan="columns.length" class="p-3 whitespace-normal">
                <EmptyState :title="emptyTitle" :description="emptyDescription" />
              </TableCell>
            </TableRow>
          </template>
        </TableBody>
      </Table>
    </div>
    <DataTablePagination v-if="!isLoading" :table="table" :bare="bare" :count-label="countLabel" />
  </div>
</template>
