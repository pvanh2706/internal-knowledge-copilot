import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { AskQuestionResponse } from '@/api/ai'
import { ApiError } from '@/api/http'
import { TEST_TOKEN, createTestRouter, setupSession } from '@/test/session'
import AiQuestionPage from './AiQuestionPage.vue'

// Chỉ mock lớp API client — kiểm tra trang gọi đúng contract hiện có (tên hàm, payload, token)
vi.mock('@/api/ai', () => ({ askQuestion: vi.fn() }))
vi.mock('@/api/documents', () => ({ getDocuments: vi.fn() }))
vi.mock('@/api/folders', () => ({ getFolderTree: vi.fn() }))
vi.mock('@/api/feedback', () => ({ submitAiFeedback: vi.fn() }))

const { askQuestion } = await import('@/api/ai')
const { getDocuments } = await import('@/api/documents')
const { getFolderTree } = await import('@/api/folders')
const { submitAiFeedback } = await import('@/api/feedback')

const answer: AskQuestionResponse = {
  interactionId: 'ix-1',
  answer: 'Câu trả lời từ máy chủ.',
  needsClarification: true,
  confidence: 'medium',
  missingInformation: ['Thiếu thời hạn duyệt.'],
  conflicts: ['Hai nguồn nêu khác nhau.'],
  suggestedFollowUps: ['Ai phê duyệt?'],
  citations: [
    { sourceType: 'Document', title: 'Quy trình A', folderPath: '/Quy trình', sectionTitle: 'Mục 1', excerpt: 'Trích đoạn A' },
  ],
}

async function mountPage() {
  const pinia = setupSession('User')
  const router = createTestRouter()
  await router.push('/ai')
  await router.isReady()
  const wrapper = mount(AiQuestionPage, { global: { plugins: [pinia, router] }, attachTo: document.body })
  await flushPromises()
  return wrapper
}

async function ask(wrapper: Awaited<ReturnType<typeof mountPage>>, question: string) {
  const textarea = wrapper.get('textarea#ai-question')
  await textarea.setValue(question)
  await textarea.trigger('keydown', { key: 'Enter' })
  await flushPromises()
}

// Unmount sau mỗi test trước khi dọn DOM — tránh component cũ còn phản ứng với state module-level
enableAutoUnmount(afterEach)

