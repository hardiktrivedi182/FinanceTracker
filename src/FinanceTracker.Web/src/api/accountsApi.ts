import type {
  AccountDto,
  AccountValuationDto,
  CreateAccountRequest,
  HouseholdNetWorthReportDto,
  NetWorthComparisonDto,
  RecordValuationRequest,
  UpdateValuationRequest,
} from '../types/account'

export async function fetchAccounts(includeInactive = false): Promise<AccountDto[]> {
  const res = await fetch(`/api/accounts?includeInactive=${includeInactive}`, {
    credentials: 'include',
  })
  if (!res.ok) {
    throw new Error(`Failed to load accounts (${res.status})`)
  }
  return res.json()
}

export async function createAccount(data: CreateAccountRequest): Promise<AccountDto> {
  const res = await fetch('/api/accounts', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(data),
  })
  if (!res.ok) {
    const errorBody = await res.json().catch(() => ({}))
    throw new Error(errorBody.error || `Failed to create account (${res.status})`)
  }
  return res.json()
}

export async function deactivateAccount(id: string): Promise<void> {
  const res = await fetch(`/api/accounts/${id}`, {
    method: 'DELETE',
    credentials: 'include',
  })
  if (!res.ok) {
    throw new Error(`Failed to deactivate account (${res.status})`)
  }
}

export async function recordValuation(accountId: string, data: RecordValuationRequest): Promise<AccountValuationDto> {
  const res = await fetch(`/api/accounts/${accountId}/valuations`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(data),
  })
  if (!res.ok) {
    const errorBody = await res.json().catch(() => ({}))
    throw new Error(errorBody.error || `Failed to record valuation (${res.status})`)
  }
  return res.json()
}

export async function updateValuation(
  accountId: string,
  valuationId: string,
  data: UpdateValuationRequest
): Promise<AccountValuationDto> {
  const res = await fetch(`/api/accounts/${accountId}/valuations/${valuationId}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(data),
  })
  if (!res.ok) {
    const errorBody = await res.json().catch(() => ({}))
    throw new Error(errorBody.error || `Failed to update valuation (${res.status})`)
  }
  return res.json()
}

export async function fetchValuations(accountId: string): Promise<AccountValuationDto[]> {
  const res = await fetch(`/api/accounts/${accountId}/valuations`, {
    credentials: 'include',
  })
  if (!res.ok) {
    throw new Error(`Failed to load valuations (${res.status})`)
  }
  return res.json()
}

export async function fetchNetWorth(asOf?: string, stalenessThresholdDays = 90): Promise<HouseholdNetWorthReportDto> {
  const query = new URLSearchParams()
  if (asOf) query.append('asOf', asOf)
  if (stalenessThresholdDays) query.append('stalenessThresholdDays', stalenessThresholdDays.toString())

  const res = await fetch(`/api/household/net-worth?${query.toString()}`, {
    credentials: 'include',
  })
  if (!res.ok) {
    throw new Error(`Failed to calculate net worth (${res.status})`)
  }
  return res.json()
}

export async function compareNetWorth(
  date1: string,
  date2: string,
  stalenessThresholdDays = 90
): Promise<NetWorthComparisonDto> {
  const query = new URLSearchParams({
    date1,
    date2,
    stalenessThresholdDays: stalenessThresholdDays.toString(),
  })

  const res = await fetch(`/api/household/net-worth/compare?${query.toString()}`, {
    credentials: 'include',
  })
  if (!res.ok) {
    throw new Error(`Failed to compare net worth (${res.status})`)
  }
  return res.json()
}
