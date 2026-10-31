import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { DocumentDetail, DocumentListItem } from '@/api/documents'
import { TEST_TOKEN, createTestRouter, setupSession } from '@/test/session'
import DocumentReviewPage from './DocumentReviewPage.vue'

vi.mock('@/api/documents', () => ({
  getDocuments: vi.fn(),
  getDocument: vi.fn(),
  approveDocument: vi.fn(),
  rejectDocument: vi.fn(),
  downloadDocument: vi.fn(),
}))
const confirmMock = vi.fn()
vi.mock('@/composables/useConfirm', () => ({ useConfirm: () => ({ confirm: confirmMock }) }))

const documentsApi = await import('@/api/documents')

const queueItem: DocumentListItem = {
  id: 'd3',
  folderId: 'f1',
  folderPath: '/Biểu mẫu',
  title: 'Biểu mẫu đề xuất',
  status: 'PendingReview',
  latestVersionNumber: 1,
  latestVersionStatus: 'PendingReview',
  pendingVersionCount: 1,
  createdBy: 'Phạm Gia Bảo',
  createdAt: '2026-10-11T08:00:00Z',
  updatedAt: '2026-10-11T08:00:00Z',
}

function detailWith(status: DocumentDetail['versions'][number]['status']): DocumentDetail {
  return {
    id: 'd3',
    folderId: 'f1',
    folderPath: '/Biểu mẫu',
    title: 'Biểu mẫu đề xuất',
    status: 'PendingReview',
    versions: [
      {
        id: 'v1',
        versionNumber: 1,
        originalFileName: 'bieu-mau.docx',
        fileSizeBytes: 4096,
        status,
        hasNormalizedText: false,
        keyTopics: [],
        entities: [],
        processingWarnings: [],
        qualityWarnings: [],
        uploadedBy: 'Phạm Gia Bảo',
        createdAt: '2026-10-11T08:00:00Z',
      },
    ],
  }
}

async function mountAndSelect() {
  const pinia = setupSession('Reviewer')
  const router = createTestRouter()
  await router.push('/review')
  await router.isReady()
  const wrapper = mount(DocumentReviewPage, { global: { plugins: [pinia, router] }, attachTo: document.body })
  await flushPromises()
  await wrapper.get('tbody tr').trigger('click')
  await flushPromises()
  return wrapper
}

function findButton(wrapper: Awaited<ReturnType<typeof mountAndSelect>>, label: string) {
  return wrapper.findAll('button').find((button) => button.text().trim() === label)!
}

// Unmount sau mỗi test trước khi dọn DOM — tránh component cũ còn phản ứng với state module-level
enableAutoUnmount(afterEach)

describe('DocumentReviewPage (UX-04 B1)', () => {
  beforeEach(() => {
    vi.mocked(documentsApi.getDocuments).mockResolvedValue([queueItem])
    vi.mocked(documentsApi.getDocument).mockResolvedValue(detailWith('PendingReview'))
    vi.mocked(documentsApi.approveDocument).mockResolvedValue()
    vi.mocked(documentsApi.rejectDocument).mockResolvedValue()
  })

  afterEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
  })

  it('tải hàng đợi chờ duyệt bằng getDocuments(status=PendingReview) và hiện số phiên bản chờ', async () => {
    const wrapper = await mountAndSelect()
    expect(documentsApi.getDocuments).toHaveBeenCalledWith(TEST_TOKEN, { status: 'PendingReview' })
    expect(documentsApi.getDocument).toHaveBeenCalledWith('d3', TEST_TOKEN)
    const text = wrapper.text()
    expect(text).toContain('1 đang chờ')
    expect(text).toContain('1 chờ duyệt')
    expect(text).toContain('Phiên bản 1')
    expect(text).toContain('bieu-mau.docx')
  })

  it('phê duyệt cần xác nhận rồi gọi approveDocument(documentId, versionId, token)', async () => {
    confirmMock.mockResolvedValue(true)
    const wrapper = await mountAndSelect()

    await findButton(wrapper, 'Phê duyệt').trigger('click')
    await flushPromises()

    expect(confirmMock).toHaveBeenCalledWith(expect.objectContaining({ title: 'Xác nhận duyệt phiên bản', confirmText: 'Phê duyệt' }))
    expect(documentsApi.approveDocument).toHaveBeenCalledWith('d3', 'v1', TEST_TOKEN)
  })

  it('hủy xác nhận thì không gửi quyết định', async () => {
    confirmMock.mockResolvedValue(false)
    const wrapper = await mountAndSelect()
    await findButton(wrapper, 'Phê duyệt').trigger('click')
    await flushPromises()
    expect(documentsApi.approveDocument).not.toHaveBeenCalled()
  })

  it('không gửi phê duyệt nếu phiên bản đã được người khác xử lý (dữ liệu cũ)', async () => {
    confirmMock.mockResolvedValue(true)
    const wrapper = await mountAndSelect()
    vi.mocked(documentsApi.getDocument).mockResolvedValue(detailWith('Approved'))

    await findButton(wrapper, 'Phê duyệt').trigger('click')
    await flushPromises()

    expect(documentsApi.approveDocument).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Tài liệu này không còn phiên bản chờ duyệt')
  })

  it('từ chối bắt buộc nhập lý do, sau đó gọi rejectDocument với lý do', async () => {
    const wrapper = await mountAndSelect()
    await findButton(wrapper, 'Từ chối').trigger('click')
    await flushPromises()

    const form = document.body.querySelector('#reject-version-form') as HTMLFormElement
    expect(form).toBeTruthy()
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    await flushPromises()
    expect(document.body.textContent).toContain('Vui lòng nhập lý do từ chối.')
    expect(documentsApi.rejectDocument).not.toHaveBeenCalled()

    const textarea = document.body.querySelector('#reject-reason') as HTMLTextAreaElement
    textarea.value = '  Sai thư mục  '
    textarea.dispatchEvent(new Event('input'))
    // Textarea (useVModel passive) đẩy giá trị lên cha qua watcher — chờ flush trước khi submit
    await flushPromises()
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    await flushPromises()

    expect(documentsApi.rejectDocument).toHaveBeenCalledWith('d3', 'v1', 'Sai thư mục', TEST_TOKEN)
  })

  it('hàng đợi trống hiện trạng thái rỗng cuối cùng', async () => {
    vi.mocked(documentsApi.getDocuments).mockResolvedValue([])
    const pinia = setupSession('Reviewer')
    const router = createTestRouter()
    await router.push('/review')
    await router.isReady()
    const wrapper = mount(DocumentReviewPage, { global: { plugins: [pinia, router] } })
    await flushPromises()
    expect(wrapper.text()).toContain('Không có tài liệu chờ duyệt')
    expect(wrapper.text()).toContain('Hàng đợi trống — mọi phiên bản đã được xử lý.')
  })
})
