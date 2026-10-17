import { render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

describe('App Component', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders application header and initial checking status', () => {
    vi.mocked(fetch).mockReturnValue(new Promise(() => {})) // never resolves

    render(<App />)

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('FinanceTracker')
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('System Status')
    expect(screen.getByTestId('api-status')).toHaveTextContent('Checking...')
  })

  it('renders healthy status when API returns OK', async () => {
    vi.mocked(fetch).mockResolvedValue({
      ok: true,
      status: 200,
      text: () => Promise.resolve('Healthy'),
    } as Response)

    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('api-status')).toHaveTextContent('Healthy')
    })
  })

  it('renders error status when API fetch fails', async () => {
    vi.mocked(fetch).mockRejectedValue(new Error('Network error'))

    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('api-status')).toHaveTextContent('Offline (Network error)')
    })
  })
})
