import type { TransactionDto } from '../types/transaction'

export async function fetchTransactions(
  accountId?: string,
  importBatchId?: string
): Promise<TransactionDto[]> {
  const params = new URLSearchParams()
  if (accountId) params.append('accountId', accountId)
  if (importBatchId) params.append('importBatchId', importBatchId)

  const query = params.toString() ? `?${params.toString()}` : ''
  const res = await fetch(`/api/transactions${query}`, {
    credentials: 'include',
  })

  if (!res.ok) {
    throw new Error(`Failed to load transactions (${res.status})`)
  }

  return res.json()
}