describe('AiQuestionPage (UX-01 B1)', () => {
  beforeEach(() => {
    vi.mocked(getDocuments).mockResolvedValue([])
    vi.mocked(getFolderTree).mockResolvedValue([])
  })

  afterEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
  })

  it('tải dữ liệu phạm vi như trước và hiện trạng thái chưa có câu hỏi', async () => {
    const wrapper = await mountPage()
    expect(getDocuments).toHaveBeenCalledWith(TEST_TOKEN, { status: 'Approved' })
    expect(getFolderTree).toHaveBeenCalledWith(TEST_TOKEN)
    expect(wrapper.text()).toContain('Chưa có câu hỏi')
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()
  })

  it('Enter gửi đúng payload /ai/ask và hiển thị câu trả lời, độ tin cậy, nguồn, gợi ý', async () => {
    vi.mocked(askQuestion).mockResolvedValue(answer)
    const wrapper = await mountPage()

    await ask(wrapper, 'Quy trình duyệt?')

    expect(askQuestion).toHaveBeenCalledWith(
      { question: 'Quy trình duyệt?', scopeType: 'All', folderId: null, documentId: null },
      TEST_TOKEN,
    )
    const text = wrapper.text()
    expect(text).toContain('Quy trình duyệt?')
    expect(text).toContain('Câu trả lời từ máy chủ.')
    expect(text).toContain('Độ tin cậy trung bình')
    expect(text).toContain('Copilot cần thêm ngữ cảnh.')
    expect(text).toContain('Thiếu thời hạn duyệt.')
    expect(text).toContain('Hai nguồn nêu khác nhau.')
    expect(text).toContain('Ai phê duyệt?')
    expect(text).toContain('Quy trình A')
    expect(text).toContain('Nguồn tham khảo (1)')
    // Thẻ nguồn không phải link/nút — chưa có contract điều hướng tới nguồn
    expect(wrapper.find('aside article a, aside article button').exists()).toBe(false)
    // Composer được làm trống sau khi có câu trả lời
    expect((wrapper.get('textarea#ai-question').element as HTMLTextAreaElement).value).toBe('')
  })

  it('Shift+Enter và Enter khi đang gõ bộ gõ tiếng Việt không gửi câu hỏi', async () => {
    const wrapper = await mountPage()
    const textarea = wrapper.get('textarea#ai-question')
    await textarea.setValue('Câu hỏi')
    await textarea.trigger('keydown', { key: 'Enter', shiftKey: true })
    await textarea.trigger('keydown', { key: 'Enter', isComposing: true })
    expect(askQuestion).not.toHaveBeenCalled()
  })

  it('403 hiện trạng thái không có quyền, không có nút thử lại', async () => {
    vi.mocked(askQuestion).mockRejectedValue(new ApiError('Bạn không có quyền truy cập phạm vi này.', 403))
    const wrapper = await mountPage()

    await ask(wrapper, 'Câu hỏi')

    expect(wrapper.text()).toContain('Không thể truy cập nguồn này')
    expect(wrapper.text()).toContain('Bạn không có quyền truy cập phạm vi này.')
    expect(wrapper.findAll('button').some((button) => button.text() === 'Thử lại')).toBe(false)
  })

  it('lỗi máy chủ cho phép thử lại với đúng payload cũ', async () => {
    vi.mocked(askQuestion).mockRejectedValueOnce(new ApiError('Lỗi máy chủ.', 500)).mockResolvedValueOnce(answer)
    const wrapper = await mountPage()

    await ask(wrapper, 'Câu hỏi')
    expect(wrapper.text()).toContain('Không thể tải câu trả lời')
    // Câu hỏi vẫn giữ trong composer khi lỗi
    expect((wrapper.get('textarea#ai-question').element as HTMLTextAreaElement).value).toBe('Câu hỏi')

    const retry = wrapper.findAll('button').find((button) => button.text() === 'Thử lại')
    await retry!.trigger('click')
    await flushPromises()

    expect(askQuestion).toHaveBeenCalledTimes(2)
    expect(vi.mocked(askQuestion).mock.calls[1]).toEqual(vi.mocked(askQuestion).mock.calls[0])
    expect(wrapper.text()).toContain('Câu trả lời từ máy chủ.')
  })

  it('lỗi mạng hiển thị thông báo mất kết nối tiếng Việt', async () => {
    vi.mocked(askQuestion).mockRejectedValue(new TypeError('Failed to fetch'))
    const wrapper = await mountPage()
    await ask(wrapper, 'Câu hỏi')
    expect(wrapper.text()).toContain('Không thể kết nối tới máy chủ')
  })

  it('phản hồi đúng gửi submitAiFeedback với interactionId của câu trả lời', async () => {
    vi.mocked(askQuestion).mockResolvedValue(answer)
    vi.mocked(submitAiFeedback).mockResolvedValue({} as never)
    const wrapper = await mountPage()
    await ask(wrapper, 'Câu hỏi')

    await wrapper.get('button[aria-label="Câu trả lời đúng"]').trigger('click')
    await flushPromises()

    expect(submitAiFeedback).toHaveBeenCalledWith('ix-1', { value: 'Correct', note: '' }, TEST_TOKEN)
  })

  it('phản hồi sai gửi kèm ghi chú', async () => {
    vi.mocked(askQuestion).mockResolvedValue(answer)
    vi.mocked(submitAiFeedback).mockResolvedValue({} as never)
    const wrapper = await mountPage()
    await ask(wrapper, 'Câu hỏi')

    await wrapper.get('button[aria-label="Câu trả lời sai"]').trigger('click')
    await wrapper.get('textarea#ai-feedback-note').setValue('Sai mục 2')
    const sendIncorrect = wrapper.findAll('button').find((button) => button.text() === 'Gửi phản hồi sai')
    await sendIncorrect!.trigger('click')
    await flushPromises()

    expect(submitAiFeedback).toHaveBeenCalledWith('ix-1', { value: 'Incorrect', note: 'Sai mục 2' }, TEST_TOKEN)
  })

  it('câu hỏi gợi ý gửi lại /ai/ask với câu hỏi đó', async () => {
    vi.mocked(askQuestion).mockResolvedValue(answer)
    const wrapper = await mountPage()
    await ask(wrapper, 'Câu hỏi')

    const followUp = wrapper.findAll('button').find((button) => button.text() === 'Ai phê duyệt?')
    await followUp!.trigger('click')
    await flushPromises()

    expect(vi.mocked(askQuestion).mock.calls[1][0]).toMatchObject({ question: 'Ai phê duyệt?', scopeType: 'All' })
  })
})
