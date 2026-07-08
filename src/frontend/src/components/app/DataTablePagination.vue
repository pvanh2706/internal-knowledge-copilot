<script setup lang="ts" generic="TData">
import type { Table } from '@tanstack/vue-table'
import { ChevronLeftIcon, ChevronRightIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'

defineProps<{
  table: Table<TData>
}>()
</script>

<template>
  <div class="flex items-center justify-between gap-3 pt-3 text-sm text-muted-foreground">
    <p>{{ table.getFilteredRowModel().rows.length }} dòng</p>
    <div v-if="table.getPageCount() > 1" class="flex items-center gap-2">
      <p class="tabular-nums">
        Trang {{ table.getState().pagination.pageIndex + 1 }}/{{ table.getPageCount() }}
      </p>
      <Button
        variant="outline"
        size="icon-sm"
        aria-label="Trang trước"
        :disabled="!table.getCanPreviousPage()"
        @click="table.previousPage()"
      >
        <ChevronLeftIcon class="size-4" />
      </Button>
      <Button
        variant="outline"
        size="icon-sm"
        aria-label="Trang sau"
        :disabled="!table.getCanNextPage()"
        @click="table.nextPage()"
      >
        <ChevronRightIcon class="size-4" />
      </Button>
    </div>
  </div>
</template>
