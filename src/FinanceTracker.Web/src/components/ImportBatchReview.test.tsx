import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ImportBatchDetailDto } from '../types/importBatch'
import { ImportBatchReview } from './ImportBatchReview'

const mockBatchDetail: ImportBatchDetailDto = {
  batch: {
    id: 'batch-111',
    householdId: 'h-111',
    statementId: 'stmt-111',
    originalFileName: 'october_bank.csv',
    accountId: 'acc-111',
    accountName: 'Joint Checking',
    accountCurrency: 'USD',
    uploadedByUserId: 'user-1',
    status: 'ReviewRequired',
    totalRowCount: 3,
    readyCount: 1,
    duplicateCount: 1,
    errorCount: 1,
    selectedRowCount: 2,
    committedCount: null,
    createdAt: '2026-10-18T10:00:00Z',
    committedAt: null,
    committedByUserId: null,
  },
  rows: [
    {
      id: 'row-1',
      sourceRowNumber: 2,
      rawData: null,
      transactionDate: '2026-10-01',
      description: 'Grocery Store',
      amount: 45.2,
      type: 'Debit',
      currencyCode: 'USD',
      fingerprint: 'fp-1',
      validationStatus: 'Valid',
      validationErrors: null,
      duplicateStatus: 'None',
      duplicateReason: null,
      matchedTransactionId: null,
      isSelected: true,
      resolution: 'None',
      resolutionNotes: null,
      resolvedTransactionDate: null,
      resolvedDescription: null,
      resolvedAmount: null,
      resolvedType: null,
      effectiveDate: '2026-10-01',
      effectiveDescription: 'Grocery Store',
      effectiveAmount: 45.2,
      effectiveType: 'Debit',
      isEligibleForCommit: true,
      committedTransactionId: null,
    },
    {
      id: 'row-2',
      sourceRowNumber: 3,
      rawData: null,
      transactionDate: '2026-10-02',
      description: 'Coffee Shop',
      amount: 4.5,
      type: 'Debit',
      currencyCode: 'USD',
      fingerprint: 'fp-2',
      validationStatus: 'Valid',
      validationErrors: null,
      duplicateStatus: 'PossibleDuplicate',
      duplicateReason: 'Possible duplicate: matches existing transaction on 2026-10-02',
      matchedTransactionId: 'existing-tx-1',
      isSelected: false,
      resolution: 'None',
      resolutionNotes: null,
      resolvedTransactionDate: null,
      resolvedDescription: null,
      resolvedAmount: null,
      resolvedType: null,
      effectiveDate: '2026-10-02',
      effectiveDescription: 'Coffee Shop',
      effectiveAmount: 4.5,
      effectiveType: 'Debit',
      isEligibleForCommit: false,
      committedTransactionId: null,
    },
    {
      id: 'row-3',
      sourceRowNumber: 4,
      rawData: null,
      transactionDate: null,
      description: 'Malformed Row',
      amount: null,
      type: null,
      currencyCode: 'USD',
      fingerprint: '',
      validationStatus: 'Invalid',
      validationErrors: 'Invalid date format',
      duplicateStatus: 'None',
      duplicateReason: null,
      matchedTransactionId: null,
      isSelected: false,
      resolution: 'None',
      resolutionNotes: null,
      resolvedTransactionDate: null,
      resolvedDescription: null,
      resolvedAmount: null,
      resolvedType: null,
      effectiveDate: null,
      effectiveDescription: 'Malformed Row',
      effectiveAmount: null,
      effectiveType: null,
      isEligibleForCommit: false,
      committedTransactionId: null,
    },
  ],
}

