<script setup lang="ts">
import { h, onMounted, ref } from 'vue'
import type { ColumnDef } from '@tanstack/vue-table'
import { PlayIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import DataTable from '@/components/app/DataTable.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import FilterBar from '@/components/app/FilterBar.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import type { EvaluationCase, EvaluationRun, EvaluationRunResult } from '@/api/evaluation'
import { getEvaluationCases, getEvaluationRuns, runEvaluation } from '@/api/evaluation'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import { useToast } from '@/composables/useToast'
import { formatDateTime } from '@/lib/format'

const authStore = useAuthStore()
const toast = useToast()

const cases = ref<EvaluationCase[]>([])
const runs = ref<EvaluationRun[]>([])
const selectedRun = ref<EvaluationRun | null>(null)
const runName = ref('')
const isLoading = ref(false)
const isRunning = ref(false)
const loadError = ref('')

const caseColumns: ColumnDef<EvaluationCase>[] = [
  { accessorKey: 'question', header: 'Câu hỏi' },
  {
    accessorKey: 'expectedKeywords',
    header: 'Từ khóa kỳ vọng',
    enableSorting: false,
    cell: ({ row }) => row.original.expectedKeywords.join(', ') || '—',
  },
  { accessorKey: 'scopeType', header: 'Phạm vi' },
  {
    id: 'run',
    header: '',
    enableSorting: false,
    cell: ({ row }) =>
      h(
        Button,
        {
          variant: 'ghost',
          size: 'sm',
          disabled: isRunning.value,
          onClick: () => void submitRun(row.original.id),
        },
        () => 'Chạy',
      ),
  },
]

const runColumns: ColumnDef<EvaluationRun>[] = [
  {
    accessorKey: 'name',
    header: 'Tên',
    cell: ({ row }) => row.original.name || 'Lần chạy evaluation',
  },
  {
    accessorKey: 'passRate',
    header: 'Pass',
    cell: ({ row }) =>
      h(
        'span',
        { class: 'tabular-nums' },
        `${row.original.passedCases}/${row.original.totalCases} (${Math.round(row.original.passRate)}%)`,
      ),
  },
  {
    accessorKey: 'createdAt',
    header: 'Thời gian',
    cell: ({ row }) => formatDateTime(row.original.finishedAt || row.original.createdAt),
  },
]

const resultColumns: ColumnDef<EvaluationRunResult>[] = [
  { accessorKey: 'question', header: 'Câu hỏi' },
  {
    accessorKey: 'passed',
    header: 'Trạng thái',
    cell: ({ row }) =>
      h(StatusBadge, {
        status: row.original.passed ? 'Approved' : 'Failed',
        label: row.original.passed ? 'Pass' : 'Fail',
      }),
  },
  {
    accessorKey: 'score',
    header: 'Điểm',
    cell: ({ row }) => h('span', { class: 'tabular-nums' }, `${Math.round(row.original.score * 100)}%`),
  },
  {
    accessorKey: 'failureReason',
    header: 'Lỗi',
    enableSorting: false,
    cell: ({ row }) => row.original.failureReason || '—',
  },
]

async function loadData() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    const [loadedCases, loadedRuns] = await Promise.all([
      getEvaluationCases(authStore.accessToken),
      getEvaluationRuns(authStore.accessToken),
    ])
    cases.value = loadedCases
    runs.value = loadedRuns
    selectedRun.value = selectedRun.value
      ? loadedRuns.find((run) => run.id === selectedRun.value?.id) ?? loadedRuns[0] ?? null
      : loadedRuns[0] ?? null
  } catch (error) {
    loadError.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tải evaluation.'
  } finally {
    isLoading.value = false
  }
}

async function submitRun(caseId?: string) {
  if (!authStore.accessToken || isRunning.value) return
  isRunning.value = true

  try {
    const run = await runEvaluation(
      {
        caseId: caseId ?? null,
        name: runName.value || null,
      },
      authStore.accessToken,
    )
    toast.success(`Eval chạy xong: ${run.passedCases}/${run.totalCases} pass.`)
    runName.value = ''
    await loadData()
    selectedRun.value = run
  } catch (error) {
    toast.error(error instanceof ApiError || error instanceof Error ? error.message : 'Không thể chạy eval.')
  } finally {
    isRunning.value = false
  }
}

onMounted(loadData)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader
      title="Đánh giá"
      description="Chạy eval case để đo chất lượng câu trả lời AI theo baseline before/after."
    />

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadData" />

    <form @submit.prevent="submitRun()">
      <FilterBar>
        <div class="grid gap-1.5">
          <Label for="eval-run-name">Tên lần chạy</Label>
          <Input id="eval-run-name" v-model="runName" type="text" class="w-64" placeholder="baseline, after correction..." />
        </div>
        <Button type="submit" :disabled="isRunning">
          <PlayIcon class="size-4" />
          {{ isRunning ? 'Đang chạy...' : 'Chạy tất cả case đang bật' }}
        </Button>
      </FilterBar>
    </form>

    <div class="grid items-start gap-6 xl:grid-cols-2">
      <div>
        <p class="mb-2 text-sm font-medium">Eval case</p>
        <DataTable
          :columns="caseColumns"
          :data="cases"
          :is-loading="isLoading"
          empty-title="Chưa có eval case nào"
          empty-description="Eval case được tạo từ phản hồi sai ở trang Phản hồi."
        />
      </div>
      <div>
        <p class="mb-2 text-sm font-medium">Lần chạy</p>
        <DataTable
          :columns="runColumns"
          :data="runs"
          :is-loading="isLoading"
          clickable-rows
          empty-title="Chưa có lần chạy nào"
          @row-click="(run) => (selectedRun = run)"
        />
      </div>
    </div>

    <Card v-if="selectedRun" class="mt-6">
      <CardHeader>
        <CardTitle class="text-lg">Kết quả: {{ selectedRun.name || 'Lần chạy evaluation' }}</CardTitle>
      </CardHeader>
      <CardContent>
        <DataTable :columns="resultColumns" :data="selectedRun.results" empty-title="Không có kết quả" />
      </CardContent>
    </Card>
  </div>
</template>
