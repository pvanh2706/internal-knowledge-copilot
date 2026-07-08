import type { AiActionRequestStatus } from '@/api/actionApprovals'
import type { AiConfidence } from '@/api/ai'
import type { ApplicationStatus } from '@/api/applications'
import type { DocumentStatus, DocumentVersionStatus } from '@/api/documents'
import type {
  AiFeedbackValue,
  AiQualityIssueStatus,
  FeedbackReviewStatus,
  KnowledgeCorrectionStatus,
} from '@/api/feedback'
import type { IntegrationConnectionStatus } from '@/api/integrations'
import type { ExternalObjectStatus, KnowledgeSourceStatus } from '@/api/knowledgeSources'
import type { TenantStatus } from '@/api/tenants'
import type { WikiStatus } from '@/api/wiki'
import type { AiRecommendationFeedbackValue, AiRecommendationStatus } from '@/api/workflowCopilot'

export type StatusVariant = 'success' | 'warning' | 'destructive' | 'info' | 'muted'

export interface StatusInfo {
  variant: StatusVariant
  label: string
}

// Mỗi union type trong src/api có 1 Record riêng để vue-tsc bắt thiếu/thừa case khi API đổi.
// Quy tắc map (kế hoạch mục 3.3): success = Approved/Indexed/Published/Correct/Resolved/pass;
// warning = PendingReview/Draft/InReview; destructive = Rejected/Incorrect/Failed;
// info = New/Processing; muted = Archived/Disabled/None.
const documentStatusMap: Record<DocumentStatus, StatusInfo> = {
  PendingReview: { variant: 'warning', label: 'Chờ duyệt' },
  Approved: { variant: 'success', label: 'Đã duyệt' },
  Rejected: { variant: 'destructive', label: 'Từ chối' },
  Archived: { variant: 'muted', label: 'Đã lưu trữ' },
  Deleted: { variant: 'muted', label: 'Đã xóa' },
}

const documentVersionStatusMap: Record<DocumentVersionStatus, StatusInfo> = {
  PendingReview: { variant: 'warning', label: 'Chờ duyệt' },
  Approved: { variant: 'success', label: 'Đã duyệt' },
  Rejected: { variant: 'destructive', label: 'Từ chối' },
  Processing: { variant: 'info', label: 'Đang xử lý' },
  Indexed: { variant: 'success', label: 'Đã lập chỉ mục' },
  ProcessingFailed: { variant: 'destructive', label: 'Xử lý thất bại' },
}

const wikiStatusMap: Record<WikiStatus, StatusInfo> = {
  Draft: { variant: 'warning', label: 'Bản nháp' },
  Published: { variant: 'success', label: 'Đã xuất bản' },
  Rejected: { variant: 'destructive', label: 'Từ chối' },
  Archived: { variant: 'muted', label: 'Đã lưu trữ' },
}

// Lưu ý: confidence từ API là chữ thường
const aiConfidenceMap: Record<AiConfidence, StatusInfo> = {
  high: { variant: 'success', label: 'Độ tin cậy cao' },
  medium: { variant: 'warning', label: 'Độ tin cậy trung bình' },
  low: { variant: 'destructive', label: 'Độ tin cậy thấp' },
}

const feedbackValueMap: Record<AiFeedbackValue, StatusInfo> = {
  Correct: { variant: 'success', label: 'Đúng' },
  Incorrect: { variant: 'destructive', label: 'Sai' },
}

const feedbackReviewStatusMap: Record<FeedbackReviewStatus, StatusInfo> = {
  New: { variant: 'info', label: 'Mới' },
  InReview: { variant: 'warning', label: 'Đang xem xét' },
  Resolved: { variant: 'success', label: 'Đã xử lý' },
}

const qualityIssueStatusMap: Record<AiQualityIssueStatus, StatusInfo> = {
  New: { variant: 'info', label: 'Mới' },
  Classified: { variant: 'info', label: 'Đã phân loại' },
  InReview: { variant: 'warning', label: 'Đang xem xét' },
  Resolved: { variant: 'success', label: 'Đã xử lý' },
}

