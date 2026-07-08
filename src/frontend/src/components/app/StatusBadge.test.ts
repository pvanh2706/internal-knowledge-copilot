import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import StatusBadge from './StatusBadge.vue'
import { statusLabel, statusVariant } from './status'

describe('StatusBadge', () => {
  it('map status nghiệp vụ sang variant + label tiếng Việt', () => {
    expect(statusVariant('Approved')).toBe('success')
    expect(statusVariant('PendingReview')).toBe('warning')
    expect(statusVariant('Rejected')).toBe('destructive')
    expect(statusVariant('Processing')).toBe('info')
    expect(statusVariant('Archived')).toBe('muted')
    // confidence từ API là chữ thường
    expect(statusVariant('high')).toBe('success')
    expect(statusLabel('high')).toBe('Độ tin cậy cao')
    expect(statusLabel('PendingReview')).toBe('Chờ duyệt')
  })

  it('status không xác định rơi về muted + in nguyên văn', () => {
    expect(statusVariant('TrangThaiLa')).toBe('muted')
    expect(statusLabel('TrangThaiLa')).toBe('TrangThaiLa')
  })

  it('render label và cho phép ghi đè', () => {
    const wrapper = mount(StatusBadge, { props: { status: 'Approved' } })
    expect(wrapper.text()).toBe('Đã duyệt')

    const overridden = mount(StatusBadge, { props: { status: 'Approved', label: 'OK' } })
    expect(overridden.text()).toBe('OK')
  })
})
