import type {
  AccountDto,
  AccountValuationDto,
  CreateAccountRequest,
  HouseholdNetWorthReportDto,
  NetWorthComparisonDto,
  RecordValuationRequest,
  UpdateValuationRequest,
} from '../types/account'
import { ensureXsrfToken } from './authApi'

async function getMutatingHeaders(): Promise<Record<string, string>> {
  const token = await ensureXsrfToken()
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  }
  if (token) {
    headers['X-XSRF-Token'] = token
  }
  return headers
}

function parseErrorMessage(errorBody: Record<string, unknown>, fallback: string): string {
  if (typeof errorBody.error === 'string' && errorBody.error.length > 0) return errorBody.error
  if (typeof errorBody.message === 'string' && errorBody.message.length > 0) return errorBody.message
  if (typeof errorBody.detail === 'string' && errorBody.detail.length > 0) return errorBody.detail
  if (errorBody.errors && typeof errorBody.errors === 'object') {
    const list = Object.values(errorBody.errors as Record<string, unknown[]>)
      .flat()
      .filter((m): m is string => typeof m === 'string' && m.length > 0)
    if (list.length > 0) return list.join(' ')
  }
  return fallback
}

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
  const headers = await getMutatingHeaders()
  const res = await fetch('/api/accounts', {
    method: 'POST',
    headers,
    credentials: 'include',
    body: JSON.stringify(data),
  })
  if (!res.ok) {
    const errorBody = (await res.json().catch(() => ({}))) as Record<string, unknown>
    throw new Error(parseErrorMessage(errorBody, `Failed to create account (${res.status})`))
  }
  return res.json()
}

export async function deactivateAccount(id: string): Promise<void> {
  const token = await ensureXsrfToken()
  const headers: Record<string, string> = {}
  if (token) {
    headers['X-XSRF-Token'] = token
  }
  const res = await fetch(`/api/accounts/${id}`, {
    method: 'DELETE',
    headers,
    credentials: 'include',
  })
  if (!res.ok) {
    const errorBody = (await res.json().catch(() => ({}))) as Record<string, unknown>
    throw new Error(parseErrorMessage(errorBody, `Failed to deactivate account (${res.status})`))
  }
}

export async function recordValuation(accountId: string, data: RecordValuationRequest): Promise<AccountValuationDto> {
  const headers = await getMutatingHeaders()
  const res = await fetch(`/api/accounts/${accountId}/valuations`, {
    method: 'POST',
    headers,
    credentials: 'include',
    body: JSON.stringify(data),
  })
  if (!res.ok) {
    const errorBody = (await res.json().catch(() => ({}))) as Record<string, unknown>
    throw new Error(parseErrorMessage(errorBody, `Failed to record valuation (${res.status})`))
  }
  return res.json()
}

export async function updateValuation(
  accountId: string,
  valuationId: string,
  data: UpdateValuationRequest
): Promise<AccountValuationDto> {
  const headers = await getMutatingHeaders()
  const res = await fetch(`/api/accounts/${accountId}/valuations/${valuationId}`, {
    method: 'PUT',
    headers,
    credentials: 'include',
    body: JSON.stringify(data),
  })
  if (!res.ok) {
    const errorBody = (await res.json().catch(() => ({}))) as Record<string, unknown>
    throw new Error(parseErrorMessage(errorBody, `Failed to update valuation (${res.status})`))
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
