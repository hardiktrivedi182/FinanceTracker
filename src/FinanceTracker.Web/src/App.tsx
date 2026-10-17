import { useEffect, useState } from 'react'
import './App.css'

function App() {
  const [apiStatus, setApiStatus] = useState<string>('Checking...')

  useEffect(() => {
    fetch('/api/health')
      .then((res) => (res.ok ? res.text() : `HTTP ${res.status}`))
      .then((data) => setApiStatus(data))
      .catch((err: Error) => setApiStatus(`Offline (${err.message})`))
  }, [])

  return (
    <div style={{ maxWidth: 800, margin: '2rem auto', padding: '1rem', fontFamily: 'system-ui, sans-serif' }}>
      <header>
        <h1>FinanceTracker</h1>
        <p>Private Household Finance Tracker</p>
      </header>
      <main style={{ marginTop: '1.5rem' }}>
        <section style={{ padding: '1rem', border: '1px solid #ddd', borderRadius: 8, background: '#fafafa' }}>
          <h2>System Status</h2>
          <p>
            <strong>Backend API:</strong> <span data-testid="api-status">{apiStatus}</span>
          </p>
        </section>
      </main>
    </div>
  )
}

export default App
