<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import type { FolderDetail, FolderTreeItem } from '../../api/folders'
import {
  createFolder,
  deleteFolder,
  getFolderDetail,
  getFolderTree,
  updateFolder,
  updateFolderPermissions,
} from '../../api/folders'
import { ApiError } from '../../api/http'
import type { Team } from '../../api/teams'
import { getTeams } from '../../api/teams'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const folders = ref<FolderTreeItem[]>([])
const teams = ref<Team[]>([])
const selectedFolder = ref<FolderDetail | null>(null)
const errorMessage = ref('')
const successMessage = ref('')

const createForm = ref({
  parentId: '',
  name: '',
})

const editForm = ref({
  parentId: '',
  name: '',
})

const permissionForm = ref<Record<string, boolean>>({})

const flattenedFolders = computed(() => flattenFolders(folders.value))

async function loadData() {
  if (!authStore.accessToken) {
    return
  }

  const [folderTree, teamList] = await Promise.all([getFolderTree(authStore.accessToken), getTeams(authStore.accessToken)])
  folders.value = folderTree
  teams.value = teamList
}

async function submitCreateFolder() {
  if (!authStore.accessToken) {
    return
  }

  await runAction(async () => {
    await createFolder(
      {
        parentId: createForm.value.parentId || undefined,
        name: createForm.value.name,
      },
      authStore.accessToken!,
    )
    createForm.value.name = ''
    await loadData()
  }, 'Đã tạo folder.')
}

async function selectFolder(folderId: string) {
  if (!authStore.accessToken) {
    return
  }

  selectedFolder.value = await getFolderDetail(folderId, authStore.accessToken)
  editForm.value = {
    parentId: selectedFolder.value.parentId ?? '',
    name: selectedFolder.value.name,
  }
  permissionForm.value = Object.fromEntries(teams.value.map((team) => [team.id, false]))
  for (const permission of selectedFolder.value.teamPermissions) {
    permissionForm.value[permission.teamId] = permission.canView
  }
}

async function submitUpdateFolder() {
  if (!authStore.accessToken || !selectedFolder.value) {
    return
  }

  await runAction(async () => {
    await updateFolder(
      selectedFolder.value!.id,
      {
        parentId: editForm.value.parentId || undefined,
        name: editForm.value.name,
      },
      authStore.accessToken!,
    )
    await loadData()
    await selectFolder(selectedFolder.value!.id)
  }, 'Đã cập nhật folder.')
}

async function submitPermissions() {
  if (!authStore.accessToken || !selectedFolder.value) {
    return
  }

  await runAction(async () => {
    await updateFolderPermissions(
      selectedFolder.value!.id,
      teams.value.map((team) => ({
        teamId: team.id,
        canView: Boolean(permissionForm.value[team.id]),
      })),
      authStore.accessToken!,
    )
    await selectFolder(selectedFolder.value!.id)
  }, 'Đã cập nhật quyền folder.')
}

async function submitDeleteFolder() {
  if (!authStore.accessToken || !selectedFolder.value) {
    return
  }

  await runAction(async () => {
    await deleteFolder(selectedFolder.value!.id, authStore.accessToken!)
    selectedFolder.value = null
    await loadData()
  }, 'Đã xóa mềm folder.')
}

async function runAction(action: () => Promise<void>, success: string) {
  errorMessage.value = ''
  successMessage.value = ''

  try {
    await action()
    successMessage.value = success
  } catch (error) {
    errorMessage.value = error instanceof ApiError ? error.message : 'Không thể xử lý yêu cầu.'
  }
}

function flattenFolders(items: FolderTreeItem[], level = 0): Array<FolderTreeItem & { label: string }> {
  return items.flatMap((item) => [
    { ...item, label: `${'--'.repeat(level)} ${item.name}`.trim() },
    ...flattenFolders(item.children, level + 1),
  ])
}

onMounted(loadData)
</script>

<template>
  <section class="panel management-page">
    <div>
      <h2>Quản lý thư mục</h2>
      <p>Tạo cây thư mục và gán quyền xem theo team.</p>
    </div>

    <form class="management-form" @submit.prevent="submitCreateFolder">
      <select v-model="createForm.parentId">
        <option value="">Folder gốc</option>
        <option v-for="folder in flattenedFolders" :key="folder.id" :value="folder.id">{{ folder.label }}</option>
      </select>
      <input v-model="createForm.name" type="text" placeholder="Tên folder" required />
      <button type="submit">Tạo folder</button>
    </form>

    <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="form-success">{{ successMessage }}</p>

    <div class="split-layout">
      <div class="folder-tree">
        <h3>Cây thư mục</h3>
        <p v-if="flattenedFolders.length === 0">Chưa có folder.</p>
        <button
          v-for="folder in flattenedFolders"
          :key="folder.id"
          type="button"
          :class="{ selected: selectedFolder?.id === folder.id }"
          @click="selectFolder(folder.id)"
        >
          {{ folder.label }}
          <small>{{ folder.path }}</small>
        </button>
      </div>

      <div v-if="selectedFolder" class="folder-detail">
        <h3>Chi tiết folder</h3>

        <form class="stack-form" @submit.prevent="submitUpdateFolder">
          <label>
            Folder cha
            <select v-model="editForm.parentId">
              <option value="">Folder gốc</option>
              <option v-for="folder in flattenedFolders" :key="folder.id" :value="folder.id" :disabled="folder.id === selectedFolder.id">
                {{ folder.label }}
              </option>
            </select>
          </label>

          <label>
            Tên folder
            <input v-model="editForm.name" type="text" required />
          </label>

          <button type="submit">Lưu folder</button>
        </form>

        <form class="permission-list" @submit.prevent="submitPermissions">
          <h3>Quyền xem theo team</h3>
          <label v-for="team in teams" :key="team.id">
            <input v-model="permissionForm[team.id]" type="checkbox" />
            {{ team.name }}
          </label>
          <button type="submit">Lưu quyền</button>
        </form>

        <button class="danger-button" type="button" @click="submitDeleteFolder">Xóa mềm folder</button>
      </div>
    </div>
  </section>
</template>