describe('ImportBatchReview Component', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn().mockImplementation(async (input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input instanceof Request ? input.url : input.toString()
      if (url.includes('/api/auth/csrf-token')) {
        return { ok: true, json: async () => ({ token: 'mock-csrf-token' }) } as Response
      }
      return { ok: true, json: async () => ({}) } as Response
    }))
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders batch summary, KPI counters, and rows with diagnostics', () => {
    render(<ImportBatchReview batchDetail={mockBatchDetail} onBack={vi.fn()} />)

    expect(screen.getByText(/Review Import Batch:/i)).toHaveTextContent('october_bank.csv')
    expect(screen.getByText(/Joint Checking/i)).toBeInTheDocument()
    expect(screen.getByTestId('batch-status-badge')).toHaveTextContent('Review Required')

    expect(screen.getByTestId('stat-total-rows')).toHaveTextContent('3')
    expect(screen.getByTestId('stat-ready-rows')).toHaveTextContent('1')
    expect(screen.getByTestId('stat-error-rows')).toHaveTextContent('1')
    expect(screen.getByTestId('stat-duplicate-rows')).toHaveTextContent('1')

    // Diagnostics displayed
    expect(screen.getByText(/Possible duplicate: matches existing transaction/i)).toBeInTheDocument()
    expect(screen.getByText(/Invalid date format/i)).toBeInTheDocument()

    // Commit button disabled because review is required
    const commitBtn = screen.getByTestId('open-commit-modal-btn')
    expect(commitBtn).toBeDisabled()
  })

  it('filters rows when tabs are clicked', () => {
    render(<ImportBatchReview batchDetail={mockBatchDetail} onBack={vi.fn()} />)

    // Filter to errors only
    fireEvent.click(screen.getByTestId('filter-errors'))
    expect(screen.getByTestId('staging-row-4')).toBeInTheDocument()
    expect(screen.queryByTestId('staging-row-2')).not.toBeInTheDocument()

    // Filter to duplicates only
    fireEvent.click(screen.getByTestId('filter-duplicates'))
    expect(screen.getByTestId('staging-row-3')).toBeInTheDocument()
    expect(screen.queryByTestId('staging-row-4')).not.toBeInTheDocument()

    // Return to all
    fireEvent.click(screen.getByTestId('filter-all'))
    expect(screen.getByTestId('staging-row-2')).toBeInTheDocument()
    expect(screen.getByTestId('staging-row-3')).toBeInTheDocument()
    expect(screen.getByTestId('staging-row-4')).toBeInTheDocument()
  })

  it('allows approving duplicate and shows confirmation modal on commit', async () => {
    const updatedApprovedBatch: ImportBatchDetailDto = {
      ...mockBatchDetail,
      batch: {
        ...mockBatchDetail.batch,
        status: 'ReadyToCommit',
        readyCount: 2,
        errorCount: 0,
        duplicateCount: 1,
        selectedRowCount: 2,
      },
      rows: [
        mockBatchDetail.rows[0],
        {
          ...mockBatchDetail.rows[1],
          isSelected: true,
          resolution: 'ApprovedAsIs',
          isEligibleForCommit: true,
        },
        {
          ...mockBatchDetail.rows[2],
          isSelected: false,
          resolution: 'Excluded',
          isEligibleForCommit: false,
        },
      ],
    }

    let isCommitted = false

    vi.mocked(fetch).mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input instanceof Request ? input.url : input.toString()
      const method = init?.method || (input instanceof Request ? input.method : 'GET')

      if (url.includes('/api/auth/csrf-token')) {
        return { ok: true, json: async () => ({ token: 'mock-csrf-token' }) } as Response
      }

      if (url.includes('/rows/') && method === 'PUT') {
        return {
          ok: true,
          json: async () => updatedApprovedBatch,
        } as Response
      }

      if (url.includes('/commit') && method === 'POST') {
        isCommitted = true
        return {
          ok: true,
          json: async () => ({
            batchId: 'batch-111',
            statementId: 'stmt-111',
            accountId: 'acc-111',
            status: 'Committed',
            importedCount: 2,
            excludedCount: 1,
            unresolvedCount: 0,
            committedAt: '2026-10-18T10:15:00Z',
            message: 'Successfully committed 2 transactions.',
          }),
        } as Response
      }

      if (url.includes('/api/import-batches/batch-111') && method === 'GET') {
        return {
          ok: true,
          json: async () => ({
            ...updatedApprovedBatch,
            batch: {
              ...updatedApprovedBatch.batch,
              status: isCommitted ? 'Committed' : 'ReadyToCommit',
            },
          }),
        } as Response
      }

      return { ok: true, json: async () => ({}) } as Response
    })

    render(<ImportBatchReview batchDetail={mockBatchDetail} onBack={vi.fn()} />)

    // Approve duplicate button for row 3
    const approveBtn = screen.getByTestId('approve-dup-btn-3')
    fireEvent.click(approveBtn)

    await waitFor(() => {
      expect(screen.getByTestId('batch-status-badge')).toHaveTextContent('Ready to Commit')
    })

    const commitBtn = screen.getByTestId('open-commit-modal-btn')
    expect(commitBtn).toBeEnabled()
    expect(commitBtn).toHaveTextContent('Commit 2 Transactions')

    // Open confirmation modal
    fireEvent.click(commitBtn)

    expect(screen.getByTestId('commit-confirm-modal')).toBeInTheDocument()
    expect(screen.getByText(/Permanent Ledger Warning:/i)).toBeInTheDocument()

    fireEvent.click(screen.getByTestId('confirm-commit-submit-btn'))

    await waitFor(() => {
      expect(screen.getByTestId('commit-success-screen')).toBeInTheDocument()
    })
    expect(screen.getByTestId('result-imported-count')).toHaveTextContent('2')
  })
})
