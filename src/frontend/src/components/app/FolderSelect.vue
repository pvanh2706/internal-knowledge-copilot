<script setup lang="ts">
import { computed } from 'vue'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { FlatFolder } from '@/composables/useFolderTree'

const props = defineProps<{
  folders: FlatFolder[]
  /** Nhãn cho lựa chọn "không chọn folder" (vd: "Tất cả thư mục", "Thư mục gốc"); bỏ qua nếu bắt buộc chọn */
  noneLabel?: string
  /** Id folder bị vô hiệu (vd: chính folder đang sửa để tránh tự làm cha) */
  disabledId?: string
  placeholder?: string
  triggerId?: string
}>()

// v-model bên ngoài dùng '' cho "không chọn"; reka SelectItem không nhận value rỗng nên map qua sentinel
const NONE = '__none__'
const model = defineModel<string>({ default: '' })

const innerValue = computed({
  get: () => (model.value === '' ? (props.noneLabel ? NONE : '') : model.value),
  set: (value: string) => {
    model.value = value === NONE ? '' : value
  },
})
</script>

<template>
  <Select v-model="innerValue">
    <SelectTrigger :id="triggerId" class="w-full min-w-52">
      <SelectValue :placeholder="placeholder ?? 'Chọn thư mục'" />
    </SelectTrigger>
    <SelectContent>
      <SelectItem v-if="noneLabel" :value="NONE">{{ noneLabel }}</SelectItem>
      <SelectItem
        v-for="folder in folders"
        :key="folder.id"
        :value="folder.id"
        :disabled="folder.id === disabledId"
      >
        {{ folder.label }}
      </SelectItem>
    </SelectContent>
  </Select>
</template>
