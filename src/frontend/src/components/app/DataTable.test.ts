import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import type { ColumnDef } from '@tanstack/vue-table'
import DataTable from './DataTable.vue'

interface Row {
  name: string
  amount: number
}

// mount() không infer được generic TData của component → khai báo cột theo unknown cho test
const columns: ColumnDef<unknown>[] = [
  { accessorKey: 'name', header: 'Tên' },
  { accessorKey: 'amount', header: 'Số lượng' },
] as ColumnDef<unknown>[]

const data: Row[] = Array.from({ length: 12 }, (_, index) => ({
  name: `Dòng ${index + 1}`,
  amount: index,
}))

describe('DataTable', () => {
  it('render dữ liệu và phân trang client-side (10 dòng/trang)', () => {
    const wrapper = mount(DataTable, {
      props: { columns, data },
    })

    expect(wrapper.text()).toContain('Dòng 1')
    expect(wrapper.text()).not.toContain('Dòng 11')
    expect(wrapper.text()).toContain('12 dòng')
    expect(wrapper.text()).toContain('Trang 1/2')
  })

  it('hiện empty state khi không có dữ liệu', () => {
    const wrapper = mount(DataTable, {
      props: { columns, data: [], emptyTitle: 'Trống trơn' },
    })

    expect(wrapper.text()).toContain('Trống trơn')
  })

  it('lọc global filter trên mọi cột', () => {
    const wrapper = mount(DataTable, {
      props: { columns, data, globalFilter: 'Dòng 12' },
    })

    expect(wrapper.text()).toContain('Dòng 12')
    expect(wrapper.text()).toContain('1 dòng')
  })

  it('emit row-click khi bật clickableRows', async () => {
    const wrapper = mount(DataTable, {
      props: { columns, data, clickableRows: true },
    })

    await wrapper.findAll('tbody tr')[0].trigger('click')
    expect(wrapper.emitted('rowClick')?.[0]?.[0]).toEqual({ name: 'Dòng 1', amount: 0 })
  })
})
