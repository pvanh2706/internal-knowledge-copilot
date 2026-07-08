# Kế Hoạch Thương Mại Hóa Cho Thị Trường Việt Nam

Ngày lập: 2026-07-08

Tài liệu này là kế hoạch điều phối thương mại hóa Internal Knowledge Copilot cho doanh nghiệp Việt Nam. Nó bổ sung (không thay thế) kế hoạch kỹ thuật dài hạn [KẾ_HOẠCH_SẢN_PHẨM_HÓA.md](technical/KẾ_HOẠCH_SẢN_PHẨM_HÓA.md): kế hoạch đó xếp việc theo độ chín kỹ thuật (phase A-H), tài liệu này xếp lại theo tiến trình bán hàng và bổ sung phần thị trường, giá, tính năng đáng giá.

Đối tượng đọc: người điều hành dự án và AI coding agent. AI agent nhận task phát triển nên đọc mục 7 (backlog kỹ thuật theo mốc bán hàng) và mục 8 (quy trình làm việc), sau đó đọc file phase chi tiết tương ứng trong `docs/technical/phases/` nếu có.

Ràng buộc xuyên suốt: đội thực tế là **1 dev + AI coding agent**. Phân bổ thời gian giả định **70% xây sản phẩm / 30% bán hàng và vận hành pilot**. Mọi ước lượng trong tài liệu dùng một thang duy nhất: **S** ≤ 2 ngày công, **M** = 3 ngày đến 2 tuần, **L** = 2-8 tuần, **XL** > 2 tháng (đã tính AI agent code, dev review).

## 1. Kết Luận Điều Hành

Sản phẩm không bán "chatbot hỏi đáp tài liệu" (đã bị thương mại hóa bởi ChatGPT Enterprise, Microsoft Copilot). Sản phẩm bán hai giá trị mà nhóm đó không phục vụ được cho doanh nghiệp Việt Nam:

1. **Tổng hợp tri thức có kiểm soát**: AI chỉ trả lời từ tài liệu đã được reviewer duyệt, kèm trích dẫn, có quy trình sửa sai và audit log — dữ liệu nằm trên hạ tầng của khách.
2. **Tự động hóa tri thức**: tri thức tự chảy vào từ Jira/CRM/ticketing, tự chưng cất thành KB draft chờ duyệt, tự đề xuất FAQ từ lịch sử hỏi đáp — thay vì chờ người ngồi viết wiki.

Lộ trình 12 tháng: hoàn thành hardening và bộ demo trong quý đầu → 2 pilot qua quan hệ trực tiếp → chuyển đổi thành 2-3 khách trả phí single-tenant on-prem → chuẩn hóa triển khai để mỗi khách mới tốn ≤ 2 ngày công. Chưa làm multi-tenant SaaS (đúng khuyến nghị mục 11 kế hoạch sản phẩm hóa).

Quy tắc pilot: **tối đa 1 pilot miễn phí** đổi lấy case study + quyền dùng logo khách; mọi pilot khác bắt buộc thu phí (dù nhỏ) — khách trả tiền mới dùng thật.

## 2. Thị Trường Mục Tiêu Và Định Vị

### 2.1 Phân khúc khách hàng

Xếp theo độ phù hợp với sản phẩm on-prem tiếng Việt và đội bán hàng 1 người:

| Ưu tiên | Phân khúc | Pain point | Rào cản mua | Điều kiện chào bán |
| --- | --- | --- | --- | --- |
| 1 | Công ty phần mềm/outsourcing 50-500 người | Tri thức dự án rải rác, turnover cao, onboarding chậm | Tâm lý "tự build được" | Sẵn sàng ngay sau mốc 1 |
| 1 | Công ty có team support/vận hành lớn (SaaS nội địa, ISP, phần mềm kế toán/HRM) | Ticket lặp lại, tri thức nằm trong đầu nhân viên cũ | Cần connector với hệ ticket đang dùng | Bán Q&A trước, connector Jira là mũi upsell |
| 2 | Sản xuất có nhà máy (SOP, ISO) | SOP/ISO dạng scan, khó tra cứu, audit tốn công | Tài liệu scan cần OCR; IT mỏng cần cài trọn gói | **Chỉ chào chủ động sau khi có OCR 1 provider** (mục 7.4) |
| 3 | Bảo hiểm, chứng khoán, tài chính tiêu dùng | Quy định thay đổi liên tục, tư vấn sai gây rủi ro pháp lý | Compliance/pentest, chu kỳ mua 3-6 tháng | Sau khi có khách tham chiếu + security overview vững |
| 3 | Bệnh viện/phòng khám tư, chuỗi nha khoa | Dữ liệu nhạy cảm không được lên SaaS nước ngoài | Rất thận trọng dữ liệu | Sau khi có DPA + security overview |
| Sau | Ngân hàng, cơ quan nhà nước | Pain lớn nhưng đấu thầu 6-18 tháng | Không khả thi với 1 người | Chỉ qua đối tác SI, không chủ động |

