<script setup lang="ts">
import { computed, ref } from 'vue'
import { ChevronDownIcon, FileTextIcon, FolderIcon, GlobeIcon, XIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import FolderSelect from '@/components/app/FolderSelect.vue'
import type { AiScopeType } from '@/api/ai'
import type { DocumentListItem } from '@/api/documents'
import type { FlatFolder } from '@/composables/useFolderTree'

// Chọn phạm vi hỏi AI — dùng chung giữa AiQuestionPage và RetrievalExplainPage.
// variant "chip" (mặc định): chip + popover; "inline": các ô chọn đặt thẳng trên trang theo B1 AI Workspace.
const props = withDefaults(
  defineProps<{
    folders: FlatFolder[]
    /** Danh sách tài liệu đã index để chọn scope Document */
    documents: DocumentListItem[]
    variant?: 'chip' | 'inline'
    /** Tiền tố id cho label/trigger khi có nhiều bộ chọn trên cùng trang */
    idPrefix?: string
  }>(),
  { variant: 'chip', idPrefix: 'scope' },
)

const scopeType = defineModel<AiScopeType>('scopeType', { default: 'All' })
const folderId = defineModel<string>('folderId', { default: '' })
const documentId = defineModel<string>('documentId', { default: '' })

const isOpen = ref(false)

const chipLabel = computed(() => {
  if (scopeType.value === 'Folder') {
    const folder = props.folders.find((item) => item.id === folderId.value)
    return folder ? folder.name : 'Chọn thư mục...'
  }
  if (scopeType.value === 'Document') {
    const document = props.documents.find((item) => item.id === documentId.value)
    return document ? document.title : 'Chọn tài liệu...'
  }
  return 'Tất cả nguồn'
})

const chipIcon = computed(() => {
  if (scopeType.value === 'Folder') return FolderIcon
  if (scopeType.value === 'Document') return FileTextIcon
  return GlobeIcon
})

function resetScope() {
  scopeType.value = 'All'
  folderId.value = ''
  documentId.value = ''
}
</script>

<template>
  <div v-if="variant === 'inline'" class="grid gap-3">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <Label :for="`${idPrefix}-type`" class="text-xs font-normal text-muted-foreground">
        <FolderIcon class="size-4 text-primary-soft-foreground" aria-hidden="true" />
        Phạm vi truy xuất
      </Label>
      <Select v-model="scopeType">
        <SelectTrigger :id="`${idPrefix}-type`" class="h-10! w-auto min-w-54 bg-card">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="All">Tất cả nguồn được phép</SelectItem>
          <SelectItem value="Folder">Theo thư mục</SelectItem>
          <SelectItem value="Document">Theo tài liệu</SelectItem>
        </SelectContent>
      </Select>
    </div>

    <div v-if="scopeType === 'Folder'" class="flex flex-wrap items-center gap-2.5">
      <Label :for="`${idPrefix}-folder`" class="text-xs font-normal text-muted-foreground">Thư mục</Label>
      <div class="min-w-0 max-w-72 flex-1">
        <FolderSelect v-model="folderId" :trigger-id="`${idPrefix}-folder`" :folders="folders" placeholder="Chọn thư mục" />
      </div>
    </div>

    <div v-if="scopeType === 'Document'" class="flex flex-wrap items-center gap-2.5">
      <Label :for="`${idPrefix}-document`" class="text-xs font-normal text-muted-foreground">Tài liệu</Label>
      <div class="min-w-0 max-w-72 flex-1">
        <Select v-model="documentId" :disabled="documents.length === 0">
          <SelectTrigger :id="`${idPrefix}-document`" class="w-full">
            <SelectValue :placeholder="documents.length ? 'Chọn tài liệu đã lập chỉ mục' : 'Chưa có tài liệu đã lập chỉ mục'" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem v-for="document in documents" :key="document.id" :value="document.id">
              {{ document.title }} — {{ document.folderPath }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>
    </div>
  </div>

  <Popover v-else v-model:open="isOpen">
    <div class="inline-flex items-center gap-0.5 rounded-full border bg-background pr-1 text-sm">
      <PopoverTrigger as-child>
        <button
          type="button"
          class="inline-flex min-h-8 max-w-64 items-center gap-1.5 rounded-full px-3 outline-none transition-colors hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring"
          aria-label="Chọn phạm vi hỏi AI"
        >
          <component :is="chipIcon" class="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
          <span class="truncate">{{ chipLabel }}</span>
          <ChevronDownIcon class="size-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />
        </button>
      </PopoverTrigger>
      <button
        v-if="scopeType !== 'All'"
        type="button"
        class="grid size-6 place-items-center rounded-full outline-none transition-colors hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring"
        aria-label="Bỏ phạm vi, hỏi trên tất cả nguồn"
        @click="resetScope"
      >
        <XIcon class="size-3.5" aria-hidden="true" />
      </button>
    </div>
    <PopoverContent class="grid w-80 gap-3" align="start">
      <div class="grid gap-1.5">
        <Label for="scope-type">Phạm vi</Label>
        <Select v-model="scopeType">
          <SelectTrigger id="scope-type" class="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="All">Tất cả nguồn được phép</SelectItem>
            <SelectItem value="Folder">Theo thư mục</SelectItem>
            <SelectItem value="Document">Theo tài liệu</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div v-if="scopeType === 'Folder'" class="grid gap-1.5">
        <Label for="scope-folder">Thư mục</Label>
        <FolderSelect v-model="folderId" trigger-id="scope-folder" :folders="folders" />
      </div>

      <div v-if="scopeType === 'Document'" class="grid gap-1.5">
        <Label for="scope-document">Tài liệu đã lập chỉ mục</Label>
        <Select v-model="documentId">
          <SelectTrigger id="scope-document" class="w-full">
            <SelectValue placeholder="Chọn tài liệu" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem v-for="document in documents" :key="document.id" :value="document.id">
              {{ document.title }} — {{ document.folderPath }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>

      <Button size="sm" class="justify-self-end" type="button" @click="isOpen = false">Xong</Button>
    </PopoverContent>
  </Popover>
</template>
