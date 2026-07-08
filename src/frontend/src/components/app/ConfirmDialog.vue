<script setup lang="ts">
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { useConfirmHost } from '@/composables/useConfirm'

// Host duy nhất cho useConfirm() — đặt 1 lần ở App.vue
const { isOpen, options, settle } = useConfirmHost()

function onOpenChange(open: boolean) {
  if (!open) {
    settle(false)
  }
}
</script>

<template>
  <AlertDialog :open="isOpen" @update:open="onOpenChange">
    <AlertDialogContent>
      <AlertDialogHeader>
        <AlertDialogTitle>{{ options.title }}</AlertDialogTitle>
        <AlertDialogDescription v-if="options.description">
          {{ options.description }}
        </AlertDialogDescription>
      </AlertDialogHeader>
      <!-- Button thường thay AlertDialogAction/Cancel: reka tự đóng dialog trước khi @click chạy
           khiến promise resolve sai giá trị — tự settle rồi dialog đóng theo isOpen -->
      <AlertDialogFooter>
        <Button variant="outline" @click="settle(false)">
          {{ options.cancelText ?? 'Hủy' }}
        </Button>
        <Button :variant="options.destructive ? 'destructive' : 'default'" @click="settle(true)">
          {{ options.confirmText ?? 'Xác nhận' }}
        </Button>
      </AlertDialogFooter>
    </AlertDialogContent>
  </AlertDialog>
</template>
