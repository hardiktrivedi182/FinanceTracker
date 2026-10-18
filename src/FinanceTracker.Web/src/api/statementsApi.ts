import type { StatementSummaryDto, StatementUploadResultDto } from '../types/statement'

function getXsrfToken(): string | null {
  const match = document.cookie.match(/(^|;)\s*XSRF-TOKEN\s*=\s*([^;]+)/)
  return match ? decodeURIComponent(match[2]) : null
}

export async function ensureXsrfToken(): Promise<string | null> {
  let token = getXsrfToken()
  if (!token) {
    try {
      const res = await fetch('/api/auth/csrf-token', { credentials: 'include' })
      if (res.ok) {
        const data = await res.json()
        token = data.token
      }
    } catch {
      // Ignored: Best effort retrieval
    }
  }
  return token
}

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
