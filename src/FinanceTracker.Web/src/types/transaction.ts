export interface TransactionDto {
  id: string
  householdId: string
  accountId: string
  statementId?: string | null
  importBatchId?: string | null
  sourceRowNumber?: number | null
  transactionDate: string
  originalTransactionDate: string
  description: string
  amount: number
  type: 'Debit' | 'Credit'
  signedAmount: number
  currencyCode: string
  fingerprint: string
  createdAt: string
}