12 tháng đầu chỉ tập trung 2 phân khúc ưu tiên 1, trong mạng lưới quan hệ sẵn có. Phân khúc support/vận hành là "đất" tốt nhất vì trùng giá trị lõi tự động hóa và ROI đo được ngay (thời gian xử lý ticket).

### 2.2 Cạnh tranh và khoảng trống

- **Quốc tế** (ChatGPT Enterprise, M365 Copilot, Glean, Notion AI): SaaS nước ngoài — dữ liệu rời Việt Nam, giá USD per-seat vượt ngân sách SME, và không có workflow duyệt tri thức (AI trả lời trên mọi thứ kể cả bản nháp/tài liệu sai).
- **Nội địa lớn** (VNPT iKNOW, Viettel AI, FPT AI): bán theo dự án lớn/cloud của chính họ, nhắm tập đoàn và chính phủ — bỏ trống SME 50-500 người ngân sách vài chục đến vài trăm triệu/năm.
- **Chatbot doanh nghiệp nhỏ lẻ**: RAG demo theo dự án, không có governance (không reviewer, không citation bắt buộc, không feedback loop, không audit).

Khoảng trống sản phẩm này chiếm: **(1)** on-prem thật sự trên Windows Server/IIS khách đã có; **(2)** governance là tính năng lõi; **(3)** tiếng Việt end-to-end + chọn được AI provider theo tenant (kể cả model nội địa/self-host chuẩn OpenAI-compatible); **(4)** giá SME, triển khai tính bằng ngày.

Rủi ro cạnh tranh: VNPT/FPT hạ xuống SME với giá rẻ. Đối sách: đi nhanh ở ngách governance + connector ticket/CRM và giữ chi phí vận hành gần 0 nhờ self-host.

### 2.3 Định vị và luận điểm bán hàng

Positioning statement:

> Internal Knowledge Copilot là trợ lý tri thức nội bộ tiếng Việt cài trên hạ tầng của chính doanh nghiệp: nhân viên hỏi — AI trả lời kèm trích dẫn, nhưng chỉ từ tài liệu đã được người có thẩm quyền duyệt. File gốc, chỉ mục và lịch sử hỏi đáp ở lại công ty; tri thức từ ticket/CRM được chưng cất thành KB có kiểm soát thay vì thất thoát theo nhân viên nghỉ việc.

Sales pitch khi gặp khách:

- "File gốc, chỉ mục và lịch sử hỏi đáp ở lại công ty anh/chị. Khi dùng AI cloud, chỉ câu hỏi và trích đoạn liên quan được gửi đến nhà cung cấp AI — hoặc chọn chế độ AI nội địa/self-host để không gì rời hệ thống." (Không hứa quá: minh bạch điểm này cả trong security overview.)
- "AI chỉ trả lời từ tài liệu đã duyệt, kèm trích dẫn — trả lời sai thì truy được nguồn, có quy trình sửa và người chịu trách nhiệm."
- "Nhân viên nghỉ, tri thức ở lại — kinh nghiệm xử lý ticket được chưng cất thành wiki đã duyệt."
- "Nối thẳng vào Jira/CRM đang dùng, không bắt đổi công cụ."
- "Giá SME, triển khai tính bằng ngày, pilot có số liệu sau 4 tuần."

Lưu ý: chỉ đưa "chế độ AI nội địa" vào tài liệu bán hàng **sau khi** đã chạy eval 20-30 golden question tiếng Việt trên 2-3 model nội địa/self-host bằng module Evaluation sẵn có (S — xem mục 10).

## 3. Pháp Lý Và Compliance Làm Lợi Thế Bán

Bối cảnh: Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 hiệu lực 01/01/2026 (Nghị định 13/2023/NĐ-CP hướng dẫn trong giai đoạn chuyển tiếp); Luật An ninh mạng 2018 + Nghị định 53/2022/NĐ-CP về lưu trữ dữ liệu tại Việt Nam. Tâm lý "không đưa tài liệu nội bộ lên SaaS nước ngoài" phổ biến ở tài chính, y tế, sản xuất FDI.

Sản phẩm đáp ứng sẵn: toàn bộ dữ liệu nằm trên hạ tầng khách; AI provider cấu hình theo tenant; audit log + phân quyền folder/team là bằng chứng kiểm soát truy cập.

Việc cần làm để biến thành công cụ bán (phần lớn là văn bản, AI agent soạn nháp được):

| Việc | Vì sao | Độ khó |
| --- | --- | --- |
| Security overview 5-10 trang tiếng Việt (kiến trúc, data flow, mã hóa, backup, giới hạn hiện tại nêu trung thực) | Tài liệu đầu tiên IT/security của khách đòi; không có thì deal đứng | S |
| DPA mẫu + phụ lục đối chiếu Luật 91/2025 và NĐ 13/2023 (thuê luật sư rà 1 lần ~5-10 triệu, tái sử dụng) | Rút vòng pháp chế của khách từ tuần xuống ngày | S-M |
| Trang "Dữ liệu của bạn ở đâu" trong tài liệu bán hàng | Biến đặc tính kỹ thuật thành thông điệp người không chuyên hiểu được | S |
| Cấu hình retention/xóa dữ liệu theo yêu cầu chủ thể dữ liệu | Luật 91/2025 trao quyền yêu cầu xóa; khách sẽ hỏi | M (nâng ưu tiên từ v1.2 roadmap) |

