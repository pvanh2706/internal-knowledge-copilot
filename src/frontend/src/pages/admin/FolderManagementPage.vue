<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { FolderIcon, FolderTreeIcon, PlusIcon, Trash2Icon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import EmptyState from '@/components/app/EmptyState.vue'
import ErrorState from '@/components/app/ErrorState.vue'
import FolderSelect from '@/components/app/FolderSelect.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import type { FolderDetail } from '@/api/folders'
import {
  createFolder,
  deleteFolder,
  getFolderDetail,
  updateFolder,
  updateFolderPermissions,
} from '@/api/folders'
import { ApiError } from '@/api/http'
import type { Team } from '@/api/teams'
import { getTeams } from '@/api/teams'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useFolderTree } from '@/composables/useFolderTree'
import { useToast } from '@/composables/useToast'

const authStore = useAuthStore()
const toast = useToast()
const { confirm } = useConfirm()
const { flatFolders, loadFolders } = useFolderTree()

const teams = ref<Team[]>([])
const selectedFolder = ref<FolderDetail | null>(null)
const isLoading = ref(false)
const isSaving = ref(false)
const loadError = ref('')
const createError = ref('')
const isCreateDialogOpen = ref(false)

const createForm = ref({ parentId: '', name: '' })
const editForm = ref({ parentId: '', name: '' })
const permissionForm = ref<Record<string, boolean>>({})

async function loadData() {
  if (!authStore.accessToken) return
  loadError.value = ''
  isLoading.value = true

  try {
    await Promise.all([loadFolders(authStore.accessToken), loadTeams()])
  } catch (error) {
    loadError.value = toMessage(error, 'Không thể tải cây thư mục.')
  } finally {
    isLoading.value = false
  }
}

async function loadTeams() {
  if (!authStore.accessToken) return
  teams.value = await getTeams(authStore.accessToken)
}

function openCreateDialog() {
  createForm.value = { parentId: '', name: '' }
  createError.value = ''
  isCreateDialogOpen.value = true
}

async function submitCreateFolder() {
  if (!authStore.accessToken || isSaving.value) return
  createError.value = ''
  isSaving.value = true

  try {
    await createFolder(
      { parentId: createForm.value.parentId || undefined, name: createForm.value.name },
      authStore.accessToken,
    )
    isCreateDialogOpen.value = false
    toast.success('Đã tạo thư mục.')
    await loadData()
  } catch (error) {
    createError.value = toMessage(error, 'Không thể tạo thư mục.')
  } finally {
    isSaving.value = false
  }
}

async function selectFolder(folderId: string) {
  if (!authStore.accessToken) return

  try {
    selectedFolder.value = await getFolderDetail(folderId, authStore.accessToken)
    editForm.value = {
      parentId: selectedFolder.value.parentId ?? '',
      name: selectedFolder.value.name,
    }
    permissionForm.value = Object.fromEntries(teams.value.map((team) => [team.id, false]))
    for (const permission of selectedFolder.value.teamPermissions) {
      permissionForm.value[permission.teamId] = permission.canView
    }
  } catch (error) {
    toast.error(toMessage(error, 'Không thể tải chi tiết thư mục.'))
  }
}

async function submitUpdateFolder() {
  if (!authStore.accessToken || !selectedFolder.value || isSaving.value) return
  isSaving.value = true

  try {
    await updateFolder(
      selectedFolder.value.id,
      { parentId: editForm.value.parentId || undefined, name: editForm.value.name },
      authStore.accessToken,
    )
    toast.success('Đã cập nhật thư mục.')
    await loadData()
    await selectFolder(selectedFolder.value.id)
  } catch (error) {
    toast.error(toMessage(error, 'Không thể cập nhật thư mục.'))
  } finally {
    isSaving.value = false
  }
}

async function submitPermissions() {
  if (!authStore.accessToken || !selectedFolder.value || isSaving.value) return
  isSaving.value = true

  try {
    await updateFolderPermissions(
      selectedFolder.value.id,
      teams.value.map((team) => ({
        teamId: team.id,
        canView: Boolean(permissionForm.value[team.id]),
      })),
      authStore.accessToken,
    )
    toast.success('Đã cập nhật quyền thư mục.')
    await selectFolder(selectedFolder.value.id)
  } catch (error) {
    toast.error(toMessage(error, 'Không thể cập nhật quyền.'))
  } finally {
    isSaving.value = false
  }
}

async function submitDeleteFolder() {
  if (!authStore.accessToken || !selectedFolder.value || isSaving.value) return

  const confirmed = await confirm({
    title: `Xóa thư mục "${selectedFolder.value.name}"?`,
    description: 'Thư mục sẽ bị xóa mềm — tài liệu bên trong không còn truy cập được từ cây thư mục.',
    confirmText: 'Xóa thư mục',
    destructive: true,
  })
  if (!confirmed) return

  isSaving.value = true
  try {
    await deleteFolder(selectedFolder.value.id, authStore.accessToken)
    selectedFolder.value = null
    toast.success('Đã xóa mềm thư mục.')
    await loadData()
  } catch (error) {
    toast.error(toMessage(error, 'Không thể xóa thư mục.'))
  } finally {
    isSaving.value = false
  }
}

function toMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback
}

onMounted(loadData)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader title="Thư mục" description="Tạo cây thư mục và gán quyền xem theo team.">
      <template #actions>
        <Button @click="openCreateDialog">
          <PlusIcon class="size-4" />
          Tạo thư mục
        </Button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :message="loadError" class="mb-4" @retry="loadData" />

    <div class="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.9fr)]">
      <Card>
        <CardHeader>
          <CardTitle class="text-lg">Cây thư mục</CardTitle>
        </CardHeader>
        <CardContent>
          <div v-if="isLoading" class="grid gap-2">
            <Skeleton v-for="n in 5" :key="n" class="h-10 rounded-md" />
          </div>
          <EmptyState
            v-else-if="flatFolders.length === 0"
            :icon="FolderTreeIcon"
            title="Chưa có thư mục nào"
            description="Bấm “Tạo thư mục” để tạo thư mục gốc đầu tiên."
          />
          <ul v-else class="grid gap-0.5">
            <li v-for="folder in flatFolders" :key="folder.id">
              <button
                type="button"
                class="flex w-full items-center gap-2 rounded-md px-3 py-2 text-left text-sm outline-none transition-colors hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring"
                :class="selectedFolder?.id === folder.id ? 'bg-accent font-medium' : ''"
                :style="{ paddingLeft: `${12 + folder.depth * 20}px` }"
                @click="selectFolder(folder.id)"
              >
                <FolderIcon class="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                <span class="min-w-0 flex-1 truncate">{{ folder.name }}</span>
                <span class="shrink-0 font-mono text-xs text-muted-foreground">{{ folder.path }}</span>
              </button>
            </li>
          </ul>
        </CardContent>
      </Card>

      <EmptyState
        v-if="!selectedFolder"
        :icon="FolderIcon"
        title="Chọn một thư mục để xem chi tiết"
        description="Bấm vào thư mục trong cây để sửa tên, chuyển vị trí và gán quyền."
      />

      <Card v-else>
        <CardHeader>
          <CardTitle class="text-lg">{{ selectedFolder.name }}</CardTitle>
        </CardHeader>
        <CardContent class="grid gap-5">
          <form class="grid gap-4" @submit.prevent="submitUpdateFolder">
            <div class="grid gap-1.5">
              <Label for="folder-parent">Thư mục cha</Label>
              <FolderSelect
                v-model="editForm.parentId"
                trigger-id="folder-parent"
                :folders="flatFolders"
                :disabled-id="selectedFolder.id"
                none-label="Thư mục gốc"
              />
            </div>
            <div class="grid gap-1.5">
              <Label for="folder-name">Tên thư mục</Label>
              <Input id="folder-name" v-model="editForm.name" type="text" required />
            </div>
            <Button type="submit" size="sm" class="justify-self-start" :disabled="isSaving">
              {{ isSaving ? 'Đang lưu...' : 'Lưu thư mục' }}
            </Button>
          </form>

          <Separator />

          <form class="grid gap-3" @submit.prevent="submitPermissions">
            <p class="text-sm font-medium">Quyền xem theo team</p>
            <p v-if="teams.length === 0" class="text-sm text-muted-foreground">Chưa có team nào để gán quyền.</p>
            <label
              v-for="team in teams"
              :key="team.id"
              class="flex min-h-9 cursor-pointer items-center gap-2.5 rounded-md px-1 text-sm"
            >
              <Checkbox v-model="permissionForm[team.id]" />
              {{ team.name }}
            </label>
            <Button type="submit" size="sm" variant="outline" class="justify-self-start" :disabled="isSaving">
              Lưu quyền
            </Button>
          </form>

          <Separator />

          <div class="flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3">
            <p class="text-sm text-muted-foreground">Xóa mềm thư mục này khỏi cây.</p>
            <Button variant="destructive" size="sm" type="button" :disabled="isSaving" @click="submitDeleteFolder">
              <Trash2Icon class="size-4" />
              Xóa thư mục
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>

    <Dialog v-model:open="isCreateDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Tạo thư mục</DialogTitle>
          <DialogDescription>Thư mục mới có thể nằm ở gốc hoặc bên trong thư mục khác.</DialogDescription>
        </DialogHeader>
        <form id="create-folder-form" class="grid gap-4" @submit.prevent="submitCreateFolder">
          <div class="grid gap-1.5">
            <Label for="create-folder-parent">Thư mục cha</Label>
            <FolderSelect
              v-model="createForm.parentId"
              trigger-id="create-folder-parent"
              :folders="flatFolders"
              none-label="Thư mục gốc"
            />
          </div>
          <div class="grid gap-1.5">
            <Label for="create-folder-name">Tên thư mục</Label>
            <Input id="create-folder-name" v-model="createForm.name" type="text" required />
          </div>
          <p v-if="createError" class="text-sm text-destructive" role="alert">{{ createError }}</p>
        </form>
        <DialogFooter>
          <Button variant="outline" type="button" :disabled="isSaving" @click="isCreateDialogOpen = false">Hủy</Button>
          <Button type="submit" form="create-folder-form" :disabled="isSaving">
            {{ isSaving ? 'Đang tạo...' : 'Tạo thư mục' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
