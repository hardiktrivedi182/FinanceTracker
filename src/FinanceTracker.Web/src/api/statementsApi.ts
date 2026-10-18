import type { StatementSummaryDto, StatementUploadResultDto } from '../types/statement'
import { ensureXsrfToken } from './authApi'
export { ensureXsrfToken }

export async function uploadStatement(file: File, accountId?: string): Promise<StatementUploadResultDto> {
  const token = await ensureXsrfToken()
  const formData = new FormData()
  formData.append('file', file)

  const headers: Record<string, string> = {}
  if (token) {
    headers['X-XSRF-Token'] = token
  }

  const query = accountId ? `?accountId=${encodeURIComponent(accountId)}` : ''
  const res = await fetch(`/api/statements/upload${query}`, {
    method: 'POST',
    credentials: 'include',
    headers,
    body: formData,
  })

  if (!res.ok) {
    const errorBody = await res.json().catch(() => ({}))
    throw new Error(errorBody.error || errorBody.detail || `Upload failed (${res.status})`)
  }

  return res.json()
}

export async function fetchStatements(): Promise<StatementSummaryDto[]> {
  const res = await fetch('/api/statements', {
    credentials: 'include',
  })
  if (!res.ok) {
    throw new Error(`Failed to load statements (${res.status})`)
  }
  return res.json()
}