## 4. Gói Sản Phẩm Và Định Giá

Mọi con số giá là **giả thuyết định vị, phải kiểm chứng qua 2-3 deal đầu**. Sau 3 deal: khách chốt quá nhanh nghĩa là giá thấp; deal nào cũng mặc cả triển khai thì tách nhỏ hạng mục triển khai.

### 4.1 Mô hình deploy

- **Single-tenant on-prem (mặc định)**: mỗi khách một bộ cài trên Windows Server/IIS của khách. Sẵn sàng nhất về codebase; khớp tâm lý dữ liệu nội bộ; chi phí vận hành vendor thấp nhất. Vì mỗi instance chỉ có 1 tenant thật, điểm yếu cô lập tenant thủ công không phải blocker bán hàng — nhưng vẫn phải sửa (mục 7.1) để chống leak giữa team trong một instance và làm nền cho SaaS sau này.
- **Single-tenant managed cloud**: VPS riêng từng khách tại datacenter VN (Viettel/VNG/FPT Cloud), vendor lo update/backup. Chỉ nhận khi đã xong monitoring + backup drill (mốc 2); tự động hóa provision trước khi nhận quá 3 khách.
- **Multi-tenant SaaS**: chưa bán. Giữ tenant model hiện tại làm nền tương lai.

### 4.2 Ba gói

Phần lớn tính năng dựa trên module sẵn có; riêng export báo cáo, cơ chế license và enforce giới hạn là việc phải xây (mục 7.5).

| Hạng mục | Pilot (3 tháng) | Standard | Enterprise |
| --- | --- | --- | --- |
| AI Q&A citation, upload/duyệt/versioning, feedback loop, wiki, dashboard, audit | ✔ | ✔ | ✔ (+ export báo cáo) |
| Integrations API (objects/permissions/events sync) | — | 1 application | Không giới hạn |
| Connector chuẩn (Jira) | — | Add-on 20-40 triệu/năm | Bao gồm |
| Connector custom (CRM/ticketing riêng) | — | — | Tính riêng 50-150 triệu/connector |
| Workflow copilot + action approval | — | — | ✔ |
| AI provider | Vendor bao tiêu có quota (vd 3.000 câu/tháng) hoặc key khách | Key khách | Key khách hoặc self-host |
| Giới hạn user / tài liệu active | 20 / 200 | 100 / 2.000 | Thỏa thuận |
| SLA hỗ trợ | Email best-effort | Phản hồi 1 ngày làm việc | Phản hồi 1 ngày làm việc, sự cố nghiêm trọng ưu tiên xử lý ngay; 1 buổi đào tạo/quý |

Lưu ý SLA: chưa cam kết "4 giờ" bằng văn bản khi chỉ có 1 người — dùng điều khoản loại trừ rõ ràng cho đến khi có người thứ hai hoặc SI nhận support L1.

Lưu ý hạ tầng: khách Standard **> 50 user kích hoạt bắt buộc** chuyển PostgreSQL/SQL Server + tách worker (Phase C kế hoạch sản phẩm hóa), tính vào phí triển khai.

### 4.3 Khung giá (VND, giả thuyết)

Định vị: không đấu per-user với MISA AMIS/Base.vn/1Office (~100-130k/user/tháng) — bán theo **deployment/năm** vì giá trị nằm ở AI + kiểm soát tri thức, on-prem một cục dễ báo giá dễ ký, và 1 dev không kham nổi billing per-user.

| Khoản | Pilot | Standard | Enterprise |
| --- | --- | --- | --- |
| License/subscription năm | 15-30 triệu trọn gói 3 tháng (1 pilot đầu có thể miễn phí đổi case study) | 120-200 triệu/năm | 300-600 triệu/năm |
| Triển khai ban đầu (một lần) | Gộp | 30-60 triệu | 80-200 triệu |
| Support/maintenance từ năm 2 | — | 15-20% license | 20% license |

### 4.4 Chi phí AI — 3 phương án

Kiến trúc đã cho phép cấu hình provider theo tenant, cả 3 phương án khả thi không cần sửa kiến trúc:

- **A. Khách tự cung cấp API key (mặc định cho Standard/Enterprise)**: vendor không ôm rủi ro chi phí; cần tài liệu "mua và cấu hình key trong 30 phút". Bắt buộc kèm **cost/token dashboard + ngưỡng cảnh báo trước go-live khách #1** (S — dữ liệu `ai_interactions` đã có, chỉ thiếu tổng hợp) để tránh bill shock giết tái tục.
- **B. Vendor bao tiêu có quota (cho Pilot)**: trải nghiệm mua đơn giản nhất; cộng biên an toàn ~2x chi phí dự kiến; quota cứng theo số câu hỏi/tháng.
- **C. LLM self-host cho khách air-gapped**: chỉ nhận khi phí triển khai ≥ 150 triệu; khách cần GPU; chưa chào chủ động khi chưa có kết quả eval model nội địa.