const correctionStatusMap: Record<KnowledgeCorrectionStatus, StatusInfo> = {
  Draft: { variant: 'warning', label: 'Bản nháp' },
  Approved: { variant: 'success', label: 'Đã duyệt' },
  Rejected: { variant: 'destructive', label: 'Từ chối' },
}

const tenantStatusMap: Record<TenantStatus, StatusInfo> = {
  Active: { variant: 'success', label: 'Hoạt động' },
  Suspended: { variant: 'warning', label: 'Tạm ngưng' },
  Archived: { variant: 'muted', label: 'Đã lưu trữ' },
}

const applicationStatusMap: Record<ApplicationStatus, StatusInfo> = {
  Active: { variant: 'success', label: 'Hoạt động' },
  Disabled: { variant: 'muted', label: 'Vô hiệu' },
  Archived: { variant: 'muted', label: 'Đã lưu trữ' },
}

const integrationStatusMap: Record<IntegrationConnectionStatus, StatusInfo> = {
  Active: { variant: 'success', label: 'Hoạt động' },
  Disabled: { variant: 'muted', label: 'Vô hiệu' },
  Archived: { variant: 'muted', label: 'Đã lưu trữ' },
}

const knowledgeSourceStatusMap: Record<KnowledgeSourceStatus, StatusInfo> = {
  Active: { variant: 'success', label: 'Hoạt động' },
  Syncing: { variant: 'info', label: 'Đang đồng bộ' },
  Failed: { variant: 'destructive', label: 'Thất bại' },
  Disabled: { variant: 'muted', label: 'Vô hiệu' },
  Archived: { variant: 'muted', label: 'Đã lưu trữ' },
}

const externalObjectStatusMap: Record<ExternalObjectStatus, StatusInfo> = {
  Active: { variant: 'success', label: 'Hoạt động' },
  Deleted: { variant: 'muted', label: 'Đã xóa' },
  Archived: { variant: 'muted', label: 'Đã lưu trữ' },
}

const actionRequestStatusMap: Record<AiActionRequestStatus, StatusInfo> = {
  Draft: { variant: 'warning', label: 'Bản nháp' },
  PendingApproval: { variant: 'warning', label: 'Chờ phê duyệt' },
  Approved: { variant: 'success', label: 'Đã duyệt' },
  Rejected: { variant: 'destructive', label: 'Từ chối' },
  Executing: { variant: 'info', label: 'Đang thực thi' },
  Succeeded: { variant: 'success', label: 'Thành công' },
  Failed: { variant: 'destructive', label: 'Thất bại' },
  Cancelled: { variant: 'muted', label: 'Đã hủy' },
}

const recommendationStatusMap: Record<AiRecommendationStatus, StatusInfo> = {
  Ready: { variant: 'info', label: 'Sẵn sàng' },
  Dismissed: { variant: 'muted', label: 'Đã bỏ qua' },
  FeedbackReceived: { variant: 'success', label: 'Đã nhận phản hồi' },
}

const recommendationFeedbackMap: Record<AiRecommendationFeedbackValue, StatusInfo> = {
  Helpful: { variant: 'success', label: 'Hữu ích' },
  NotHelpful: { variant: 'destructive', label: 'Không hữu ích' },
  NeedsReview: { variant: 'warning', label: 'Cần xem lại' },
}

// Gộp mọi map thành 1 lookup — các key trùng nhau (Approved, Rejected...) đều map cùng variant/label
const combinedMap: Record<string, StatusInfo> = {
  ...documentStatusMap,
  ...documentVersionStatusMap,
  ...wikiStatusMap,
  ...aiConfidenceMap,
  ...feedbackValueMap,
  ...feedbackReviewStatusMap,
  ...qualityIssueStatusMap,
  ...correctionStatusMap,
  ...tenantStatusMap,
  ...applicationStatusMap,
  ...integrationStatusMap,
  ...knowledgeSourceStatusMap,
  ...externalObjectStatusMap,
  ...actionRequestStatusMap,
  ...recommendationStatusMap,
  ...recommendationFeedbackMap,
}

export function statusInfo(status: string): StatusInfo {
  return combinedMap[status] ?? { variant: 'muted', label: status }
}

export function statusVariant(status: string): StatusVariant {
  return statusInfo(status).variant
}

export function statusLabel(status: string): string {
  return statusInfo(status).label
}
