export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000/api'
export const tenantCodeStorageKey = 'ikc.tenantCode'

// Các key localStorage của phiên đăng nhập — dùng chung với stores/auth.ts để tránh lệch tên key
export const accessTokenStorageKey = 'ikc.accessToken'
export const userStorageKey = 'ikc.user'

// Đường dẫn trang đăng nhập trên router (xem src/router/index.ts)
const loginPagePath = '/login'

// Đường dẫn API đăng nhập — 401 từ endpoint này nghĩa là sai thông tin đăng nhập, không phải hết hạn phiên
const loginApiPath = '/auth/login'

export function getTenantCode() {
  return localStorage.getItem(tenantCodeStorageKey) || 'default'
}

export function setTenantCode(value: string) {
  const cleaned = value.trim().toLowerCase()
  localStorage.setItem(tenantCodeStorageKey, cleaned || 'default')
}

export class ApiError extends Error {
  public readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

// Kết quả tải file nhị phân: blob kèm tên file đọc từ header Content-Disposition (nếu có)
export interface BlobResult {
  blob: Blob
  fileName: string | null
}

// Gắn các header chung (Content-Type, Authorization, X-Tenant-Code) cho mọi request
function buildHeaders(options: RequestInit, token?: string | null): Headers {
  const headers = new Headers(options.headers)
  if (!headers.has('Content-Type') && options.body && !(options.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }

  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const tenantCode = getTenantCode()
  if (tenantCode && !headers.has('X-Tenant-Code')) {
    headers.set('X-Tenant-Code', tenantCode)
  }

  return headers
}

// Xử lý 401 tập trung: xóa dữ liệu phiên rồi đưa người dùng về trang đăng nhập.
// Giữ lại tenant code để lần đăng nhập sau không phải nhập lại.
// Dùng window.location.href thay vì router để tránh import vòng (router -> store -> http).
function handleSessionExpired(path: string) {
  if (path.startsWith(loginApiPath)) {
    return
  }

  localStorage.removeItem(accessTokenStorageKey)
  localStorage.removeItem(userStorageKey)

  // Nếu đang ở sẵn trang đăng nhập thì không chuyển hướng nữa để tránh vòng lặp reload
  if (window.location.pathname !== loginPagePath) {
    window.location.href = `${loginPagePath}?expired=1`
  }
}

// Đọc message lỗi từ body JSON (nếu có) và tạo ApiError tương ứng
async function buildApiError(response: Response): Promise<ApiError> {
  const errorBody = (await response.json().catch(() => undefined)) as { message?: string } | undefined
  return new ApiError(errorBody?.message ?? 'Không thể xử lý yêu cầu.', response.status)
}

async function throwOnHttpError(response: Response, path: string): Promise<void> {
  if (response.ok) {
    return
  }

  if (response.status === 401) {
    handleSessionExpired(path)
  }

  throw await buildApiError(response)
}

export async function apiRequest<T>(path: string, options: RequestInit = {}, token?: string | null): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: buildHeaders(options, token),
  })

  await throwOnHttpError(response, path)

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

// Tải dữ liệu nhị phân qua client chung: gắn đủ Authorization + X-Tenant-Code,
// lỗi thì ném ApiError giống apiRequest
export async function apiRequestBlob(path: string, token?: string | null): Promise<BlobResult> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: buildHeaders({}, token),
  })

  await throwOnHttpError(response, path)

  const disposition = response.headers.get('content-disposition') ?? ''
  const rawFileName = disposition.match(/filename="?([^";]+)"?/i)?.[1]

  return {
    blob: await response.blob(),
    fileName: rawFileName ? decodeURIComponent(rawFileName) : null,
  }
}
