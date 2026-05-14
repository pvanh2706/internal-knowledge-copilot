<script setup lang="ts">
import { onMounted, ref } from 'vue'
import type { EvaluationCase, EvaluationRun } from '../../api/evaluation'
import { getEvaluationCases, getEvaluationRuns, runEvaluation } from '../../api/evaluation'
import { ApiError } from '../../api/http'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const cases = ref<EvaluationCase[]>([])
const runs = ref<EvaluationRun[]>([])
const selectedRun = ref<EvaluationRun | null>(null)
const runName = ref('')
const errorMessage = ref('')
const successMessage = ref('')
const isRunning = ref(false)

async function loadData() {
  if (!authStore.accessToken) return
  const [loadedCases, loadedRuns] = await Promise.all([
    getEvaluationCases(authStore.accessToken),
    getEvaluationRuns(authStore.accessToken),
  ])
  cases.value = loadedCases
  runs.value = loadedRuns
  selectedRun.value = selectedRun.value
    ? loadedRuns.find((run) => run.id === selectedRun.value?.id) ?? loadedRuns[0] ?? null
    : loadedRuns[0] ?? null
}

async function submitRun(caseId?: string) {
  if (!authStore.accessToken) return
  errorMessage.value = ''
  successMessage.value = ''
  isRunning.value = true

  try {
    const run = await runEvaluation(
      {
        caseId: caseId ?? null,
        name: runName.value || null,
      },
      authStore.accessToken,
    )
    successMessage.value = `Eval run xong: ${run.passedCases}/${run.totalCases} pass.`
    runName.value = ''
    await loadData()
    selectedRun.value = run
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể chạy eval.'
  } finally {
    isRunning.value = false
  }
}

function selectRun(run: EvaluationRun) {
  selectedRun.value = run
}

function formatPassRate(run: EvaluationRun) {
  return `${Math.round(run.passRate)}%`
}

function formatKeywords(item: EvaluationCase) {
  return item.expectedKeywords.join(', ') || '-'
}

onMounted(async () => {
  try {
    await loadData()
  } catch (error) {
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Không thể tải evaluation.'
  }
})
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Đánh giá</h2>
      <p>Chạy eval cases để đo chất lượng câu trả lời AI theo baseline before/after.</p>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <form class="stack-form" @submit.prevent="submitRun()">
      <label>
        Tên lần chạy
        <input v-model="runName" placeholder="baseline, after correction..." />
      </label>
      <button type="submit" :disabled="isRunning">Chạy tất cả case đang bật</button>
    </form>

    <div class="split-layout">
      <section class="answer-panel">
        <h3>Eval case</h3>
        <table class="data-table">
          <thead>
            <tr>
              <th>Câu hỏi</th>
              <th>Từ khóa</th>
              <th>Phạm vi</th>
              <th>Chạy</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in cases" :key="item.id">
              <td>{{ item.question }}</td>
              <td>{{ formatKeywords(item) }}</td>
              <td>{{ item.scopeType }}</td>
              <td>
                <button type="button" class="text-button" :disabled="isRunning" @click="submitRun(item.id)">Chạy</button>
              </td>
            </tr>
          </tbody>
        </table>
      </section>

      <section class="answer-panel">
        <h3>Lần chạy</h3>
        <table class="data-table">
          <thead>
            <tr>
              <th>Tên</th>
              <th>Pass</th>
              <th>Thời gian</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="run in runs" :key="run.id" class="clickable-row" @click="selectRun(run)">
              <td>{{ run.name || 'Lần chạy evaluation' }}</td>
              <td>{{ run.passedCases }}/{{ run.totalCases }} ({{ formatPassRate(run) }})</td>
              <td>{{ run.finishedAt || run.createdAt }}</td>
            </tr>
          </tbody>
        </table>
      </section>
    </div>

    <section v-if="selectedRun" class="answer-panel">
      <h3>Kết quả chạy</h3>
      <table class="data-table">
        <thead>
          <tr>
            <th>Câu hỏi</th>
            <th>Trạng thái</th>
            <th>Điểm</th>
            <th>Lỗi</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="result in selectedRun.results" :key="result.id">
            <td>{{ result.question }}</td>
            <td>{{ result.passed ? 'Pass' : 'Fail' }}</td>
            <td>{{ Math.round(result.score * 100) }}%</td>
            <td>{{ result.failureReason || '-' }}</td>
          </tr>
        </tbody>
      </table>
    </section>
  </section>
</template>
