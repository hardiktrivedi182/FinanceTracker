import { useState, type FormEvent } from 'react'
import { login, type CurrentUserDto } from '../api/authApi'

interface LoginProps {
  onLoginSuccess: (user: CurrentUserDto) => void
}

export function Login({ onLoginSuccess }: LoginProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const isDev = Boolean(import.meta.env.DEV)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setLoading(true)

    try {
      const user = await login(email.trim(), password)
      onLoginSuccess(user)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Login failed')
    } finally {
      setLoading(false)
    }
  }

  const handleQuickLogin = async (partnerEmail: string) => {
    setError(null)
    setLoading(true)
    setEmail(partnerEmail)
    setPassword('Password123!')

    try {
      const user = await login(partnerEmail, 'Password123!')
      onLoginSuccess(user)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Local login failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div
      data-testid="login-container"
      style={{
        maxWidth: 440,
        margin: '3rem auto',
        padding: '2rem',
        background: '#fff',
        borderRadius: 8,
        boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -2px rgba(0, 0, 0, 0.1)',
        border: '1px solid #e2e8f0',
      }}
    >
      <div style={{ textAlign: 'center', marginBottom: '1.5rem' }}>
        <h2 style={{ margin: '0 0 0.5rem 0', fontSize: '1.5rem', color: '#0f172a' }}>Sign In</h2>
        <p style={{ margin: 0, color: '#64748b', fontSize: '0.9rem' }}>
          FinanceTracker Household Security Portal
        </p>
      </div>

      {error && (
        <div
          data-testid="login-error"
          style={{
            background: '#fee2e2',
            color: '#b91c1c',
            padding: '0.75rem 1rem',
            borderRadius: 6,
            marginBottom: '1rem',
            fontSize: '0.875rem',
          }}
        >
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: '1rem' }}>
          <label
            htmlFor="login-email"
            style={{ display: 'block', marginBottom: 4, fontWeight: 500, fontSize: '0.875rem' }}
          >
            Email Address
          </label>
          <input
            id="login-email"
            data-testid="login-email"
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            disabled={loading}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: 6,
              border: '1px solid #cbd5e1',
              boxSizing: 'border-box',
              fontSize: '0.95rem',
            }}
          />
        </div>

        <div style={{ marginBottom: '1.25rem' }}>
          <label
            htmlFor="login-password"
            style={{ display: 'block', marginBottom: 4, fontWeight: 500, fontSize: '0.875rem' }}
          >
            Password
          </label>
          <input
            id="login-password"
            data-testid="login-password"
            type="password"
            required
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={loading}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: 6,
              border: '1px solid #cbd5e1',
              boxSizing: 'border-box',
              fontSize: '0.95rem',
            }}
          />
        </div>

        <button
          type="submit"
          data-testid="login-submit"
          disabled={loading}
          style={{
            width: '100%',
            background: loading ? '#94a3b8' : '#2563eb',
            color: '#fff',
            border: 'none',
            borderRadius: 6,
            padding: '0.65rem 1rem',
            fontWeight: 600,
            fontSize: '0.95rem',
            cursor: loading ? 'not-allowed' : 'pointer',
            transition: 'background 0.2s',
          }}
        >
          {loading ? 'Signing in...' : 'Sign In'}
        </button>
      </form>

      {/* Local Development Test Identities - strictly omitted in production */}
      {isDev && (
        <div
          data-testid="dev-identity-section"
          style={{
            marginTop: '2rem',
            paddingTop: '1.5rem',
            borderTop: '1px dashed #cbd5e1',
          }}
        >
          <div
            style={{
              fontSize: '0.75rem',
              fontWeight: 600,
              textTransform: 'uppercase',
              letterSpacing: '0.05em',
              color: '#64748b',
              marginBottom: '0.75rem',
              textAlign: 'center',
            }}
          >
            Local Development Identities
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            <button
              type="button"
              data-testid="login-as-partner-1"
              disabled={loading}
              onClick={() => handleQuickLogin('partner1@household.local')}
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                padding: '0.5rem 0.75rem',
                borderRadius: 6,
                border: '1px solid #93c5fd',
                background: '#eff6ff',
                color: '#1e40af',
                cursor: loading ? 'not-allowed' : 'pointer',
                fontSize: '0.85rem',
                fontWeight: 500,
              }}
            >
              <span>Partner 1 (Admin)</span>
              <span style={{ fontSize: '0.75rem', color: '#3b82f6' }}>partner1@household.local</span>
            </button>
            <button
              type="button"
              data-testid="login-as-partner-2"
              disabled={loading}
              onClick={() => handleQuickLogin('partner2@household.local')}
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                padding: '0.5rem 0.75rem',
                borderRadius: 6,
                border: '1px solid #cbd5e1',
                background: '#f8fafc',
                color: '#334155',
                cursor: loading ? 'not-allowed' : 'pointer',
                fontSize: '0.85rem',
                fontWeight: 500,
              }}
            >
              <span>Partner 2 (Member)</span>
              <span style={{ fontSize: '0.75rem', color: '#64748b' }}>partner2@household.local</span>
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
