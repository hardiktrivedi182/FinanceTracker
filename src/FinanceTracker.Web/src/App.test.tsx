import { render, screen, waitFor, fireEvent } from '@testing-library/react'
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
    expect(screen.getByTestId('api-status')).toHaveTextContent('Checking...')
    expect(screen.getByText(/Two-User Household/i)).toBeInTheDocument()
  })

  it('renders healthy status and accounts when API returns OK', async () => {
    const mockAccounts = [
      {
        id: 'acc-1',
        householdId: 'hh-1',
        name: 'Main Checking',
        accountType: 'Checking',
        ownershipType: 'Joint',
        ownerUserId: null,
        currencyCode: 'USD',
        isActive: true,
        createdByUserId: 'partner-1',
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        latestBalance: 5200.5,
        latestValuationDate: '2026-03-31',
        isStale: false,
      },
      {
        id: 'acc-2',
        householdId: 'hh-1',
        name: 'Partner 1 EPF',
        accountType: 'Epf',
        ownershipType: 'Individual',
        ownerUserId: 'partner-1',
        currencyCode: 'INR',
        isActive: true,
        createdByUserId: 'partner-1',
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        latestBalance: 850000,
        latestValuationDate: '2026-03-31',
        isStale: false,
      },
    ]

    vi.mocked(fetch).mockImplementation((url: RequestInfo | URL) => {
      const urlStr = url.toString()
      if (urlStr.includes('/api/health')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () => Promise.resolve('Healthy'),
        } as Response)
      }
      if (urlStr.includes('/api/accounts')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(mockAccounts),
        } as Response)
      }
      return Promise.reject(new Error('Unknown url'))
    })

    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('api-status')).toHaveTextContent('Healthy')
    })

    await waitFor(() => {
      expect(screen.getByText('Main Checking')).toBeInTheDocument()
      expect(screen.getByText('Partner 1 EPF')).toBeInTheDocument()
      expect(screen.getByText('Epf')).toBeInTheDocument()
      expect(screen.getByText('Joint')).toBeInTheDocument()
    })
  })

  it('opens add account modal when Add Account button is clicked', async () => {
    vi.mocked(fetch).mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve([]),
      text: () => Promise.resolve('Healthy'),
    } as Response)

    render(<App />)

    const addBtn = await screen.findByTestId('add-account-btn')
    fireEvent.click(addBtn)

    expect(screen.getByText('Add Financial Account / Asset')).toBeInTheDocument()
    expect(screen.getByText('Asset / Liability Type:')).toBeInTheDocument()
  })

  it('displays a warning banner on net worth card when an account has a missing valuation', async () => {
    const mockAccounts = [
      {
        id: 'acc-1',
        householdId: 'hh-1',
        name: 'Unvalued Account',
        accountType: 'Checking',
        ownershipType: 'Joint',
        ownerUserId: null,
        currencyCode: 'USD',
        isActive: true,
        createdByUserId: 'partner-1',
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        latestBalance: null,
        latestValuationDate: null,
        isStale: false,
      },
    ]

    const mockNetWorth = {
      asOfDate: '2026-10-18',
      generatedAt: '2026-10-18T00:00:00Z',
      stalenessThresholdDays: 90,
      currencies: [
        {
          currencyCode: 'USD',
          totalAssets: 0,
          totalLiabilities: 0,
          netWorth: 0,
          ownershipBreakdown: {
            jointTotal: 0,
            partner1IndividualTotal: 0,
            partner2IndividualTotal: 0,
            partner1TotalShare: 0,
            partner2TotalShare: 0,
          },
          assetAllocation: [],
          accounts: [
            {
              accountId: 'acc-1',
              accountName: 'Unvalued Account',
              accountType: 'Checking',
              ownershipType: 'Joint',
              ownerUserId: null,
              isLiability: false,
              valuationDate: null,
              amount: 0,
              isStale: false,
              hasMissingValuation: true,
              sourceType: null,
            },
          ],
        },
      ],
    }

    vi.mocked(fetch).mockImplementation((url: RequestInfo | URL) => {
      const urlStr = url.toString()
      if (urlStr.includes('/api/health')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () => Promise.resolve('Healthy'),
        } as Response)
      }
      if (urlStr.includes('/api/accounts')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(mockAccounts),
        } as Response)
      }
      if (urlStr.includes('/api/household/net-worth')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(mockNetWorth),
        } as Response)
      }
      return Promise.reject(new Error('Unknown url'))
    })

    render(<App />)

    await waitFor(() => {
      expect(screen.getByText(/1 account\(s\) have no valuation on or before 2026-10-18/i)).toBeInTheDocument()
    })
  })
})

