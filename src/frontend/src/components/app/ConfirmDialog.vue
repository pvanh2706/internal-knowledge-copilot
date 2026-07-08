<script setup lang="ts">
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { buttonVariants } from '@/components/ui/button'
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
      <AlertDialogFooter>
        <AlertDialogCancel @click="settle(false)">
          {{ options.cancelText ?? 'Hủy' }}
        </AlertDialogCancel>
        <AlertDialogAction
          :class="options.destructive ? buttonVariants({ variant: 'destructive' }) : ''"
          @click="settle(true)"
        >
          {{ options.confirmText ?? 'Xác nhận' }}
        </AlertDialogAction>
      </AlertDialogFooter>
    </AlertDialogContent>
  </AlertDialog>
</template>
