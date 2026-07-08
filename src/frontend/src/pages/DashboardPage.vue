<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ChartColumnIcon } from '@lucide/vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import EmptyState from '@/components/app/EmptyState.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatCard from '@/components/app/StatCard.vue'
import type { DashboardSummary, NamedCount } from '@/api/dashboard'
import { getDashboardSummary } from '@/api/dashboard'
import { ApiError } from '@/api/http'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
const summary = ref<DashboardSummary | null>(null)
const errorMessage = ref('')
const isLoading = ref(false)

const canViewKpi = computed(() => authStore.isReviewer || authStore.isAdmin)

const feedbackTotal = computed(() => (summary.value?.feedbackCorrectCount ?? 0) + (summary.value?.feedbackIncorrectCount ?? 0))
const feedbackCorrectRate = computed(() => {
  if (!summary.value || feedbackTotal.value === 0) return '0%'
  return `${Math.round((summary.value.feedbackCorrectCount / feedbackTotal.value) * 100)}%`
})
const latestEvaluationPassRate = computed(() => {
  if (!summary.value || summary.value.latestEvaluationPassRate == null) return 'Chưa chạy'
  return `${Math.round(summary.value.latestEvaluationPassRate)}%`
})

const sourceTypeLabels: Record<string, string> = {
  Document: 'Tài liệu',
  Wiki: 'Wiki',
  Correction: 'Hiệu chỉnh',
}

async function loadSummary() {
  if (!authStore.accessToken || !canViewKpi.value) return
  errorMessage.value = ''
  isLoading.value = true

  try {
    summary.value = await getDashboardSummary(authStore.accessToken)
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tải dashboard.'
  } finally {
    isLoading.value = false
  }
}

function countOf(items: NamedCount[] | undefined, name: string) {
  return items?.find((item) => item.name === name)?.count ?? 0
}

onMounted(loadSummary)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader title="Tổng quan" description="Theo dõi tài liệu, wiki, lượt hỏi AI và feedback." />

    <EmptyState
      v-if="!canViewKpi"
      :icon="ChartColumnIcon"
      title="Dashboard KPI chỉ dành cho Admin và Reviewer"
      description="Bạn vẫn có thể tra cứu tài liệu và hỏi AI từ menu bên trái."
    />

    <ErrorState v-else-if="errorMessage" :message="errorMessage" @retry="loadSummary" />

    <div v-else-if="isLoading" class="grid gap-4">
      <div class="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Skeleton v-for="n in 8" :key="n" class="h-[104px] rounded-lg" />
      </div>
      <Skeleton class="h-64 rounded-lg" />
    </div>

    <div v-else-if="summary" class="grid gap-6">
      <div class="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Tài liệu đã duyệt" :value="countOf(summary.documentCounts, 'Approved')" />
        <StatCard label="Tài liệu chờ duyệt" :value="countOf(summary.documentCounts, 'PendingReview')" />
        <StatCard label="Wiki đã xuất bản" :value="countOf(summary.wikiCounts, 'Published')" />
        <StatCard label="Lượt hỏi AI" :value="summary.aiQuestionCount" />
        <StatCard label="Tỷ lệ feedback đúng" :value="feedbackCorrectRate" :hint="`${feedbackTotal} lượt feedback`" />
        <StatCard label="Feedback sai chờ xử lý" :value="summary.incorrectFeedbackPendingCount" />
        <StatCard label="Eval case đang bật" :value="summary.evaluationCaseCount" />
        <StatCard
          label="Tỷ lệ eval pass"
          :value="latestEvaluationPassRate"
          :hint="summary.latestEvaluationTotalCases ? `${summary.latestEvaluationPassedCases}/${summary.latestEvaluationTotalCases} pass` : undefined"
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle class="text-lg">Nguồn được trích dẫn nhiều</CardTitle>
        </CardHeader>
        <CardContent>
          <EmptyState
            v-if="summary.topCitedSources.length === 0"
            title="Chưa có nguồn nào được trích dẫn"
            description="Số liệu sẽ xuất hiện sau khi có lượt hỏi AI đầu tiên."
          />
          <div v-else class="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Nguồn</TableHead>
                  <TableHead>Thư mục</TableHead>
                  <TableHead class="text-right">Lượt</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                <TableRow
                  v-for="source in summary.topCitedSources"
                  :key="`${source.sourceType}-${source.title}-${source.folderPath}`"
                >
                  <TableCell>
                    <span class="text-muted-foreground">{{ sourceTypeLabels[source.sourceType] ?? source.sourceType }}</span>
                    · {{ source.title }}
                  </TableCell>
                  <TableCell class="text-muted-foreground">{{ source.folderPath || '—' }}</TableCell>
                  <TableCell class="text-right tabular-nums">{{ source.count }}</TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  </div>
</template>
