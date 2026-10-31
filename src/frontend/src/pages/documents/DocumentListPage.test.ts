import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { UserRole } from '@/api/auth'
import type { DocumentDetail, DocumentListItem } from '@/api/documents'
import { ApiError } from '@/api/http'
import { TEST_TOKEN, createTestRouter, setupSession } from '@/test/session'
import DocumentListPage from './DocumentListPage.vue'

vi.mock('@/api/documents', () => ({
  getDocuments: vi.fn(),
  getDocument: vi.fn(),
  uploadDocument: vi.fn(),
  uploadDocumentVersion: vi.fn(),
  downloadDocument: vi.fn(),
}))
vi.mock('@/api/folders', () => ({ getFolderTree: vi.fn() }))
vi.mock('@/api/wiki', () => ({ generateWikiDraft: vi.fn() }))

const documentsApi = await import('@/api/documents')
const { getFolderTree } = await import('@/api/folders')
const { generateWikiDraft } = await import('@/api/wiki')

const listItem: DocumentListItem = {
  id: 'd1',
  folderId: 'f1',
  folderPath: '/Quy trình',
  title: 'Quy trình duyệt',
  description: 'Mô tả',
  status: 'Approved',
  currentVersionId: 'v2',
  currentVersionNumber: 2,
  latestVersionNumber: 2,
  latestVersionStatus: 'Indexed',
  pendingVersionCount: 0,
  createdBy: 'Trần Thu Hà',
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-15T08:00:00Z',
}

const version = (id: string, versionNumber: number, status: DocumentDetail['versions'][number]['status']) => ({
  id,
  versionNumber,
  originalFileName: `file-v${versionNumber}.pdf`,
  fileSizeBytes: 2048,
  status,
  hasNormalizedText: status === 'Indexed',
  sectionCount: 4,
  keyTopics: [],
  entities: [],
  processingWarnings: [],
  qualityWarnings: [],
  uploadedBy: 'Trần Thu Hà',
  createdAt: '2026-10-01T08:00:00Z',
  rejectReason: status === 'Rejected' ? 'Sai thư mục' : undefined,
})

const detail: DocumentDetail = {
  id: 'd1',
  folderId: 'f1',
  folderPath: '/Quy trình',
  title: 'Quy trình duyệt',
  description: 'Mô tả',
  status: 'Approved',
  currentVersionId: 'v2',
  versions: [version('v2', 2, 'Indexed'), version('v1', 1, 'Rejected')],
}

async function mountPage(role: UserRole = 'User') {
  const pinia = setupSession(role)
  const router = createTestRouter()
  await router.push('/documents')
  await router.isReady()
  const wrapper = mount(DocumentListPage, { global: { plugins: [pinia, router] }, attachTo: document.body })
  await flushPromises()
  return wrapper
}

// Unmount sau mỗi test trước khi dọn DOM — tránh component cũ còn phản ứng với state module-level
enableAutoUnmount(afterEach)

