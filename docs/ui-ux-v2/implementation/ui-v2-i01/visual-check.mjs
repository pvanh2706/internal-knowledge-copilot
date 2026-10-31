// UI-V2-I01 visual verification harness (test-only, NOT production code).
// Runs the real Vue app (Vite dev server) in headless Chromium and answers the app's
// /api requests with clearly-labelled TEST FIXTURES via Playwright route interception,
// so the three converted screens can be screenshotted without a backend.
// Also screenshots the approved B1 prototype at the same viewports. Use the multi-file
// prototype (index.html + styles.css + prototype.js): index-standalone.html on PR #11 has a
// bundling defect ("Identifier '$' has already been declared") and does not navigate.
//
// Not wired into npm scripts and playwright-core is deliberately NOT a project dependency.
// Run from a scratch folder that has playwright-core installed:
//   npx vite --port 5179 --strictPort            (in src/frontend)
//   CHROME_PATH=<chromium/edge exe> [SHOT_FORMAT=jpeg] \
//     node visual-check.mjs <outDir> http://localhost:5179 <path>/prototypes/b1/index.html [--only=a,b]
import { chromium } from 'playwright-core'
import { mkdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { pathToFileURL } from 'node:url'

const [, , outDir, appBase, ...rest] = process.argv
const prototypeFile = rest.find((a) => !a.startsWith("--"))
const only = (rest.find((a) => a.startsWith('--only=')) ?? '').slice(7).split(',').filter(Boolean)
const executablePath = process.env.CHROME_PATH
mkdirSync(outDir, { recursive: true })

// ---------- TEST FIXTURES (visual harness only) ----------
const iso = (d) => new Date(d).toISOString()
const folders = [
  { id: 'f-qt', name: 'Quy trình', path: '/Quy trình', children: [
    { id: 'f-qt-ns', parentId: 'f-qt', name: 'Nhân sự', path: '/Quy trình/Nhân sự', children: [] },
  ] },
  { id: 'f-hd', name: 'Hướng dẫn', path: '/Hướng dẫn', children: [] },
  { id: 'f-bm', name: 'Biểu mẫu', path: '/Biểu mẫu', children: [] },
]
const docs = [
  { id: 'd1', folderId: 'f-qt', folderPath: '/Quy trình', title: 'Quy trình duyệt tài liệu nội bộ', description: 'Các bước kiểm soát tài liệu trước khi lập chỉ mục.', status: 'Approved', currentVersionId: 'd1v3', currentVersionNumber: 3, latestVersionNumber: 3, latestVersionStatus: 'Indexed', pendingVersionCount: 0, createdBy: 'Trần Thu Hà', createdAt: iso('2026-09-01T08:00:00Z'), updatedAt: iso('2026-10-15T09:30:00Z') },
  { id: 'd2', folderId: 'f-hd', folderPath: '/Hướng dẫn', title: 'Hướng dẫn sử dụng kho tri thức', description: 'Tra cứu tài liệu và hỏi AI.', status: 'Approved', currentVersionId: 'd2v2', currentVersionNumber: 2, latestVersionNumber: 2, latestVersionStatus: 'Indexed', pendingVersionCount: 0, createdBy: 'Lê Minh Quân', createdAt: iso('2026-09-03T08:00:00Z'), updatedAt: iso('2026-10-12T10:00:00Z') },
  { id: 'd3', folderId: 'f-bm', folderPath: '/Biểu mẫu', title: 'Biểu mẫu đề xuất thay đổi', description: 'Mẫu đề xuất cập nhật quy trình.', status: 'PendingReview', latestVersionNumber: 1, latestVersionStatus: 'PendingReview', pendingVersionCount: 1, createdBy: 'Phạm Gia Bảo', createdAt: iso('2026-10-11T08:00:00Z'), updatedAt: iso('2026-10-11T08:00:00Z') },
  { id: 'd4', folderId: 'f-qt-ns', folderPath: '/Quy trình/Nhân sự', title: 'Quy trình nghỉ phép năm 2026', description: 'Phiên bản cập nhật chính sách nghỉ phép.', status: 'Approved', currentVersionId: 'd4v1', currentVersionNumber: 1, latestVersionNumber: 2, latestVersionStatus: 'PendingReview', pendingVersionCount: 1, createdBy: 'Đỗ Thị Mai', createdAt: iso('2026-08-20T08:00:00Z'), updatedAt: iso('2026-10-08T14:00:00Z') },
  { id: 'd5', folderId: 'f-hd', folderPath: '/Hướng dẫn', title: 'Hướng dẫn phân quyền tài liệu', description: 'Cấu hình quyền xem theo team.', status: 'Rejected', latestVersionNumber: 1, latestVersionStatus: 'Rejected', pendingVersionCount: 0, createdBy: 'Lê Minh Quân', createdAt: iso('2026-10-01T08:00:00Z'), updatedAt: iso('2026-10-05T08:00:00Z') },
  { id: 'd6', folderId: 'f-bm', folderPath: '/Biểu mẫu', title: 'Danh sách biểu mẫu phòng ban', status: 'Approved', currentVersionId: 'd6v2', currentVersionNumber: 2, latestVersionNumber: 2, latestVersionStatus: 'Processing', pendingVersionCount: 0, createdBy: 'Trần Thu Hà', createdAt: iso('2026-09-10T08:00:00Z'), updatedAt: iso('2026-10-02T08:00:00Z') },
]
const version = (id, n, status, extra = {}) => ({
  id, versionNumber: n, originalFileName: `tai-lieu-v${n}.pdf`, fileSizeBytes: 245760 + n * 1024, contentType: 'application/pdf', status,
  hasNormalizedText: status === 'Indexed', sectionCount: status === 'Indexed' ? 12 : null, documentSummary: status === 'Indexed' ? 'Mô tả các bước tải lên, duyệt, xử lý và lập chỉ mục tài liệu.' : null,
  language: 'vi', documentType: 'Quy trình', keyTopics: status === 'Indexed' ? ['duyệt tài liệu', 'lập chỉ mục'] : [], entities: [], effectiveDate: null, sensitivity: 'Internal',
  processingWarnings: [], qualityWarnings: [], uploadedBy: 'Trần Thu Hà', reviewedBy: status === 'PendingReview' ? null : 'Nguyễn Văn An', reviewedAt: null, createdAt: iso(`2026-10-0${n}T08:00:00Z`), ...extra,
})
const detail = {
  d1: { versions: [version('d1v3', 3, 'Indexed'), version('d1v2', 2, 'Approved'), version('d1v1', 1, 'Rejected', { rejectReason: 'Thiếu phụ lục biểu mẫu.' })] },
  d2: { versions: [version('d2v2', 2, 'Indexed'), version('d2v1', 1, 'Approved')] },
  d3: { versions: [version('d3v1', 1, 'PendingReview', { originalFileName: 'bieu-mau-de-xuat.docx', contentType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', uploadedBy: 'Phạm Gia Bảo' })] },
  d4: { versions: [version('d4v2', 2, 'PendingReview', { uploadedBy: 'Đỗ Thị Mai' }), version('d4v1', 1, 'Indexed')] },
  d5: { versions: [version('d5v1', 1, 'Rejected', { rejectReason: 'Sai thư mục lưu trữ.' })] },
  d6: { versions: [version('d6v2', 2, 'Processing'), version('d6v1', 1, 'Approved')] },
}
const docDetail = (id) => {
  const d = docs.find((x) => x.id === id)
  return { id: d.id, folderId: d.folderId, folderPath: d.folderPath, title: d.title, description: d.description, status: d.status, currentVersionId: d.currentVersionId, versions: detail[id].versions }
}
const askResponse = {
  interactionId: 'ix-1',
  answer: 'Quy trình duyệt tài liệu nội bộ gồm các bước chính:\n\n1. Người dùng tải tài liệu lên đúng thư mục; tài liệu ở trạng thái chờ duyệt.\n2. Reviewer kiểm tra phiên bản và phê duyệt hoặc từ chối kèm lý do.\n3. Sau khi phê duyệt, phiên bản được xử lý và lập chỉ mục để phục vụ tra cứu.',
  needsClarification: false,
  confidence: 'high',
  missingInformation: ['Thời hạn tối đa cho mỗi bước duyệt chưa được nêu trong tài liệu.'],
  conflicts: [],
  suggestedFollowUps: ['Ai có quyền phê duyệt tài liệu?', 'Tài liệu bị từ chối thì xử lý thế nào?'],
  citations: [
    { sourceType: 'Document', title: 'Quy trình duyệt tài liệu nội bộ', folderPath: '/Quy trình', sectionTitle: 'Mục 2 — Các bước duyệt', excerpt: 'Sau khi được reviewer phê duyệt, phiên bản tài liệu sẽ được xử lý và lập chỉ mục để phục vụ tra cứu.' },
    { sourceType: 'Wiki', title: 'Wiki quản lý tri thức', folderPath: '/Hướng dẫn', sectionTitle: null, excerpt: 'Nguồn đã lập chỉ mục có thể được tra cứu theo phạm vi thư mục hoặc tài liệu.' },
  ],
}
const dashboard = { documentCounts: [{ name: 'Approved', count: 4 }, { name: 'PendingReview', count: 2 }], wikiCounts: [], aiQuestionCount: 12, feedbackCorrectCount: 8, feedbackIncorrectCount: 1, incorrectFeedbackPendingCount: 1, evaluationCaseCount: 0, topCitedSources: [] }
// ---------------------------------------------------------

function json(route, status, body) {
  return route.fulfill({ status, contentType: 'application/json', body: body === undefined ? '' : JSON.stringify(body) })
}

async function mockApi(page, overrides = {}) {
  const requests = []
  await page.route('http://localhost:5000/api/**', async (route) => {
    const req = route.request()
    const url = new URL(req.url())
    const path = url.pathname.replace(/^.*\/api/, '')
    requests.push(`${req.method()} ${path}${url.search}`)
    for (const [pattern, handler] of Object.entries(overrides)) {
      if (new RegExp(pattern).test(`${req.method()} ${path}`)) return handler(route, req)
    }
    if (path === '/folders/tree') return json(route, 200, folders)
    if (path === '/documents' && req.method() === 'GET') {
      const status = url.searchParams.get('status')
      const keyword = (url.searchParams.get('keyword') ?? '').toLowerCase()
      const folderId = url.searchParams.get('folderId')
      let list = docs
      if (status === 'PendingReview') list = list.filter((d) => d.status === 'PendingReview' || d.pendingVersionCount > 0)
      else if (status) list = list.filter((d) => d.status === status)
      if (keyword) list = list.filter((d) => d.title.toLowerCase().includes(keyword))
      if (folderId) list = list.filter((d) => d.folderId === folderId)
      return json(route, 200, list)
    }
    const m = path.match(/^\/documents\/([^/]+)$/)
    if (m && req.method() === 'GET') return json(route, 200, docDetail(m[1]))
    if (path === '/ai/ask') { await new Promise((r) => setTimeout(r, 300)); return json(route, 200, askResponse) }
    if (path === '/dashboard/summary') return json(route, 200, dashboard)
    if (path.startsWith('/ai-settings')) return json(route, 200, { hasLlmApiKey: true, usesMockLlmProvider: false, providerName: 'fixture' })
    return json(route, 200, [])
  })
  return requests
}

async function newPage(browser, { width, height, theme = 'light', role = 'Reviewer' }) {
  const context = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: 1, locale: 'vi-VN', timezoneId: 'Asia/Ho_Chi_Minh' })
  await context.addInitScript(({ theme, role }) => {
    localStorage.setItem('ikc.accessToken', 'visual-fixture-token')
    localStorage.setItem('ikc.user', JSON.stringify({ id: 'u1', displayName: 'Nguyễn Văn An', role }))
    localStorage.setItem('ikc.tenantCode', 'acme')
    localStorage.setItem('theme', theme)
  }, { theme, role })
  return { context, page: await context.newPage() }
}

const VIEWPORTS = { desktop: { width: 1440, height: 900 }, mobile: { width: 390, height: 844 } }
const log = []

async function shot(page, name, opts = {}) {
  await page.waitForTimeout(250)
  if (opts.fullPage) {
    // App shell cuộn trong <main> (h-dvh) — chỉ trong harness: mở khóa chiều cao để chụp toàn trang
    await page.evaluate(() => {
      const shell = document.querySelector('.h-dvh')
      shell?.classList.remove('h-dvh', 'overflow-hidden')
      shell?.querySelector('main')?.classList.remove('overflow-y-auto')
    })
  }
  const jpeg = process.env.SHOT_FORMAT === 'jpeg'
  const file = join(outDir, `${name}.${jpeg ? 'jpg' : 'png'}`)
  await page.screenshot({ path: file, fullPage: opts.fullPage ?? false, ...(jpeg ? { type: 'jpeg', quality: 78 } : {}) })
  log.push(name)
}

const scenarios = {
  async 'ai-empty'(browser, vp, theme) {
    const { context, page } = await newPage(browser, { ...VIEWPORTS[vp], theme })
    await mockApi(page); await page.goto(`${appBase}/ai`); await page.waitForLoadState('networkidle')
    await shot(page, `vue-ai-empty-${vp}-${theme}`); await context.close()
  },
  async 'ai-answer'(browser, vp, theme) {
    const { context, page } = await newPage(browser, { ...VIEWPORTS[vp], theme })
    const reqs = await mockApi(page); await page.goto(`${appBase}/ai`); await page.waitForLoadState('networkidle')
    await page.locator('textarea').first().fill('Quy trình duyệt tài liệu nội bộ gồm những bước nào?')
    await page.locator('textarea').first().press('Enter')
    await page.getByText('Quy trình duyệt tài liệu nội bộ gồm các bước chính').first().waitFor()
    await shot(page, `vue-ai-answer-${vp}-${theme}`, { fullPage: vp === 'mobile' })
    writeFileSync(join(outDir, `requests-ai-${vp}-${theme}.txt`), reqs.join('\n'))
    await context.close()
  },
  async 'ai-denied'(browser, vp, theme) {
    const { context, page } = await newPage(browser, { ...VIEWPORTS[vp], theme })
    await mockApi(page, { 'POST /ai/ask': (route) => json(route, 403, { message: 'Bạn không có quyền truy cập phạm vi này.' }) })
    await page.goto(`${appBase}/ai`); await page.waitForLoadState('networkidle')
    await page.locator('textarea').first().fill('Câu hỏi thử quyền')
    await page.locator('textarea').first().press('Enter')
    await page.waitForTimeout(500)
    await shot(page, `vue-ai-denied-${vp}-${theme}`); await context.close()
  },
  async 'documents'(browser, vp, theme) {
    const { context, page } = await newPage(browser, { ...VIEWPORTS[vp], theme })
    const reqs = await mockApi(page); await page.goto(`${appBase}/documents`); await page.waitForLoadState('networkidle')
    await page.locator('tbody tr').first().click()
    await page.getByText('tai-lieu-v3.pdf').first().waitFor()
    await shot(page, `vue-documents-${vp}-${theme}`, { fullPage: vp === 'mobile' })
    writeFileSync(join(outDir, `requests-documents-${vp}-${theme}.txt`), reqs.join('\n'))
    await context.close()
  },
  async 'documents-upload'(browser, vp, theme) {
    const { context, page } = await newPage(browser, { ...VIEWPORTS[vp], theme })
    await mockApi(page); await page.goto(`${appBase}/documents`); await page.waitForLoadState('networkidle')
    await page.getByRole('button', { name: /Tải tài liệu/ }).first().click()
    await page.waitForTimeout(400)
    await shot(page, `vue-documents-upload-${vp}-${theme}`); await context.close()
  },
  async 'documents-empty'(browser, vp, theme) {
    const { context, page } = await newPage(browser, { ...VIEWPORTS[vp], theme })
    await mockApi(page, { 'GET /documents$': (route) => json(route, 200, []) }); await page.goto(`${appBase}/documents`); await page.waitForLoadState('networkidle')
    await shot(page, `vue-documents-empty-${vp}-${theme}`); await context.close()
  },
  async 'documents-error'(browser, vp, theme) {
    const { context, page } = await newPage(browser, { ...VIEWPORTS[vp], theme })
    await mockApi(page, { 'GET /documents$': (route) => json(route, 403) }); await page.goto(`${appBase}/documents`); await page.waitForLoadState('networkidle')
    await shot(page, `vue-documents-error-${vp}-${theme}`); await context.close()
  },
  async 'review'(browser, vp, theme) {
    const { context, page } = await newPage(browser, { ...VIEWPORTS[vp], theme })
    const reqs = await mockApi(page); await page.goto(`${appBase}/review`); await page.waitForLoadState('networkidle')
    await page.locator('tbody tr').first().click()
    await page.waitForTimeout(500)
    await shot(page, `vue-review-${vp}-${theme}`, { fullPage: vp === 'mobile' })
    writeFileSync(join(outDir, `requests-review-${vp}-${theme}.txt`), reqs.join('\n'))
    await context.close()
  },
  async 'review-reject'(browser, vp, theme) {
    const { context, page } = await newPage(browser, { ...VIEWPORTS[vp], theme })
    await mockApi(page); await page.goto(`${appBase}/review`); await page.waitForLoadState('networkidle')
    await page.locator('tbody tr').first().click(); await page.waitForTimeout(400)
    await page.getByRole('button', { name: /Từ chối/ }).first().click(); await page.waitForTimeout(400)
    await shot(page, `vue-review-reject-${vp}-${theme}`); await context.close()
  },
  async 'review-empty'(browser, vp, theme) {
    const { context, page } = await newPage(browser, { ...VIEWPORTS[vp], theme })
    await mockApi(page, { 'GET /documents$': (route) => json(route, 200, []) }); await page.goto(`${appBase}/review`); await page.waitForLoadState('networkidle')
    await shot(page, `vue-review-empty-${vp}-${theme}`); await context.close()
  },
  async 'other-screens'(browser, vp, theme) {
    for (const path of ['/', '/wiki', '/admin/folders', '/retrieval-explain']) {
      const { context, page } = await newPage(browser, { ...VIEWPORTS[vp], theme, role: 'Admin' })
      await mockApi(page); await page.goto(`${appBase}${path}`); await page.waitForLoadState('networkidle')
      await shot(page, `vue-other${path.replace(/\//g, '_') || '_root'}-${vp}-${theme}`); await context.close()
    }
  },
  async 'mobile-nav'(browser, vp, theme) {
    const { context, page } = await newPage(browser, { ...VIEWPORTS[vp], theme })
    await mockApi(page); await page.goto(`${appBase}/documents`); await page.waitForLoadState('networkidle')
    const toggle = page.getByRole('button', { name: /menu/i }).first()
    if (await toggle.count()) { await toggle.click(); await page.waitForTimeout(500) }
    await shot(page, `vue-mobile-nav-${vp}-${theme}`); await context.close()
  },
}

async function prototypeShots(browser) {
  if (!prototypeFile) return
  for (const vp of Object.keys(VIEWPORTS)) {
    for (const screen of ['ai', 'documents', 'review']) {
      const context = await browser.newContext({ viewport: VIEWPORTS[vp], deviceScaleFactor: 1 })
      const page = await context.newPage()
      await page.goto(pathToFileURL(prototypeFile).href)
      await page.locator(`[data-route="${screen}"]`).first().evaluate((el) => el.click())
      await page.waitForTimeout(300)
      await shot(page, `proto-${screen}-${vp}`, { fullPage: vp === 'mobile' })
      await context.close()
    }
  }
}

const browser = await chromium.launch({ executablePath, headless: true })
try {
  if (!only.length || only.includes('proto')) await prototypeShots(browser)
  for (const [name, run] of Object.entries(scenarios)) {
    if (only.length && !only.includes(name)) continue
    const vps = name === 'other-screens' ? ['desktop'] : name === 'mobile-nav' ? ['mobile'] : ['desktop', 'mobile']
    for (const vp of vps) {
      try { await run(browser, vp, 'light') } catch (error) { log.push(`FAILED ${name} ${vp}: ${error.message.split('\n')[0]}`) }
    }
    if (['ai-answer', 'documents', 'review'].includes(name)) {
      try { await run(browser, 'desktop', 'dark') } catch (error) { log.push(`FAILED ${name} dark: ${error.message.split('\n')[0]}`) }
    }
  }
} finally {
  await browser.close()
}
writeFileSync(join(outDir, 'shots.log'), log.join('\n'))
console.log(log.join('\n'))
