import { useEffect, useState } from 'react'
import './App.css'
import { AccountManager } from './components/AccountManager'

function App() {
  const [apiStatus, setApiStatus] = useState<string>('Checking...')

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

      <main>
        <AccountManager />
      </main>
    </div>
  )
}

export default App