describe('DocumentListPage (UX-02 B1)', () => {
  beforeEach(() => {
    vi.mocked(documentsApi.getDocuments).mockResolvedValue([listItem])
    vi.mocked(documentsApi.getDocument).mockResolvedValue(detail)
    vi.mocked(getFolderTree).mockResolvedValue([{ id: 'f1', name: 'Quy trình', path: '/Quy trình', children: [] }])
  })

  afterEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
  })

  it('tải danh sách bằng getDocuments với bộ lọc rỗng và hiển thị dòng dữ liệu thật', async () => {
    const wrapper = await mountPage()
    expect(documentsApi.getDocuments).toHaveBeenCalledWith(TEST_TOKEN, { folderId: undefined, status: undefined, keyword: undefined })
    expect(getFolderTree).toHaveBeenCalledWith(TEST_TOKEN)
    expect(wrapper.text()).toContain('Quy trình duyệt')
    expect(wrapper.text()).toContain('Phiên bản 2/2 · Đã lập chỉ mục')
    expect(wrapper.text()).toContain('1 tài liệu')
  })

  it('chọn dòng (bàn phím) tải chi tiết, đánh dấu dòng đang chọn, hiện phiên bản + lý do từ chối', async () => {
    const wrapper = await mountPage()
    const row = wrapper.get('tbody tr')
    await row.trigger('keydown', { key: ' ' })
    await flushPromises()

    expect(documentsApi.getDocument).toHaveBeenCalledWith('d1', TEST_TOKEN)
    expect(wrapper.get('tbody tr').attributes('aria-current')).toBe('true')
    const text = wrapper.text()
    expect(text).toContain('Lịch sử phiên bản')
    expect(text).toContain('file-v2.pdf')
    expect(text).toContain('Lý do từ chối: Sai thư mục')
  })

  it('tải từng phiên bản qua downloadDocument với versionId', async () => {
    vi.mocked(documentsApi.downloadDocument).mockResolvedValue()
    const wrapper = await mountPage()
    await wrapper.get('tbody tr').trigger('click')
    await flushPromises()

    await wrapper.get('button[aria-label="Tải file file-v1.pdf (phiên bản 1)"]').trigger('click')
    await flushPromises()
    expect(documentsApi.downloadDocument).toHaveBeenCalledWith('d1', TEST_TOKEN, 'v1')
  })

  it('nút tạo wiki draft chỉ hiện với reviewer/admin và gọi generateWikiDraft với phiên bản hiện tại', async () => {
    const userView = await mountPage('User')
    await userView.get('tbody tr').trigger('click')
    await flushPromises()
    expect(userView.text()).not.toContain('Tạo wiki draft')
    userView.unmount()
    localStorage.clear()

    vi.mocked(generateWikiDraft).mockResolvedValue({} as never)
    const reviewerView = await mountPage('Reviewer')
    await reviewerView.get('tbody tr').trigger('click')
    await flushPromises()
    const button = reviewerView.findAll('button').find((item) => item.text().includes('Tạo wiki draft'))
    await button!.trigger('click')
    await flushPromises()
    expect(generateWikiDraft).toHaveBeenCalledWith({ documentId: 'd1', documentVersionId: 'v2' }, TEST_TOKEN)
  })

  it('dialog tải lên kiểm tra trường bắt buộc trước khi gọi uploadDocument', async () => {
    const wrapper = await mountPage()
    const openButton = wrapper.findAll('button').find((button) => button.text().includes('Tải tài liệu'))
    await openButton!.trigger('click')
    await flushPromises()

    const form = document.body.querySelector('#upload-document-form') as HTMLFormElement
    expect(form).toBeTruthy()
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    await flushPromises()

    expect(document.body.textContent).toContain('Vui lòng nhập tên tài liệu.')
    expect(document.body.textContent).toContain('Vui lòng chọn thư mục.')
    expect(document.body.textContent).toContain('Vui lòng chọn file cần tải lên.')
    expect(documentsApi.uploadDocument).not.toHaveBeenCalled()
  })

  it('lỗi tải danh sách hiện thông báo và nút thử lại gọi lại API', async () => {
    vi.mocked(documentsApi.getDocuments).mockRejectedValueOnce(new ApiError('Không thể xử lý yêu cầu.', 403))
    const wrapper = await mountPage()
    expect(wrapper.text()).toContain('Bạn không có quyền thực hiện thao tác này.')
    expect(wrapper.text()).toContain('Chưa tải được danh sách tài liệu')

    const retry = wrapper.findAll('button').find((button) => button.text() === 'Thử lại')
    await retry!.trigger('click')
    await flushPromises()
    expect(documentsApi.getDocuments).toHaveBeenCalledTimes(2)
    expect(wrapper.text()).toContain('Quy trình duyệt')
  })

  it('đổi từ khóa lọc lại phía máy chủ với keyword (debounce)', async () => {
    vi.useFakeTimers()
    try {
      const wrapper = await mountPage()
      await wrapper.get('input#doc-filter-keyword').setValue('duyệt')
      await vi.advanceTimersByTimeAsync(400)
      await flushPromises()
      expect(documentsApi.getDocuments).toHaveBeenLastCalledWith(TEST_TOKEN, { folderId: undefined, status: undefined, keyword: 'duyệt' })
      expect(wrapper.text()).toContain('Quy trình duyệt')
    } finally {
      vi.useRealTimers()
    }
  })
})
