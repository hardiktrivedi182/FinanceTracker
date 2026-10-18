export interface CurrentUserDto {
  userId: string | null
  email: string | null
  displayName?: string | null
  isAuthenticated: boolean
  householdId: string | null
  householdRole: string | null
}

export function getXsrfToken(): string | null {
  if (typeof document === 'undefined') return null
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

export async function fetchCurrentUser(): Promise<CurrentUserDto> {
  const res = await fetch('/api/auth/me', {
    credentials: 'include',
  })
  if (!res.ok) {
    throw new Error(`Failed to check authentication status (${res.status})`)
  }
  return res.json()
}

export async function login(email: string, password: string): Promise<CurrentUserDto> {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ email, password }),
  })
  if (!res.ok) {
    const errorBody = await res.json().catch(() => ({}))
    throw new Error(errorBody.message || `Login failed (${res.status})`)
  }
  await ensureXsrfToken()
  return fetchCurrentUser()
}

export async function devLogin(userId: string): Promise<CurrentUserDto> {
  const res = await fetch('/api/auth/dev-login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ userId }),
  })
  if (!res.ok) {
    const errorBody = await res.json().catch(() => ({}))
    throw new Error(errorBody.message || `Dev login failed (${res.status})`)
  }
  await ensureXsrfToken()
  return fetchCurrentUser()
}

export async function logout(): Promise<void> {
  const res = await fetch('/api/auth/logout', {
    method: 'POST',
    credentials: 'include',
  })
  if (!res.ok) {
    throw new Error(`Logout failed (${res.status})`)
  }
}
