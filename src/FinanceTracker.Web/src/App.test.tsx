import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

describe('App Component', () => {
  const mockPartner1User = {
    userId: 'partner-1',
    email: 'partner1@household.local',
    displayName: 'Partner 1',
    isAuthenticated: true,
    householdId: '11111111-1111-1111-1111-111111111111',
    householdRole: 'Admin',
  }

  const mockPartner2User = {
    userId: 'partner-2',
    email: 'partner2@household.local',
    displayName: 'Partner 2',
    isAuthenticated: true,
    householdId: '11111111-1111-1111-1111-111111111111',
    householdRole: 'Member',
  }

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

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders application header and displays login screen when unauthenticated', async () => {
    vi.mocked(fetch).mockImplementation((url: RequestInfo | URL) => {
      const urlStr = url.toString()
      if (urlStr.includes('/api/health')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () => Promise.resolve('Healthy'),
        } as Response)
      }
      if (urlStr.includes('/api/auth/me')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve({ isAuthenticated: false, userId: null }),
        } as Response)
      }
      return Promise.reject(new Error(`Unhandled URL: ${urlStr}`))
    })

    render(<App />)

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('FinanceTracker')
    await waitFor(() => {
      expect(screen.getByTestId('api-status')).toHaveTextContent('Healthy')
    })
    expect(await screen.findByTestId('login-container')).toBeInTheDocument()
    expect(screen.getByTestId('login-email')).toBeInTheDocument()
    expect(screen.getByTestId('login-password')).toBeInTheDocument()
    expect(screen.getByTestId('login-as-partner-1')).toBeInTheDocument()
    expect(screen.getByTestId('login-as-partner-2')).toBeInTheDocument()
  })

  it('renders healthy status and accounts when user is authenticated', async () => {
    vi.mocked(fetch).mockImplementation((url: RequestInfo | URL) => {
      const urlStr = url.toString()
      if (urlStr.includes('/api/health')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () => Promise.resolve('Healthy'),
        } as Response)
      }
      if (urlStr.includes('/api/auth/me')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(mockPartner1User),
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
          json: () => Promise.resolve(null),
        } as Response)
      }
      return Promise.reject(new Error(`Unhandled URL: ${urlStr}`))
    })

    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('api-status')).toHaveTextContent('Healthy')
    })

    await waitFor(() => {
      expect(screen.getByTestId('user-identity')).toHaveTextContent('Partner 1 (Admin)')
      expect(screen.getByText('Main Checking')).toBeInTheDocument()
      expect(screen.getByText('Partner 1 EPF')).toBeInTheDocument()
      expect(screen.getByText('Epf')).toBeInTheDocument()
      expect(screen.getByText('Joint')).toBeInTheDocument()
    })
  })

  it('opens add account modal when Add Account button is clicked by authenticated partner', async () => {
    vi.mocked(fetch).mockImplementation((url: RequestInfo | URL) => {
      const urlStr = url.toString()
      if (urlStr.includes('/api/health')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () => Promise.resolve('Healthy'),
        } as Response)
      }
      if (urlStr.includes('/api/auth/me')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(mockPartner1User),
        } as Response)
      }
      if (urlStr.includes('/api/accounts')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve([]),
        } as Response)
      }
      if (urlStr.includes('/api/household/net-worth')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(null),
        } as Response)
      }
      return Promise.reject(new Error(`Unhandled URL: ${urlStr}`))
    })

    render(<App />)

    const addBtn = await screen.findByTestId('add-account-btn')
    fireEvent.click(addBtn)

    expect(screen.getByText('Add Financial Account / Asset')).toBeInTheDocument()
    expect(screen.getByText('Asset / Liability Type:')).toBeInTheDocument()
  })

  it('displays a warning banner on net worth card when an account has a missing valuation', async () => {
    const mockUnvaluedAccount = [
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
      if (urlStr.includes('/api/auth/me')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(mockPartner1User),
        } as Response)
      }
      if (urlStr.includes('/api/accounts')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(mockUnvaluedAccount),
        } as Response)
      }
      if (urlStr.includes('/api/household/net-worth')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(mockNetWorth),
        } as Response)
      }
      return Promise.reject(new Error(`Unhandled URL: ${urlStr}`))
    })

    render(<App />)

    await waitFor(() => {
      expect(screen.getByText(/1 account\(s\) have no valuation on or before 2026-10-18/i)).toBeInTheDocument()
    })
  })

  it('allows logging in with local development test identity and updates user session', async () => {
    let authState = false
    vi.mocked(fetch).mockImplementation((url: RequestInfo | URL) => {
      const urlStr = url.toString()
      if (urlStr.includes('/api/health')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () => Promise.resolve('Healthy'),
        } as Response)
      }
      if (urlStr.includes('/api/auth/me')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(authState ? mockPartner1User : { isAuthenticated: false }),
        } as Response)
      }
      if (urlStr.includes('/api/auth/login')) {
        authState = true
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve({
            userId: 'partner-1',
            email: 'partner1@household.local',
            householdRole: 'Admin',
            message: 'Signed in successfully.',
          }),
        } as Response)
      }
      if (urlStr.includes('/api/auth/csrf-token')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve({ token: 'mock-csrf' }),
        } as Response)
      }
      if (urlStr.includes('/api/accounts')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve([]),
        } as Response)
      }
      if (urlStr.includes('/api/household/net-worth')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(null),
        } as Response)
      }
      return Promise.reject(new Error(`Unhandled URL: ${urlStr}`))
    })

    render(<App />)

    const partner1Btn = await screen.findByTestId('login-as-partner-1')
    fireEvent.click(partner1Btn)

    await waitFor(() => {
      expect(screen.getByTestId('user-identity')).toHaveTextContent('Partner 1 (Admin)')
      expect(screen.getByTestId('tab-accounts')).toBeInTheDocument()
    })
  })

  it('allows logging out and returns user to login screen', async () => {
    let authState = true
    vi.mocked(fetch).mockImplementation((url: RequestInfo | URL) => {
      const urlStr = url.toString()
      if (urlStr.includes('/api/health')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () => Promise.resolve('Healthy'),
        } as Response)
      }
      if (urlStr.includes('/api/auth/me')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(authState ? mockPartner2User : { isAuthenticated: false }),
        } as Response)
      }
      if (urlStr.includes('/api/auth/logout')) {
        authState = false
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve({ message: 'Signed out.' }),
        } as Response)
      }
      if (urlStr.includes('/api/accounts')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve([]),
        } as Response)
      }
      if (urlStr.includes('/api/household/net-worth')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(null),
        } as Response)
      }
      return Promise.reject(new Error(`Unhandled URL: ${urlStr}`))
    })

    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('user-identity')).toHaveTextContent('Partner 2 (Member)')
    })

    const logoutBtn = screen.getByTestId('logout-btn')
    fireEvent.click(logoutBtn)

    await waitFor(() => {
      expect(screen.getByTestId('login-container')).toBeInTheDocument()
      expect(screen.queryByTestId('logout-btn')).not.toBeInTheDocument()
    })
  })

  it('displays an error message when login fails with invalid credentials', async () => {
    vi.mocked(fetch).mockImplementation((url: RequestInfo | URL) => {
      const urlStr = url.toString()
      if (urlStr.includes('/api/health')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () => Promise.resolve('Healthy'),
        } as Response)
      }
      if (urlStr.includes('/api/auth/me')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve({ isAuthenticated: false }),
        } as Response)
      }
      if (urlStr.includes('/api/auth/login')) {
        return Promise.resolve({
          ok: false,
          status: 401,
          json: () => Promise.resolve({ message: 'Invalid email or password.' }),
        } as Response)
      }
      return Promise.reject(new Error(`Unhandled URL: ${urlStr}`))
    })

    render(<App />)

    const emailInput = await screen.findByTestId('login-email')
    const passwordInput = screen.getByTestId('login-password')
    const submitBtn = screen.getByTestId('login-submit')

    fireEvent.change(emailInput, { target: { value: 'bad@household.local' } })
    fireEvent.change(passwordInput, { target: { value: 'WrongPass!' } })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(screen.getByTestId('login-error')).toHaveTextContent('Invalid email or password.')
    })
  })
})
