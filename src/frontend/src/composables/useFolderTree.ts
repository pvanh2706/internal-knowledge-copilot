import { computed, ref } from 'vue'
import type { FolderTreeItem } from '@/api/folders'
import { getFolderTree } from '@/api/folders'

export interface FlatFolder extends FolderTreeItem {
  depth: number
  label: string
}

// Duỗi cây folder thành danh sách thụt lề — trước đây copy y hệt ở 4 trang
export function flattenFolderTree(items: FolderTreeItem[], depth = 0): FlatFolder[] {
  return items.flatMap((item) => [
    { ...item, depth, label: `${'—'.repeat(depth)} ${item.name}`.trim() },
    ...flattenFolderTree(item.children, depth + 1),
  ])
}

export function useFolderTree() {
  const folders = ref<FolderTreeItem[]>([])
  const flatFolders = computed(() => flattenFolderTree(folders.value))

  async function loadFolders(token: string) {
    folders.value = await getFolderTree(token)
  }

  return { folders, flatFolders, loadFolders }
}
