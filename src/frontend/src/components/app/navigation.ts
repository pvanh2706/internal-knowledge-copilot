import type { FunctionalComponent } from 'vue'
import {
  AppWindowIcon,
  BookOpenIcon,
  Building2Icon,
  ChartColumnIcon,
  ClipboardCheckIcon,
  DatabaseIcon,
  FileTextIcon,
  FolderTreeIcon,
  LayoutDashboardIcon,
  LibraryIcon,
  LightbulbIcon,
  ListChecksIcon,
  MessageSquareIcon,
  PlugIcon,
  ScrollTextIcon,
  SearchIcon,
  SettingsIcon,
  SparklesIcon,
  Trash2Icon,
  UsersIcon,
  UsersRoundIcon,
} from '@lucide/vue'

export interface NavItem {
  label: string
  to: string
  icon: FunctionalComponent
  /** Dòng phụ dưới tiêu đề trang trên topbar (B1) */
  description?: string
  /** Khai báo role theo TỪNG item, khớp đúng meta của router (không nhóm cứng theo role) */
  requiresAdmin?: boolean
  requiresReviewer?: boolean
}

export interface NavGroup {
  label: string
  items: NavItem[]
}

// Menu sidebar config-driven — nguồn sự thật duy nhất cho AppSidebar + tên trang trên topbar
export const navGroups: NavGroup[] = [
  {
    label: 'Làm việc',
    items: [
      { label: 'Tổng quan', to: '/', icon: LayoutDashboardIcon },
      { label: 'AI Workspace', to: '/ai', icon: SparklesIcon, description: 'Tra cứu và nhận câu trả lời có dẫn chứng' },
      { label: 'Tài liệu', to: '/documents', icon: FileTextIcon, description: 'Quản lý tài liệu và phiên bản' },
      { label: 'Wiki', to: '/wiki', icon: BookOpenIcon, requiresReviewer: true },
    ],
  },
  {
    label: 'Chất lượng',
    items: [
      { label: 'Duyệt tài liệu', to: '/review', icon: ClipboardCheckIcon, requiresReviewer: true, description: 'Reviewer · kiểm tra phiên bản chờ duyệt' },
      { label: 'Phản hồi', to: '/feedback', icon: MessageSquareIcon, requiresReviewer: true },
      { label: 'Đánh giá', to: '/evaluation', icon: ChartColumnIcon, requiresReviewer: true },
      { label: 'Giải thích truy xuất', to: '/retrieval-explain', icon: SearchIcon, requiresReviewer: true },
      { label: 'Chỉ mục tri thức', to: '/knowledge-index', icon: DatabaseIcon, requiresReviewer: true },
      { label: 'Nguồn tri thức', to: '/knowledge-sources', icon: LibraryIcon, requiresReviewer: true },
      { label: 'Thư mục', to: '/admin/folders', icon: FolderTreeIcon, requiresReviewer: true },
    ],
  },
  {
    label: 'Quản trị',
    items: [
      { label: 'Người dùng', to: '/admin/users', icon: UsersIcon, requiresAdmin: true },
      { label: 'Team', to: '/admin/teams', icon: UsersRoundIcon, requiresAdmin: true },
      { label: 'Cấu hình AI', to: '/admin/ai-settings', icon: SettingsIcon, requiresAdmin: true },
      { label: 'Nhật ký hệ thống', to: '/admin/audit-logs', icon: ScrollTextIcon, requiresAdmin: true },
      { label: 'Reset dữ liệu', to: '/admin/data-reset', icon: Trash2Icon, requiresAdmin: true },
    ],
  },
  {
    label: 'Nền tảng',
    items: [
      { label: 'Gợi ý quy trình', to: '/workflow/recommendations', icon: LightbulbIcon },
      { label: 'Hàng đợi hành động', to: '/workflow/actions', icon: ListChecksIcon },
      { label: 'Tenant', to: '/admin/tenants', icon: Building2Icon, requiresAdmin: true },
      { label: 'Application', to: '/admin/applications', icon: AppWindowIcon, requiresAdmin: true },
      { label: 'Tích hợp', to: '/admin/integrations', icon: PlugIcon, requiresAdmin: true },
    ],
  },
]

export function findNavItemByPath(path: string): NavItem | undefined {
  for (const group of navGroups) {
    const item = group.items.find((candidate) => candidate.to === path)
    if (item) {
      return item
    }
  }
  return undefined
}
