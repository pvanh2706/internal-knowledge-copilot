<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

// Form tạo eval case từ feedback — expectedAnswer/expectedKeywords điều khiển từ trang cha
defineProps<{
  disabled?: boolean
}>()

const expectedAnswer = defineModel<string>('expectedAnswer', { default: '' })
const expectedKeywords = defineModel<string>('expectedKeywords', { default: '' })

defineEmits<{
  submit: []
}>()
</script>

<template>
  <form class="grid gap-3" @submit.prevent="$emit('submit')">
    <div class="grid gap-1.5">
      <Label for="eval-expected-answer">Đáp án kỳ vọng</Label>
      <Textarea
        id="eval-expected-answer"
        v-model="expectedAnswer"
        rows="4"
        required
        placeholder="Nhập đáp án đúng dùng để chấm eval..."
      />
    </div>
    <div class="grid gap-1.5">
      <Label for="eval-expected-keywords">Từ khóa kỳ vọng</Label>
      <Textarea
        id="eval-expected-keywords"
        v-model="expectedKeywords"
        rows="2"
        placeholder="Mỗi từ khóa một dòng hoặc cách nhau bằng dấu phẩy..."
      />
    </div>
    <Button type="submit" size="sm" class="justify-self-start" :disabled="disabled">Tạo eval case</Button>
  </form>
</template>