### 4.5 Chuẩn bị ký hợp đồng B2B

- Báo giá mẫu 1 trang, hiệu lực 30 ngày, tách license/triển khai/support (S).
- Hợp đồng + NDA mẫu, thuê luật sư rà 1 lần. **Bắt buộc có điều khoản trách nhiệm AI**: miễn trừ nội dung do AI sinh trong giới hạn hợp lý, trách nhiệm giới hạn theo giá trị hợp đồng; mọi kịch bản trả lời hướng ra khách hàng cuối bắt buộc chế độ "chỉ KB đã duyệt + escalate người thật khi confidence thấp".
- Pháp nhân theo giai đoạn: pilot quen biết → cá nhân/hộ kinh doanh hoặc ký qua công ty đối tác quen (chia %); **lập TNHH một thành viên ngay khi có tín hiệu deal Standard đầu tiên** (xuất hóa đơn VAT, đủ tư cách ký enterprise).
- Điều khoản chống rủi ro key-person mà khách chắc chắn hỏi ("công ty anh dừng thì hệ thống tôi ra sao"): license hết hạn chỉ chuyển read-only (không kill-switch), cam kết bàn giao/source escrow khi ngừng kinh doanh, bộ runbook đủ để IT khách tự vận hành.
- Hồ sơ pilot: dùng luôn [MẪU_BÁO_CÁO_KẾT_QUẢ_PILOT.md](pilot/MẪU_BÁO_CÁO_KẾT_QUẢ_PILOT.md) và [MẪU_HỖ_TRỢ_VÀ_BÁO_LỖI.md](pilot/MẪU_HỖ_TRỢ_VÀ_BÁO_LỖI.md) làm phụ lục hợp đồng.

## 5. Kênh Bán Và KPI Thương Mại

### 5.1 Kênh theo thứ tự

1. **Quan hệ trực tiếp (kênh chính 6 tháng đầu)**: 1-2 công ty quen trong phân khúc ưu tiên; điều kiện tiên quyết là demo end-to-end 30 phút với demo dataset.
2. **Case study từ pilot**: mỗi pilot kết thúc bằng case study 2 trang có số liệu KPI (bộ KPI trong [TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md](TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md) mục 2); quyền dùng tên khách thỏa thuận ngay trong hợp đồng pilot.
3. **Đối tác SI (từ tháng 6+)**: SI vừa/nhỏ và đơn vị triển khai ERP/CRM nội địa thiếu "món AI"; cần gói cài chuẩn hóa + chia doanh thu đơn giản (20-30% năm đầu); 1-2 đối tác làm thật hơn 5 MOU.
4. **Nội dung kỹ thuật tiếng Việt**: 1-2 bài/tháng (AI agent soạn, dev duyệt) trên blog/LinkedIn/cộng đồng CTO.

### 5.2 KPI 6-12 tháng (đã hiệu chỉnh theo quỹ thời gian 30% cho bán hàng)

| KPI | 6 tháng | 12 tháng |
| --- | --- | --- |
| Pilot khởi động (chỉ nhận khi có champion nội bộ rõ ràng) | 2 | 3-5 |
| Khách trả phí đang hoạt động | 1 | 2-3 |
| Doanh thu quy đổi/tháng | 10-25 triệu | 30-60 triệu |
| Case study công bố được | 1 | 2 |
| Đối tác SI "hoạt động thật" (đã cùng chốt ≥ 1 deal) | 0 (tiếp xúc) | 1 |
| Thời gian triển khai khách mới | ≤ 5 ngày công | ≤ 2 ngày công |
| Churn khách trả phí | 0 | ≤ 1 |

Tín hiệu xoay hướng: sau 6 tháng pilot dùng đều nhưng không ai chịu trả phí → vấn đề là giá hoặc phân khúc; thử chuyển từ thuê bao sang license + phí triển khai một lần (mô hình SME Việt Nam quen hơn), hoặc chuyển mũi bán chính từ Q&A sang tự động hóa connector.

### 5.3 Điểm hòa vốn sơ bộ

Chi phí ~45-65 triệu/tháng (chủ yếu opportunity cost công dev). Mức sàn "dự án tự thở" (nuôi chi phí ngoài công dev): 1 khách pilot có phí. Hòa vốn thật: ~3 khách Standard hoặc 1 Enterprise + 1 Standard trong năm đầu. Từ năm 2, support 15-20% tái tục là doanh thu biên cao — đừng bán rẻ năm đầu rồi tăng sốc.

## 6. Tính Năng Đáng Giá Cần Phát Triển

Nguyên tắc: xếp theo giá trị thương mại × độ khả thi; tận dụng hạ tầng sẵn có (Integrations, BackgroundJobs, ActionApprovals, Wiki, AiProvider); **mỗi quý chỉ cam kết 1 tính năng L**; mọi connector mới tái dùng worker `DocumentChanged` + pipeline index, không viết pipeline riêng.

### 6.1 Top 10 theo lịch đã cân đối nguồn lực

