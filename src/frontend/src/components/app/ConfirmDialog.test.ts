import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import ConfirmDialog from './ConfirmDialog.vue'
import { useConfirm } from '@/composables/useConfirm'

describe('ConfirmDialog + useConfirm', () => {
  it('mở dialog, bấm xác nhận trả về true', async () => {
    const wrapper = mount(ConfirmDialog, {
      attachTo: document.body,
    })

    const { confirm } = useConfirm()
    const pending = confirm({ title: 'Xóa thư mục?', description: 'Không thể hoàn tác.', destructive: true })
    await wrapper.vm.$nextTick()

    // Dialog của reka-ui render qua teleport vào body
    expect(document.body.textContent).toContain('Xóa thư mục?')
    expect(document.body.textContent).toContain('Không thể hoàn tác.')

    const buttons = Array.from(document.body.querySelectorAll('button'))
    const confirmButton = buttons.find((button) => button.textContent?.includes('Xác nhận'))
    expect(confirmButton).toBeTruthy()
    confirmButton!.click()

    await expect(pending).resolves.toBe(true)
    wrapper.unmount()
  })

  it('bấm hủy trả về false', async () => {
    const wrapper = mount(ConfirmDialog, {
      attachTo: document.body,
    })

    const { confirm } = useConfirm()
    const pending = confirm({ title: 'Lưu trữ tenant?' })
    await wrapper.vm.$nextTick()

    const buttons = Array.from(document.body.querySelectorAll('button'))
    const cancelButton = buttons.find((button) => button.textContent?.includes('Hủy'))
    expect(cancelButton).toBeTruthy()
    cancelButton!.click()

    await expect(pending).resolves.toBe(false)
    wrapper.unmount()
  })
})
