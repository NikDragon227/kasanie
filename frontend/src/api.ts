export class ApiError extends Error {
  constructor(public status: number, public body: Record<string, unknown>) {
    super((body.message ?? body.detail ?? body.title ?? `Ошибка ${status}`) as string)
  }
}

let csrfToken: string | null = null
export function resetCsrf() { csrfToken = null }

async function ensureCsrf() {
  if (csrfToken) return csrfToken
  const response = await fetch('/api/auth/csrf', { credentials: 'include' })
  if (!response.ok) throw new Error('Не удалось установить защищённую сессию')
  csrfToken = (await response.json() as { token: string }).token
  return csrfToken
}

export async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  const method = options.method?.toUpperCase() ?? 'GET'
  const headers = new Headers(options.headers)
  if (options.body && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json')
  if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) headers.set('X-CSRF-TOKEN', await ensureCsrf())
  const response = await fetch(path, { ...options, headers, credentials: 'include' })
  if (response.status === 204) return undefined as T
  const body = await response.json().catch(() => ({})) as Record<string, unknown>
  if (!response.ok) {
    if (response.status === 400 && body.title === 'Недействительный CSRF-токен') csrfToken = null
    throw new ApiError(response.status, body)
  }
  return body as T
}

export const post = <T>(path: string, value?: unknown) => api<T>(path, { method: 'POST', body: value === undefined ? undefined : JSON.stringify(value) })
export const put = <T>(path: string, value: unknown) => api<T>(path, { method: 'PUT', body: JSON.stringify(value) })
export const remove = <T>(path: string) => api<T>(path, { method: 'DELETE' })
