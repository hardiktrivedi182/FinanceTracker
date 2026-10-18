import { useEffect, useState } from 'react'
import './App.css'
import { AccountManager } from './components/AccountManager'
import { StatementUploader } from './components/StatementUploader'

function App() {
  const [apiStatus, setApiStatus] = useState<string>('Checking...')
  const [activeTab, setActiveTab] = useState<'accounts' | 'statements'>('accounts')

  useEffect(() => {
    fetch('/api/health')
      .then((res) => (res.ok ? res.text() : `HTTP ${res.status}`))
      .then((data) => setApiStatus(data))
      .catch((err: Error) => setApiStatus(`Offline (${err.message})`))
  }, [])

  return (
    <div style={{ maxWidth: 960, margin: '2rem auto', padding: '1rem', fontFamily: 'system-ui, -apple-system, sans-serif' }}>
      <header style={{ borderBottom: '1px solid #e2e8f0', paddingBottom: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1 style={{ margin: 0, fontSize: '1.75rem' }}>FinanceTracker</h1>
          <p style={{ margin: '4px 0 0 0', color: '#64748b' }}>Private Household Finance &amp; Net Worth Tracker</p>
        </div>
        <div style={{ fontSize: '0.85rem', color: '#64748b', textAlign: 'right' }}>
          <div>Backend API: <span data-testid="api-status" style={{ fontWeight: 600, color: apiStatus === 'Healthy' ? '#16a34a' : '#d97706' }}>{apiStatus}</span></div>
        </div>
      </header>

      <nav style={{ display: 'flex', gap: '0.5rem', marginTop: '1rem', borderBottom: '1px solid #e2e8f0', paddingBottom: '0.5rem' }}>
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

      <main>
        {activeTab === 'accounts' ? <AccountManager /> : <StatementUploader />}
      </main>
    </div>
  )
}

export default App