| # | Tính năng | Giá trị bán | Độ khó | Lịch |
| --- | --- | --- | --- | --- |
| 1 | Demo dataset tiếng Việt theo ngành + trang "AI trả lời được gì" | Mở khóa mọi buổi demo; chi phí thấp nhất, tác động deal cao nhất | S | Q3/2026 |
| 2 | Export báo cáo pilot PDF | Champion phía khách dùng để xin ngân sách | S | Q3/2026 |
| 3 | Cost/token dashboard + cảnh báo | Điều kiện go-live khách #1, chống bill shock | S | Q4/2026 |
| 4 | Tự đề xuất FAQ từ lịch sử hỏi đáp (clustering embedding + draft chờ duyệt) | Chứng minh "tri thức tự sinh" ngay trong pilot | M | Q4/2026 |
| 5 | Jira connector + chưng cất ticket → KB draft (kèm PII detection cơ bản) | Use case ROI đo được; khác biệt hóa mạnh nhất | L | Khởi động Q4/2026, hoàn thành Q1/2027 |
| 6 | Zalo OA làm kênh hỏi đáp | Khác biệt hóa cho thị trường VN; demo ấn tượng | M | Q1/2027, sau khảo sát 1 ngày về chi phí/chính sách OA API (giả thuyết cần kiểm chứng) |
| 7 | Quy tắc tự động (khi X → làm Y) + webhook outbound ký HMAC | Nâng định vị từ chatbot lên nền tảng tự động hóa | M | Q1-Q2/2027 |
| 8 | SSO Entra ID | Gate của IT enterprise | M | **Trigger-based**: khởi động trong 2 tuần khi hợp đồng yêu cầu |
| 9 | OCR tiếng Việt (1 provider trước, adapter `IOcrProvider`) | Mở khóa kho tài liệu scan | M-L | **Trigger-based**: bắt buộc trước khi chào chủ động phân khúc sản xuất |
| 10 | Google Drive/SharePoint sync | Gỡ rào cản "tài liệu đang ở Drive" | L | Q2/2027 hoặc theo yêu cầu khách trả phí |

Backlog chờ tín hiệu khách: Teams/Slack bot (đi kèm deal Microsoft 365), email inbox ingestion, ingest Confluence/website (bán kèm dịch vụ migration), gom cụm nguồn trùng chủ đề → wiki hợp nhất (hạ scope M: chỉ document-level, tái dùng clustering của FAQ), mobile-responsive (làm cùng đợt refactor component frontend), action execution sâu vào CRM (chỉ làm với design partner).

### 6.2 Ghi chú thiết kế các tính năng chính

- **Jira connector (ưu tiên chiến lược số 1 về tính năng)**: sync issue resolved qua JQL/webhook → worker `DocumentChanged` kéo content → AI chưng cất "triệu chứng/nguyên nhân/cách xử lý" thành wiki draft (dùng prompt versioning sẵn có) → reviewer duyệt → team support hỏi AI; bước xa hơn là bot trả lời khách hàng cuối **chỉ từ KB đã duyệt**. Đi kèm bắt buộc: PII detection cơ bản (regex SĐT/email/CCCD + cảnh báo reviewer trước khi index) vì ticket chứa dữ liệu cá nhân khách hàng cuối — nhất quán với luận điểm bán dựa trên Luật 91/2025. Phụ thuộc: outbound secret store, rate limiting integration. Làm connector Jira cụ thể trước, chỉ "framework hóa" (`IKnowledgeConnector`: TestConnection / SyncObjects(cursor) / MapAcl / DistillCandidates) khi bắt đầu connector thứ 2.
- **FAQ tự đề xuất**: job định kỳ gom câu hỏi tương đồng trong `ai_interactions` bằng cosine similarity (không cần thư viện ML) → sinh draft FAQ cho câu hỏi lặp nhiều chưa có wiki → hàng đợi reviewer. Cần đủ dữ liệu interaction thật (~2-4 tuần pilot).
- **OCR provider matrix** (khi kích hoạt): Azure Document Intelligence (managed cloud/Microsoft), FPT.AI OCR (dữ liệu lưu tại VN — lợi thế bán riêng), Tesseract + tessdata `vie` (on-prem không internet, fallback). Adapter + confidence threshold + warning reviewer theo acceptance Phase F.
- **"Báo cáo sức khỏe tri thức" hàng tháng** (S, sau khi knowledge gap dashboard của Phase E xong): số câu hỏi không trả lời được, tài liệu quá hạn, nội dung mâu thuẫn — trang khiến CEO/COO thấy giá trị bằng số, căn cứ upsell.

## 7. Kế Hoạch Kỹ Thuật Theo Mốc Bán Hàng

Phase A-H của [KẾ_HOẠCH_SẢN_PHẨM_HÓA.md](technical/KẾ_HOẠCH_SẢN_PHẨM_HÓA.md) vẫn là khung dài hạn. Dưới đây sắp lại theo thời điểm khách chạm vào hệ thống. Mục không xuất hiện = hoãn sau khách #5 (Phase G Qdrant, wiki editor đầy đủ Phase E, OpenTelemetry đầy đủ, multi-tenant SaaS) — chỉ kích hoạt khi ≥ 2 khách trả phí cùng yêu cầu.

