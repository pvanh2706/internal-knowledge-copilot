export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000/api'
export const tenantCodeStorageKey = 'ikc.tenantCode'

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

export async function apiRequest<T>(path: string, options: RequestInit = {}, token?: string | null): Promise<T> {
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

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
  })

  if (!response.ok) {
    const errorBody = await response.json().catch(() => undefined)
    throw new ApiError(errorBody?.message ?? 'Không thể xử lý yêu cầu.', response.status)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}
