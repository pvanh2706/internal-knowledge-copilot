/* B1 prototype: local-only interactions, no API/network, no persistent storage. */
(() => {
  'use strict'
  const $ = (id) => document.getElementById(id)
  const $$ = (selector) => [...document.querySelectorAll(selector)]
  const icon = (name) => '<svg class="icon sm" aria-hidden="true"><use href="#i-' + name + '"/></svg>'
  const escapeHTML = (value) => String(value).replace(/[&<>"']/g, (c) => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]))
  let toastTimer = null
  let lastFocus = null
  let modalAction = null
  function toast(msg) {
    const node = $('toast'); node.textContent = msg; node.hidden = false
    clearTimeout(toastTimer)
    toastTimer = setTimeout(() => { node.hidden = true }, 4000)
  }
  const meta = {
    ai:['AI Workspace','Tra cứu và nhận câu trả lời có dẫn chứng'],
    documents:['Tài liệu','Quản lý tài liệu và phiên bản'],
    review:['Duyệt tài liệu','Reviewer · kiểm tra phiên bản chờ duyệt']
  }
  function navigate(name) {
    if (!meta[name]) return
    $$('.screen').forEach((element) => element.classList.toggle('active',element.id === 'screen-' + name))
    $$('[data-route]').forEach((element) => element.classList.toggle('active',element.dataset.route === name))
    $('currentTitle').textContent = meta[name][0]
    $('currentSubtitle').textContent = meta[name][1]
    $('sidebar').classList.remove('open')
    if (location.hash !== '#' + name) { try { history.replaceState(null, '', '#' + name) } catch (_) { location.hash = name } }
  }
  $$('[data-route]').forEach((el) => el.addEventListener('click', () => navigate(el.dataset.route)))
  $$('[data-soon]').forEach((el) => el.addEventListener('click', () => toast('“' + el.dataset.soon + '” chưa nằm trong ba màn hình prototype lần này.')))
  $('mobileNav').addEventListener('click', () => $('sidebar').classList.toggle('open'))
  window.addEventListener('hashchange', () => navigate(location.hash.slice(1)))
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      if (!$('modalBackdrop').hidden) closeModal()
      else $('sidebar').classList.remove('open')
    }
  })
  document.addEventListener('click', (event) => {
    if (event.target.closest('.sidebar,.mobile-only')) return
    if (window.innerWidth <= 750) $('sidebar').classList.remove('open')
  })

  // One reusable confirmation/dialog pattern, intentionally local-only.
  function openModal(title, help, bodyHTML, confirmLabel, action, danger = false) {
    lastFocus = document.activeElement
    modalAction = action
    $('modalTitle').textContent = title
    $('modalHelp').textContent = help
    $('modalBody').innerHTML = bodyHTML
    $('modalConfirm').textContent = confirmLabel
    $('modalConfirm').classList.toggle('danger',danger)
    $('modalBackdrop').hidden = false
    $('modalClose').focus()
  }
  function closeModal() {
    $('modalBackdrop').hidden = true
    $('modalBody').replaceChildren()
    modalAction = null
    if (lastFocus && lastFocus.isConnected) lastFocus.focus()
  }
  $('modalClose').addEventListener('click', closeModal)
  $('modalCancel').addEventListener('click', closeModal)
  $('modalBackdrop').addEventListener('click', (event) => { if (event.target === $('modalBackdrop')) closeModal() })
  $('modalConfirm').addEventListener('click', () => {
    if (typeof modalAction === 'function') {
      const shouldClose = modalAction()
      if (shouldClose !== false) closeModal()
    }
  })

  // AI Workspace mock states (matches existing single-turn /ai/ask affordances).
  const aiResult = $('aiResult')
  const baseAnswer = aiResult.innerHTML
  let lastQuestion = 'Quy trình duyệt tài liệu nội bộ gồm những bước nào?'
  const scopeOptions = {
    Folder: ['Quy trình','Hướng dẫn','Biểu mẫu'],
    Document: ['Hướng dẫn quy trình tài liệu','Wiki quản lý tri thức']
  }
  function updateScope() {
    const scope = $('aiScope').value
    const show = scope !== 'All'
    $('aiScopeSpecific').hidden = !show
    if (show) {
      $('scopeDetailLabel').textContent = scope === 'Folder' ? 'Thư mục' : 'Tài liệu'
      $('aiScopeValue').innerHTML = scopeOptions[scope].map((v) => '<option value="' + escapeHTML(v) + '">' + escapeHTML(v) + '</option>').join('')
    }
  }
  $('aiScope').addEventListener('change', updateScope)
  updateScope()
  const emptyIllustration = '<div class="state-block">' + icon('sparkle') + '<h2>Chưa có câu hỏi</h2><p>Hãy nhập câu hỏi để bắt đầu tra cứu trong phạm vi được phép.</p></div>'
  const states = {
    empty:emptyIllustration,
    loading:'<div class="card card-pad stack"><div class="horizontal">' + icon('sparkle') + '<strong>Đang tìm trong kho tri thức (demo)...</strong></div><div class="skeleton" style="width:100%"></div><div class="skeleton" style="width:85%"></div><div class="skeleton" style="width:94%"></div><div class="skeleton" style="width:65%"></div></div>',
    'no-source':'<div class="state-block">' + icon('search') + '<h2>Chưa có nguồn phù hợp</h2><p>Không tìm thấy nguồn đủ phù hợp trong phạm vi đã chọn. Hãy thử diễn đạt lại hoặc chọn phạm vi khác.</p></div>',
    clarify:'<div class="notice"><strong>Copilot cần thêm ngữ cảnh.</strong><div style="margin-top:5px">Bạn muốn hỏi quy trình duyệt theo loại tài liệu hoặc phòng ban nào? (demo)</div></div>',
    denied:'<div class="state-block">' + icon('lock') + '<h2>Không thể truy cập nguồn này</h2><p>Backend có thể từ chối khi người dùng không có quyền. Prototype không thực hiện kiểm tra quyền.</p></div>',
    error:'<div class="state-block">' + icon('x') + '<h2>Không thể tải câu trả lời</h2><p>Đây là trạng thái lỗi API được mô phỏng. Hãy thử lại khi kết nối khả dụng.</p><button type="button" class="btn secondary sm" id="aiRetry" style="margin-top:12px">Thử lại</button></div>'
  }
  function renderAi() {
    const state = $('aiState').value
    if (state === 'answer') {
      aiResult.innerHTML = baseAnswer
      $('aiQuestion').textContent = lastQuestion
    } else {
      aiResult.innerHTML = states[state]
      if (state === 'error') $('aiRetry').addEventListener('click', () => { $('aiState').value = 'answer'; renderAi(); toast('Đã chuyển về trạng thái câu trả lời mẫu.') })
    }
  }
  $('aiState').addEventListener('change', renderAi)
  $('aiSend').addEventListener('click', () => {
    const question = $('aiInput').value.trim()
    if (!question) { $('aiInput').focus();toast('Hãy nhập một câu hỏi demo.');return }
    const scope = $('aiScope').value
    if (scope !== 'All' && !$('aiScopeValue').value) { toast('Vui lòng chọn phạm vi.');return }
    lastQuestion = question
    $('aiState').value = 'answer'
    renderAi()
    $('aiInput').value = ''
    toast('Đã cập nhật câu hỏi mẫu. Câu trả lời dưới đây là DEMO, không sinh bởi AI.')
  })
  $('aiInput').addEventListener('keydown', (event) => {
    if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) { event.preventDefault();$('aiSend').click() }
  })
  $('aiNew').addEventListener('click', () => { $('aiState').value = 'empty';renderAi();$('aiInput').value='';$('aiInput').focus() })
  document.addEventListener('click', (event) => {
    const cite = event.target.closest('[data-citation],[data-source]')
    if (cite) {
      const index = cite.dataset.citation || cite.dataset.source
      $$('[data-source]').forEach((node) => node.classList.toggle('selected',node.dataset.source === index))
      toast('Đã đánh dấu nguồn ' + index + '. Không có deep-link tới tài liệu thật trong contract hiện tại.')
    }
    const feedback = event.target.closest('[data-feedback]')
    if (feedback) {
      const value = feedback.dataset.feedback
      if (value === 'copy') {
        const text = [...aiResult.querySelectorAll('.answer-copy p')].map(n => n.textContent).join('\n')
        if (navigator.clipboard?.writeText) {
          navigator.clipboard.writeText(text).then(() => toast('Đã sao chép nội dung DEMO.')).catch(() => toast('Không thể sao chép trong trình duyệt hiện tại.'))
        } else toast('Trình duyệt này không hỗ trợ Clipboard trong chế độ file://.')
      } else {
        toast('Đã chọn phản hồi “' + (value === 'good' ? 'Hữu ích' : 'Không hữu ích') + '” (demo, không ghi vào backend).')
      }
    }
  })

  // Documents: sample data and reusable CRUD/detail layout.
  const originalDocuments = [
    {id:'D-01',name:'Quy trình duyệt tài liệu v2.0',type:'PDF',folder:'Quy trình',status:'Approved',date:'15/10/2026',versions:3,desc:'Quy trình kiểm soát tài liệu nội bộ (dữ liệu minh họa).'},
    {id:'D-02',name:'Hướng dẫn sử dụng kho tri thức',type:'DOCX',folder:'Hướng dẫn',status:'Approved',date:'12/10/2026',versions:2,desc:'Hướng dẫn tra cứu tài liệu và hỏi AI (dữ liệu minh họa).'},
    {id:'D-03',name:'Biểu mẫu đề xuất thay đổi',type:'DOCX',folder:'Biểu mẫu',status:'PendingReview',date:'11/10/2026',versions:1,desc:'Biểu mẫu đề xuất cập nhật (dữ liệu minh họa).'},
    {id:'D-04',name:'Quy trình vận hành bộ phận',type:'PDF',folder:'Quy trình',status:'PendingReview',date:'08/10/2026',versions:2,desc:'Tài liệu vận hành cần được reviewer kiểm tra (dữ liệu minh họa).'},
    {id:'D-05',name:'Hướng dẫn phân quyền tài liệu',type:'PDF',folder:'Hướng dẫn',status:'Rejected',date:'05/10/2026',versions:1,desc:'Phiên bản bị từ chối và cần điều chỉnh (dữ liệu minh họa).'},
    {id:'D-06',name:'Danh sách biểu mẫu phòng ban',type:'DOCX',folder:'Biểu mẫu',status:'Approved',date:'02/10/2026',versions:2,desc:'Danh sách biểu mẫu theo phòng ban (dữ liệu minh họa).'}
  ]
  const statusNames={Approved:'Đã duyệt',PendingReview:'Chờ duyệt',Rejected:'Từ chối'}
  const badgeClass={Approved:'green',PendingReview:'amber',Rejected:'red'}
  let selectedDocument = 'D-01'
  function statusBadge(status) { return '<span class="badge ' + (badgeClass[status]||'') + '">' + (statusNames[status]||status) + '</span>' }
  function fileBadge(type) { return '<span class="file-icon ' + (type==='PDF'?'pdf':'') + '">' + escapeHTML(type) + '</span>' }
  function renderDocuments() {
    const query = $('docSearch').value.toLocaleLowerCase('vi').trim()
    const status = $('docStatus').value
    const folder = $('docFolder').value
    const filtered = originalDocuments.filter(d => (d.name.toLocaleLowerCase('vi').includes(query)||d.desc.toLocaleLowerCase('vi').includes(query)) && (status==='All'||d.status===status) && (folder==='All'||d.folder===folder))
    $('docCount').textContent = filtered.length+' tài liệu demo'
    $('docFooter').textContent = 'Hiển thị '+filtered.length+' / '+originalDocuments.length+' tài liệu mẫu'
    $('docRows').innerHTML = filtered.length ? filtered.map(d =>
      '<tr data-doc-id="'+d.id+'" tabindex="0" role="button" aria-label="Xem '+escapeHTML(d.name)+'" class="'+(selectedDocument===d.id?'selected':'')+'"><td><span class="title-cell">'+fileBadge(d.type)+'<span>'+escapeHTML(d.name)+'</span></span></td><td>'+escapeHTML(d.folder)+'</td><td>'+statusBadge(d.status)+'</td><td>'+d.date+'</td></tr>'
    ).join('') : '<tr><td colspan="4" class="empty-row">Không tìm thấy tài liệu phù hợp · Thử bỏ bộ lọc</td></tr>'
    $$('[data-doc-id]').forEach(node => {
      const select = () => {selectedDocument=node.dataset.docId;renderDocuments();renderDocDetail()}
      node.addEventListener('click',select)
      node.addEventListener('keydown', e => {if(e.key==='Enter'||e.key===' '){e.preventDefault();select()}})
    })
  }
  function renderDocDetail() {
    const doc=originalDocuments.find(d=>d.id===selectedDocument)
    $('docTitle').textContent = doc ? doc.name : 'Chọn một tài liệu'
    $('docDesc').textContent = doc ? doc.desc : 'Bấm vào một dòng trong bảng để xem phiên bản và chi tiết.'
    if (!doc) {
      $('docDetailBody').innerHTML='<div class="state-block">'+icon('file')+'<h2>Chưa chọn tài liệu</h2><p>Chọn một dòng để xem trạng thái và các phiên bản.</p></div>'
      return
    }
    $('docDetailBody').innerHTML =
      '<div class="meta-grid"><div class="meta-box"><span>Thư mục</span><b>'+escapeHTML(doc.folder)+'</b></div><div class="meta-box"><span>Trạng thái</span><b>'+statusNames[doc.status]+'</b></div><div class="meta-box"><span>Số phiên bản</span><b>'+doc.versions+'</b></div><div class="meta-box"><span>Ngày cập nhật (demo)</span><b>'+doc.date+'</b></div></div>'+
      '<div class="horizontal between" style="margin:17px 0 11px"><h3>Lịch sử phiên bản</h3><button type="button" class="btn secondary sm" id="docNewVersion">'+icon('plus')+' Phiên bản mới</button></div>'+
      '<div class="version-list">'+Array.from({length:doc.versions},(_,i)=>'<div class="version"><div class="horizontal">'+fileBadge(doc.type)+'<div><strong>v'+(doc.versions-i)+'.0</strong><div class="tiny">Phiên bản mô phỏng</div></div></div>'+statusBadge(i===0?doc.status:'Approved')+'</div>').join('')+'</div>'+
      '<div class="sep"></div><div class="horizontal" style="flex-wrap:wrap"><button type="button" class="btn secondary sm" id="docDownload">'+icon('file')+' Tải file gốc</button><button type="button" class="btn subtle sm" id="docToReview">'+icon('shield')+' Xem hàng đợi duyệt</button></div>'
    $('docNewVersion').addEventListener('click',()=>openUploadDialog(true))
    $('docDownload').addEventListener('click',()=>toast('Không có file thật để tải trong HTML prototype.'))
    $('docToReview').addEventListener('click',()=>navigate('review'))
  }
  ['docSearch','docStatus','docFolder'].forEach(id=> $(id).addEventListener(id==='docSearch'?'input':'change',renderDocuments))
  $('docClear').addEventListener('click',()=>{selectedDocument=null;renderDocuments();renderDocDetail()})
  $('docUpload').addEventListener('click',()=>openUploadDialog(false))
  function openUploadDialog(version) {
    const doc=originalDocuments.find(d=>d.id===selectedDocument)
    const name = version && doc ? doc.name : ''
    openModal(version?'Tải lên phiên bản mới':'Tải tài liệu lên',
      'Mô phỏng biểu mẫu. Dữ liệu không được upload, lưu trữ hoặc gửi tới API.',
      '<form id="docDemoForm" class="form-grid">'+
      '<div><label class="label" for="uploadTitle">Tên tài liệu *</label><input id="uploadTitle" class="input" required value="'+escapeHTML(name)+'" placeholder="Nhập tên tài liệu"></div>'+
      '<div><label class="label" for="uploadFolder">Thư mục *</label><select id="uploadFolder" class="select" required><option value="Quy trình">Quy trình</option><option value="Hướng dẫn">Hướng dẫn</option><option value="Biểu mẫu">Biểu mẫu</option></select></div>'+
      '<div><label class="label" for="uploadFile">Chọn file (chỉ mô phỏng)</label><input id="uploadFile" type="file" class="input" accept=".pdf,.doc,.docx,.txt"></div>'+
      '<div class="notice">Thực tế quyền upload, versioning và trạng thái xử lý do backend quyết định.</div></form>',
      'Xác nhận demo', () => {
        if (!$('uploadTitle').value.trim()){$('uploadTitle').focus();toast('Vui lòng nhập tên tài liệu.');return false}
        toast('Đã hoàn tất thao tác mẫu. Không có file hay phiên bản mới được tạo.')
      })
  }

  // Review: local-only reviewer decisions with separate confirmation and rejection reason.
  const originalReviews=[
    {id:'R-01',doc:'Biểu mẫu đề xuất thay đổi',user:'Người gửi A',version:'v1.0',status:'PendingReview',time:'11/10/2026',desc:'Biểu mẫu đề xuất thay đổi quy trình (mô phỏng).'},
    {id:'R-02',doc:'Quy trình vận hành bộ phận',user:'Người gửi B',version:'v2.0',status:'PendingReview',time:'08/10/2026',desc:'Nội dung phiên bản vận hành cần được kiểm tra (mô phỏng).'},
    {id:'R-03',doc:'Quy chế lưu trữ nội bộ',user:'Người gửi C',version:'v1.1',status:'PendingReview',time:'07/10/2026',desc:'Bản đề xuất cập nhật phạm vi lưu trữ (mô phỏng).'},
    {id:'R-04',doc:'Hướng dẫn phân quyền',user:'Người gửi D',version:'v1.0',status:'Approved',time:'01/10/2026',desc:'Đã phê duyệt (trạng thái dữ liệu mẫu).'}
  ]
  let reviews=originalReviews.map(item=>({...item}))
  let selectedReview='R-01'
  function renderReviews() {
    const query=$('reviewSearch').value.toLocaleLowerCase('vi').trim()
    const filter=$('reviewStatus').value
    const filtered=reviews.filter(r=>(r.doc.toLocaleLowerCase('vi').includes(query)||r.user.toLocaleLowerCase('vi').includes(query))&&(filter==='All'||r.status===filter))
    const pending=reviews.filter(r=>r.status==='PendingReview').length
    $('reviewPendingCount').textContent=pending+' đang chờ'
    $('reviewCount').textContent=filtered.length+' mục'
    $('reviewRows').innerHTML=filtered.length?filtered.map(r=>'<tr tabindex="0" role="button" aria-label="Xem '+escapeHTML(r.doc)+'" data-review-id="'+r.id+'" class="'+(r.id===selectedReview?'selected':'')+'"><td><strong style="color:#2f4261">'+escapeHTML(r.doc)+'</strong><div class="tiny">'+escapeHTML(r.version)+' · '+r.time+'</div></td><td>'+escapeHTML(r.user)+'</td><td>'+statusBadge(r.status)+'</td></tr>').join(''):'<tr><td class="empty-row" colspan="3">Hàng đợi đang trống theo bộ lọc đã chọn.</td></tr>'
    $$('[data-review-id]').forEach(el=>{
      const select=()=>{selectedReview=el.dataset.reviewId;renderReviews();renderReviewDetail()}
      el.addEventListener('click',select)
      el.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();select()}})
    })
  }
  function renderReviewDetail() {
    const rev=reviews.find(r=>r.id===selectedReview)
    $('reviewTitle').textContent=rev?rev.doc:'Chọn một phiên bản'
    $('reviewVersion').textContent=rev?rev.version:''
    $('reviewDesc').textContent=rev?rev.desc:'Chọn dòng trong danh sách để xem nội dung cần review.'
    if(!rev){$('reviewDetailBody').innerHTML='<div class="state-block">Chưa chọn tài liệu.</div>';return}
    $('reviewDetailBody').innerHTML=
      '<div class="meta-grid"><div class="meta-box"><span>Người gửi</span><b>'+escapeHTML(rev.user)+'</b></div><div class="meta-box"><span>Trạng thái</span><b>'+statusNames[rev.status]+'</b></div><div class="meta-box"><span>Ngày gửi (demo)</span><b>'+rev.time+'</b></div><div class="meta-box"><span>Phiên bản</span><b>'+escapeHTML(rev.version)+'</b></div></div>'+
      '<div class="warn" style="margin:13px 0">Reviewer cần xác minh nội dung thật trước khi quyết định. Màn hình này không chứa nội dung file thực tế.</div>'+
      '<div class="horizontal" style="justify-content:flex-end;flex-wrap:wrap">'+
      (rev.status==='PendingReview'?'<button id="reviewReject" class="btn secondary">'+icon('x')+' Từ chối</button><button id="reviewApprove" class="btn">'+icon('check')+' Phê duyệt</button>':'<span class="badge '+badgeClass[rev.status]+'">Đã xử lý (demo)</span>')+'</div>'
    if(rev.status==='PendingReview'){
      $('reviewApprove').addEventListener('click',()=>openModal('Xác nhận duyệt phiên bản','Quyết định chỉ thay đổi trạng thái DEMO, không ghi vào backend.','<div class="notice" style="margin-top:17px">Phê duyệt “'+escapeHTML(rev.doc)+'” · '+escapeHTML(rev.version)+'?</div>','Phê duyệt demo',()=>applyReview(rev.id,'Approved')))
      $('reviewReject').addEventListener('click',()=>openModal('Từ chối phiên bản','Lý do từ chối là bắt buộc trong luồng xem trước.','<div class="form-grid"><div><label for="rejectReason" class="label">Lý do từ chối *</label><textarea id="rejectReason" class="textarea" placeholder="Nêu lý do cần chỉnh sửa..."></textarea></div><div class="warn">Không có yêu cầu thực tế nào được gửi đi.</div></div>','Từ chối demo',()=>{
        const reason=$('rejectReason').value.trim()
        if(!reason){$('rejectReason').focus();toast('Vui lòng nhập lý do từ chối.');return false}
        applyReview(rev.id,'Rejected')
      },true))
    }
  }
  function applyReview(id,status) {
    const row=reviews.find(r=>r.id===id)
    if(row) row.status=status
    renderReviews();renderReviewDetail()
    toast(status==='Approved'?'Đã phê duyệt DEMO. Không gọi API.':'Đã từ chối DEMO. Không gọi API.')
  }
  $('reviewSearch').addEventListener('input',renderReviews)
  $('reviewStatus').addEventListener('change',renderReviews)
  $('reviewReset').addEventListener('click',()=>{reviews=originalReviews.map(r=>({...r}));selectedReview='R-01';$('reviewSearch').value='';$('reviewStatus').value='PendingReview';renderReviews();renderReviewDetail();toast('Đã khôi phục dữ liệu mẫu.')})

  // Initialize all three screens even when opening deep link.
  renderDocuments();renderDocDetail()
  renderReviews();renderReviewDetail()
  renderAi()
  navigate(meta[location.hash.slice(1)]?location.hash.slice(1):'ai')
})()