### 7.1 Pre-sales hardening — làm ngay, trước mọi buổi demo (~7-10 ngày)

Các lỗ hổng đã xác định qua review code 2026-07. Không có phụ thuộc chéo trừ ghi chú.

| # | Hạng mục | File/module chính | Ước lượng | Acceptance criteria |
| --- | --- | --- | --- | --- |
| 1 | EF global query filter theo tenant | `Infrastructure/Database/AppDbContext.cs` + `Infrastructure/Tenancy` | M | Mọi entity có `TenantId` khai báo `HasQueryFilter` đọc từ tenant context; rà lại các filter thủ công; ≥ 3 test "user tenant A truy vấn dữ liệu tenant B trả rỗng/404" cho documents/wiki/ai_interactions; 97 test cũ pass. Lý do: chống leak giữa team/tenant demo trong một instance và là nền bắt buộc cho SaaS sau này. |
| 2 | Fail-fast JWT signing key | `Program.cs` (~dòng 132), `JwtOptions.cs`, `appsettings.json` | S | Xóa default key khỏi code + appsettings gốc (chuyển sang Development); Production mà key rỗng/ngắn hơn 32 ký tự/trùng chuỗi dev → throw lúc khởi động với message rõ. |
| 3 | Mã hóa API key AI provider trong DB | `Modules/AiSettings`, `IDataProtector` | M | Key mã hóa trước khi lưu; migrate dữ liệu cũ một lần có cờ; API trả key dạng masked; test round-trip pass. |
| 4 | Rate limiting `/api/ai/*` + `/api/integrations/*` | `Program.cs`, RateLimiter built-in .NET 8 | S | Policy theo user (vd 10 req/phút) và theo integration key; 429 kèm ApiError chuẩn; ngưỡng đọc từ config. |
| 5 | Global exception handler | `Program.cs`, `IExceptionHandler` | S | Exception chưa bắt trả JSON một schema (`code`, `message`, `traceId`); không lộ stack trace ở Production; controller tự try/catch lệch schema được rà về chuẩn. |
| 6 | Migrate/seed theo môi trường | `Program.cs` dòng 152-158, `DevelopmentSeeder` | S | MigrateAsync chỉ chạy khi Development hoặc cờ `Database:MigrateOnStartup`; seeder guard theo `IHostEnvironment`, tuyệt đối không chạy Production; smoke step xác nhận Production + DB trống không sinh user demo. |
| 7 | `downloadDocument` thiếu `X-Tenant-Code` | `src/frontend/src/api/documents.ts` (~dòng 98) | S | Thêm `apiRequestBlob` trong `http.ts` thay fetch trần; tải file pass ở tenant khác default. Gộp một đợt với #8. |
| 8 | Xử lý 401 tập trung + auto logout | `src/frontend/src/api/http.ts`, store auth | S | 401 → xóa token, về trang đăng nhập kèm thông báo hết phiên; không còn trang "đứng hình" khi token hết hạn. |
| 9 | Bỏ credential demo hardcode | `src/frontend/src/pages/auth/LoginPage.vue` | S | Không còn username/password mẫu trong code; thông tin demo chuyển vào tài liệu/seeder dev. |
| 10 | `engines` Node >= 18 | `src/frontend/package.json`, `.npmrc` | S | `engines.node >= 18` + `engine-strict=true`; Node cũ báo lỗi rõ. |
| 11 | ESLint + strict tsconfig | `src/frontend/` | M | `npm run lint` và `npm run build` pass sạch với `strict: true`; lint vào gate mục 8. |

### 7.2 Mốc 1 — Sẵn sàng POC/demo khách (~2 tuần sau hardening, trong Q3/2026)

- Toàn bộ 7.1.
- Fix config embedding local/test mặc định mock (Phase A) — demo không được chết vì thiếu real key.
- Demo dataset tiếng Việt theo ngành + demo script 30 phút (Phase H kéo lên sớm).
- Production AI provider setup guide (Phase A).
- `scripts/smoke-mvp.ps1` pass trên máy sạch.
- Song song (không phải code): security overview, DPA mẫu, báo giá mẫu (mục 3, 4.5).

Acceptance: cài trên máy Windows sạch theo tài liệu trong < 1 buổi; demo end-to-end upload → duyệt → hỏi có citation → feedback → wiki trong 30 phút không lỗi; không còn credential/key demo trong code; cross-tenant test pass. Playwright E2E hoãn sang mốc 2 (smoke + 97 test backend đủ cho POC).

### 7.3 Mốc 2 — Sẵn sàng production khách #1 on-prem (~4-6 tuần sau mốc 1, Q4/2026)

