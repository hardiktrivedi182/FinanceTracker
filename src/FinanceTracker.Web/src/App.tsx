import { useEffect, useState } from 'react'
import './App.css'
import { AccountManager } from './components/AccountManager'
import { StatementUploader } from './components/StatementUploader'
import { Login } from './components/Login'
import { fetchCurrentUser, logout, devLogin, type CurrentUserDto } from './api/authApi'

function App() {
  const [apiStatus, setApiStatus] = useState<string>('Checking...')
  const [activeTab, setActiveTab] = useState<'accounts' | 'statements'>('accounts')
  const [currentUser, setCurrentUser] = useState<CurrentUserDto | null>(null)
  const [loadingAuth, setLoadingAuth] = useState<boolean>(true)

  useEffect(() => {
    fetch('/api/health')
      .then((res) => (res.ok ? res.text() : `HTTP ${res.status}`))
      .then((data) => setApiStatus(data))
      .catch((err: Error) => setApiStatus(`Offline (${err.message})`))

    fetchCurrentUser()
      .then((user) => {
        if (user.isAuthenticated) {
          setCurrentUser(user)
        } else {
          setCurrentUser(null)
        }
      })
      .catch(() => {
        setCurrentUser(null)
      })
      .finally(() => {
        setLoadingAuth(false)
      })
  }, [])

  const handleLogout = async () => {
    try {
      await logout()
    } finally {
      setCurrentUser(null)
    }
  }

  const handleDevSwitch = async (userId: string) => {
    try {
      const user = await devLogin(userId)
      setCurrentUser(user)
    } catch {
      // Ignore in dev
    }
  }

  return (
    <div style={{ maxWidth: 960, margin: '2rem auto', padding: '1rem', fontFamily: 'system-ui, -apple-system, sans-serif' }}>
      <header style={{ borderBottom: '1px solid #e2e8f0', paddingBottom: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1 style={{ margin: 0, fontSize: '1.75rem' }}>FinanceTracker</h1>
          <p style={{ margin: '4px 0 0 0', color: '#64748b' }}>Private Household Finance &amp; Net Worth Tracker</p>
        </div>
        <div style={{ fontSize: '0.85rem', color: '#64748b', textAlign: 'right' }}>
          <div>Backend API: <span data-testid="api-status" style={{ fontWeight: 600, color: apiStatus === 'Healthy' ? '#16a34a' : '#d97706' }}>{apiStatus}</span></div>
          {currentUser?.isAuthenticated && (
            <div style={{ marginTop: 4 }}>
              <span data-testid="user-identity" style={{ fontWeight: 500, color: '#334155' }}>
                {currentUser.displayName || currentUser.userId} ({currentUser.householdRole || 'Member'})
              </span>
            </div>
          )}
        </div>
      </header>

      {loadingAuth ? (
        <div data-testid="auth-loading" style={{ textAlign: 'center', padding: '3rem', color: '#64748b' }}>
          Checking authentication session...
        </div>
      ) : !currentUser?.isAuthenticated ? (
        <Login onLoginSuccess={(user) => setCurrentUser(user)} />
      ) : (
        <>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '1rem', borderBottom: '1px solid #e2e8f0', paddingBottom: '0.5rem' }}>
            <nav style={{ display: 'flex', gap: '0.5rem' }}>
              <button
                type="button"
                data-testid="tab-accounts"
                onClick={() => setActiveTab('accounts')}
                style={{
                  background: activeTab === 'accounts' ? '#2563eb' : '#f1f5f9',
                  color: activeTab === 'accounts' ? '#fff' : '#475569',
                  border: 'none',
                  borderRadius: 6,
                  padding: '0.5rem 1rem',
                  cursor: 'pointer',
                  fontWeight: 500,
                }}
              >
                Accounts &amp; Net Worth
              </button>
              <button
                type="button"
                data-testid="tab-statements"
                onClick={() => setActiveTab('statements')}
                style={{
                  background: activeTab === 'statements' ? '#2563eb' : '#f1f5f9',
                  color: activeTab === 'statements' ? '#fff' : '#475569',
                  border: 'none',
                  borderRadius: 6,
                  padding: '0.5rem 1rem',
                  cursor: 'pointer',
                  fontWeight: 500,
                }}
              >
                Statement Upload &amp; Parsing
              </button>
            </nav>

            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
              {Boolean(import.meta.env.DEV) && (
                <div style={{ display: 'flex', gap: '0.25rem' }}>
                  <button
                    type="button"
                    data-testid="header-switch-partner-1"
                    onClick={() => handleDevSwitch('partner-1')}
                    style={{
                      background: currentUser.userId === 'partner-1' ? '#dbeafe' : '#f8fafc',
                      color: currentUser.userId === 'partner-1' ? '#1e40af' : '#64748b',
                      border: '1px solid #cbd5e1',
                      borderRadius: 4,
                      padding: '2px 6px',
                      fontSize: '0.75rem',
                      cursor: 'pointer',
                    }}
                  >
                    P1
                  </button>
                  <button
                    type="button"
                    data-testid="header-switch-partner-2"
                    onClick={() => handleDevSwitch('partner-2')}
                    style={{
                      background: currentUser.userId === 'partner-2' ? '#dbeafe' : '#f8fafc',
                      color: currentUser.userId === 'partner-2' ? '#1e40af' : '#64748b',
                      border: '1px solid #cbd5e1',
                      borderRadius: 4,
                      padding: '2px 6px',
                      fontSize: '0.75rem',
                      cursor: 'pointer',
                    }}
                  >
                    P2
                  </button>
                </div>
              )}
              <button
                type="button"
                data-testid="logout-btn"
                onClick={handleLogout}
                style={{
                  background: 'transparent',
                  color: '#dc2626',
                  border: '1px solid #fca5a5',
                  borderRadius: 6,
                  padding: '0.4rem 0.8rem',
                  cursor: 'pointer',
                  fontSize: '0.85rem',
                  fontWeight: 500,
                }}
              >
                Sign Out
              </button>
            </div>
          </div>

          <main>
            {activeTab === 'accounts' ? (
              <AccountManager
                key={currentUser.userId || 'partner-1'}
                currentUserId={currentUser.userId || 'partner-1'}
                onUserSwitched={() => {
                  fetchCurrentUser().then(setCurrentUser).catch(() => {})
                }}
              />
            ) : (
              <StatementUploader />
            )}
          </main>
        </>
      )}
    </div>
  )
}

export default App
