import '@tanstack/vue-table'
import type { RowData } from '@tanstack/vue-table'

declare module '@tanstack/vue-table' {
  // Class bổ sung theo từng cột cho DataTable (vd: cho phép cột tiêu đề xuống dòng)
  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- phải giữ đúng chữ ký generic gốc để merge interface
  interface ColumnMeta<TData extends RowData, TValue> {
    headClass?: string
    cellClass?: string
  }
}
