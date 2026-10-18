export type StatementFormat = 'Csv'
export type StatementProcessingStatus = 'Uploaded' | 'Parsing' | 'Parsed' | 'Failed' | 'Committed'

export interface ParsedTransactionRowDto {
  rowNumber: number
  date: string
  description: string
  amount: number
  direction: 'Debit' | 'Credit'
  balance?: number | null
}

export interface StatementParseErrorDto {
  rowNumber?: number | null
  message: string
}

export interface StatementUploadResultDto {
  statementId: string
  originalFileName: string
  status: StatementProcessingStatus
  parsedTransactionCount?: number | null
  errorMessage?: string | null
  sampleRows?: ParsedTransactionRowDto[] | null
  errors?: StatementParseErrorDto[] | null
  message: string
  importBatchId?: string | null
}

export interface StatementSummaryDto {
  id: string
  householdId: string
  uploaderUserId: string
  originalFileName: string
  format: StatementFormat
  fileSizeBytes: number
  uploadedAt: string
  status: StatementProcessingStatus
  parsedTransactionCount?: number | null
  errorMessage?: string | null
}