- Playwright E2E luồng chính (Phase A).
- Structured logging JSON + `/health` endpoint + cảnh báo failed job (Phase B rút gọn — chưa cần OpenTelemetry đầy đủ).
- Backup/restore drill lặp lại được (mở rộng `scripts/backup-local.ps1`); restore drill chạy thật 1 lần trước go-live.
- File type sniffing + size limit + quarantine (Phase D rút gọn; malware scan đầy đủ hoãn nếu khách chấp nhận — ghi rõ trong security overview).
- Audit document view/download (Phase D).
- Job claiming atomic trong `ProcessingJobService` (Phase C rút gọn — claim theo batch + transaction, chưa cần tách worker).
- Cost/token dashboard + ngưỡng cảnh báo (mục 4.4).
- Runbook vận hành + checklist triển khai tại khách.

Acceptance: chạy 2 tuần liên tục ở staging không can thiệp tay; restore drill thành công; E2E pass; IT khách vận hành theo runbook không cần gọi dev; test permission leak pass.

**Ngoại lệ có điều kiện về SQLite** (điều chỉnh so với DoD v1 của kế hoạch sản phẩm hóa, quyết định ngày 2026-07-08): khách #1 được chạy SQLite nếu ≤ ~50 user, có backup/restore drill, và hợp đồng có điều khoản cam kết migrate khi vượt ngưỡng. Chuyển PostgreSQL/SQL Server + tách worker (Phase C đầy đủ) là **hạng mục trigger-based**: kích hoạt khi có khách > 50 user hoặc khách yêu cầu — làm 1 lần, mọi khách sau hưởng. SSO cũng trigger-based như mục 6.1.

### 7.4 Mốc 3 — Sẵn sàng nhân bản cho khách #2-5 (~6-8 tuần sau mốc 2, Q1/2027)

| Hạng mục | Ước lượng | Thiết kế phác |
| --- | --- | --- |
| License key + feature flag theo gói | M (~2 tuần) | File JSON ký **Ed25519** (private key ở vendor, public key nhúng binary): tên khách, tenant code, hạn, gói, giới hạn user/tài liệu, connector được bật. `ILicenseService.IsFeatureEnabled(...)`; middleware chặn endpoint ngoài gói; UI ẩn menu theo flag; hết hạn → **read-only, không kill-switch**; verify offline hoàn toàn. Enforce giới hạn tại điểm tạo user + upload. Kèm usage report tháng (JSON/CSV) làm căn cứ gia hạn. |
| Installer PowerShell một lệnh | M | `scripts/install.ps1`: check prerequisite (.NET 8 Hosting Bundle, IIS), giải nén artifact build sẵn (khách không cần Node), tạo app pool + site, sinh `appsettings.Production.json` từ tham số (JWT key random, license path), migration có kiểm soát, smoke, in pass/fail. Kèm `CHECKLIST_TRIỂN_KHAI_KHÁCH.md`. Tận dụng [TRIỂN_KHAI_IIS.md](../TRIỂN_KHAI_IIS.md). |
| Cơ chế update tại khách | M | `scripts/update.ps1`: backup tự động (DB+storage+config, timestamp) → stop pool → thay binary → migration qua cờ `--migrate` → start + smoke → fail thì `rollback.ps1`. SemVer + release notes; migration phải backward-compatible 1 phiên bản (không drop cột ngay). **Chính sách version**: chỉ hỗ trợ N và N-1; hợp đồng support yêu cầu khách nhận update ≥ 2 lần/năm. |
| Log bundle export | S | Nút admin + script dự phòng: log 7 ngày, health check, job failed/dead-letter, config đã redact secret, version + license → zip gửi qua ticket. Điều kiện sống còn để 1 dev support nhiều khách on-prem không cần remote access. |
| Telemetry tối thiểu | S | Không "gọi về nhà". Trang admin "Báo cáo sử dụng" tổng hợp KPI tháng, xuất JSON/CSV khách chủ động gửi. Ghi rõ trong security overview. |
| Jira connector hoàn thành (khởi động từ Q4/2026) | L tổng | Xem mục 6.2, kèm PII detection cơ bản. |
| Tài liệu sản phẩm + release notes có version | M | Admin/reviewer/user/security/deployment. |
| Evaluation gate bắt buộc trước khi đổi prompt/model | S | Module Evaluation đã có — đưa vào quy trình gate mục 8. |

Acceptance mốc 3: cài mới cho "khách giả lập" trên môi trường sạch bằng installer + checklist trong ≤ nửa ngày; update N-1 → N có rollback thử thật; license hết hạn/thiếu tính năng bị chặn đúng; Jira connector sync project thật vào KB với ACL đúng.

## 8. Quy Trình Phát Triển Với AI Agent

