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
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Khong the chay eval.'
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
    errorMessage.value = error instanceof ApiError || error instanceof Error ? error.message : 'Khong the tai evaluation.'
  }
})
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Evaluation</h2>
      <p>Chay eval cases de do chat luong cau tra loi AI theo baseline before/after.</p>
    </div>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <form class="stack-form" @submit.prevent="submitRun()">
      <label>
        Run name
        <input v-model="runName" placeholder="baseline, after correction..." />
      </label>
      <button type="submit" :disabled="isRunning">Run all active cases</button>
    </form>

    <div class="split-layout">
      <section class="answer-panel">
        <h3>Eval cases</h3>
        <table class="data-table">
          <thead>
            <tr>
              <th>Question</th>
              <th>Keywords</th>
              <th>Scope</th>
              <th>Run</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in cases" :key="item.id">
              <td>{{ item.question }}</td>
              <td>{{ formatKeywords(item) }}</td>
              <td>{{ item.scopeType }}</td>
              <td>
                <button type="button" class="text-button" :disabled="isRunning" @click="submitRun(item.id)">Run</button>
              </td>
            </tr>
          </tbody>
        </table>
      </section>

      <section class="answer-panel">
        <h3>Runs</h3>
        <table class="data-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Pass</th>
              <th>Time</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="run in runs" :key="run.id" class="clickable-row" @click="selectRun(run)">
              <td>{{ run.name || 'Evaluation run' }}</td>
              <td>{{ run.passedCases }}/{{ run.totalCases }} ({{ formatPassRate(run) }})</td>
              <td>{{ run.finishedAt || run.createdAt }}</td>
            </tr>
          </tbody>
        </table>
      </section>
    </div>

    <section v-if="selectedRun" class="answer-panel">
      <h3>Run result</h3>
      <table class="data-table">
        <thead>
          <tr>
            <th>Question</th>
            <th>Status</th>
            <th>Score</th>
            <th>Failure</th>
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
