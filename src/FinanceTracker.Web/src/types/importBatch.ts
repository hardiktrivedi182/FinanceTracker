export type ImportBatchStatus =
  | 'AwaitingReview'
  | 'ReviewRequired'
  | 'ReadyToCommit'
  | 'Committed'
  | 'Cancelled'

export type StagingRowValidationStatus = 'Valid' | 'Warning' | 'Invalid'

export type DuplicateStatus = 'None' | 'PossibleDuplicate' | 'ExactDuplicate'

export type RowResolution =
  | 'None'
  | 'ApprovedAsIs'
  | 'ManuallyCorrected'
  | 'Excluded'

export interface ImportBatchSummaryDto {
  id: string
  householdId: string
  statementId: string
  originalFileName: string
  accountId: string
  accountName: string
  accountCurrency: string
  uploadedByUserId: string
  status: ImportBatchStatus
  totalRowCount: number
  readyCount: number
  duplicateCount: number
  errorCount: number
  selectedRowCount: number
  committedCount?: number | null
  createdAt: string
  committedAt?: string | null
  committedByUserId?: string | null
}

export interface ImportStagingRowDto {
  id: string
  sourceRowNumber: number
  rawData?: string | null
  transactionDate?: string | null
  description: string
  amount?: number | null
  type?: 'Debit' | 'Credit' | null
  currencyCode?: string | null
  fingerprint: string
  validationStatus: StagingRowValidationStatus
  validationErrors?: string | null
  duplicateStatus: DuplicateStatus
  duplicateReason?: string | null
  matchedTransactionId?: string | null
  isSelected: boolean
  resolution: RowResolution
  resolutionNotes?: string | null
  resolvedTransactionDate?: string | null
  resolvedDescription?: string | null
  resolvedAmount?: number | null
  resolvedType?: 'Debit' | 'Credit' | null
  effectiveDate?: string | null
  effectiveDescription: string
  effectiveAmount?: number | null
  effectiveType?: 'Debit' | 'Credit' | null
  isEligibleForCommit: boolean
  committedTransactionId?: string | null
}

export interface ImportBatchDetailDto {
  batch: ImportBatchSummaryDto
  rows: ImportStagingRowDto[]
}

export interface CreateImportBatchRequest {
  statementId: string
  accountId: string
}

export interface UpdateStagingRowRequest {
  isSelected?: boolean
  resolution?: RowResolution
  resolutionNotes?: string
  resolvedDate?: string
  resolvedDescription?: string
  resolvedAmount?: number
  resolvedType?: 'Debit' | 'Credit'
}

export interface BulkUpdateRowsRequest {
  rowIds?: string[]
  selectAll?: boolean
  isSelected?: boolean
  resolution?: RowResolution
  resolutionNotes?: string
}

export interface CommitImportBatchResultDto {
  batchId: string
  statementId: string
  accountId: string
  status: ImportBatchStatus
  importedCount: number
  excludedCount: number
  unresolvedCount: number
  committedAt: string
  message: string
}