- **1 phase = 1 file kế hoạch cho AI** tại `docs/technical/phases/PHASE_<số>_<tên>.md`, cấu trúc cố định: (1) mục tiêu 2-3 câu; (2) file/module liên quan kèm đường dẫn; (3) task đánh số, mỗi task có acceptance criteria kiểm chứng được bằng lệnh; (4) ràng buộc (không đổi API công khai, không thêm dependency ngoài danh sách, UI text và comment tiếng Việt theo [QUY_TẮC_CODE.md](../QUY_TẮC_CODE.md)); (5) lệnh xác minh cuối phase. Phase ≤ 1 tuần công, ≤ ~10 task — to hơn thì tách.
- **Acceptance test**: task backend kèm ≥ 1 test tự động (hoặc bước smoke mới nếu là hạ tầng); task frontend pass `npm run lint` + `npm run build` + test liên quan. Task không có cách kiểm chứng tự động phải ghi bước kiểm tay trong file phase — không có acceptance thì không giao cho AI agent.
- **Gate đóng phase**: `dotnet test` pass toàn bộ; `npm run build` + `npm run lint` pass; `scripts/smoke-mvp.ps1` pass; phase chạm prompt/model/retrieval thì eval suite (module Evaluation) không tụt so với baseline. Kết quả gate ghi cuối file phase trước commit "Hoàn thiện phase X".
- **Giao việc**: mỗi task một commit, không trộn refactor với feature; dev review diff + chạy gate, tự tay chỉ phần thiết kế và các quyết định ở mục 10.

## 9. Rủi Ro Và Biện Pháp

| Rủi ro | Biện pháp |
| --- | --- |
| Permission leak (nghiêm trọng nhất về uy tín) | Mục 7.1 #1; giữ nguyên tắc SQLite là source of truth cho quyền; test cross-tenant/cross-team trong mọi phase chạm retrieval |
| Trách nhiệm pháp lý khi AI trả lời sai, nhất là external-facing | Điều khoản hợp đồng mục 4.5; external-facing bắt buộc "chỉ KB đã duyệt + escalate người thật"; disclaimer trong UI |
| Bill shock chi phí AI của khách | Cost/token dashboard + cảnh báo trước go-live khách #1 |
| Key-person (mọi thứ ở 1 người) | License read-only không kill-switch; cam kết bàn giao/escrow; runbook đủ để IT khách tự chạy; tiến tới SI nhận L1 |
| 5 khách on-prem khác version thành gánh nợ vận hành | Installer + update/rollback script; chính sách N/N-1; update ≥ 2 lần/năm trong hợp đồng support |
| VNPT/FPT hạ xuống SME | Đi nhanh ngách governance + connector; chi phí vận hành gần 0 |
| Quá tải 1 dev (double-booking) | Backlog duy nhất theo mốc bán hàng (mục 7); mỗi quý 1 tính năng L; hạng mục trigger-based chỉ khởi động khi có deal yêu cầu; KPI đã hiệu chỉnh theo 70/30 |
| Chất lượng AI nội địa chưa kiểm chứng nhưng đã chào bán | Chạy eval golden questions trước khi đưa vào tài liệu bán (mục 10) |
| Pilot dùng đều nhưng không chuyển đổi | Tín hiệu xoay hướng mục 5.2 |

## 10. Quyết Định Cần Chốt Trong 30 Ngày (đến 2026-08-08)

| Quyết định | Lựa chọn | Ghi chú |
| --- | --- | --- |
| DB production khi kích hoạt Phase C | PostgreSQL hay SQL Server (Express) | Ảnh hưởng installer/update script; SQL Server Express có thể hợp khẩu vị khách Windows Server; spike 2 ngày rồi chốt |
| AI provider production mặc định khuyến nghị cho khách | OpenAI / Azure OpenAI / openai-compatible nội địa | Kèm danh sách model khuyến nghị theo gói |
| Eval "AI nội địa" | Chạy 20-30 golden question tiếng Việt trên 2-3 model nội địa/self-host bằng module Evaluation (S) | Điều kiện để đưa "chế độ AI nội địa" vào tài liệu bán |
| Khảo sát Zalo OA API | 1 ngày: chi phí gói, yêu cầu OA xác thực, giới hạn tin chủ động | Điều kiện cam kết lịch Q1/2027 cho tính năng #6 |
| Pháp nhân | HKD trước / TNHH khi có tín hiệu deal Standard | Mục 4.5 |
| Khách pilot đầu tiên | Chọn 2 ứng viên cụ thể từ quan hệ sẵn có, xác định champion | Điều kiện khởi động mọi KPI mục 5.2 |
| Giá chính thức | Giữ khung mục 4.3, chốt lại sau 2-3 deal | |

## 11. Tài Liệu Liên Quan

- [Kế hoạch sản phẩm hóa (kỹ thuật, phase A-H)](technical/KẾ_HOẠCH_SẢN_PHẨM_HÓA.md)
- [Tổng quan trạng thái roadmap](TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md)
- [Hướng dẫn tích hợp bên thứ 3](technical/HUONG_DAN_TICH_HOP_BEN_THU_3.md)
- [Hướng dẫn pilot và sử dụng](pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md)
- [Mẫu báo cáo kết quả pilot](pilot/MẪU_BÁO_CÁO_KẾT_QUẢ_PILOT.md)
- [Hướng dẫn trình bày và demo](presentation/HƯỚNG_DẪN_TRÌNH_BÀY_VÀ_DEMO.md)
- [Triển khai IIS](../TRIỂN_KHAI_IIS.md)
- [Quy tắc code](../QUY_TẮC_CODE.md)
