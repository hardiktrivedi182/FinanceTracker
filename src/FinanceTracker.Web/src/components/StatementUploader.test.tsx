import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { StatementUploader } from './StatementUploader'

describe('StatementUploader Component', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn().mockImplementation(async (input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input instanceof Request ? input.url : input.toString()
      if (url.includes('/api/accounts')) {
        return { ok: true, json: async () => [] } as Response
      }
      if (url.includes('/api/statements')) {
        return { ok: true, json: async () => [] } as Response
      }
      if (url.includes('/api/auth/csrf-token')) {
        return { ok: true, json: async () => ({ token: 'mock-csrf-token' }) } as Response
      }
      return { ok: true, json: async () => ({}) } as Response
    }))
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders upload form, visible 10 MB limit, and ledger non-mutation disclaimer', async () => {
    render(<StatementUploader />)

    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Upload Financial Statement')
    expect(screen.getByText(/No transactions are imported into the ledger/i)).toBeInTheDocument()
    expect(screen.getByText(/Maximum file size is/i)).toHaveTextContent('10 MB')
    expect(screen.getByRole('button', { name: /Upload & Parse CSV/i })).toBeDisabled()

    await waitFor(() => {
      expect(screen.getByTestId('no-statements-message')).toBeInTheDocument()
    })
  })

  it('validates empty file before upload', async () => {
    render(<StatementUploader />)

    const fileInput = screen.getByLabelText(/Select CSV Statement/i)
    const emptyFile = new File([], 'empty.csv', { type: 'text/csv' })

    fireEvent.change(fileInput, { target: { files: [emptyFile] } })

    expect(await screen.findByTestId('client-validation-error')).toHaveTextContent('The selected file is empty (0 bytes).')
    expect(screen.getByRole('button', { name: /Upload & Parse CSV/i })).toBeDisabled()
  })

  it('validates non-CSV file extension before upload', async () => {
    render(<StatementUploader />)

    const fileInput = screen.getByLabelText(/Select CSV Statement/i)
    const pdfFile = new File(['dummy-content'], 'statement.pdf', { type: 'application/pdf' })

    fireEvent.change(fileInput, { target: { files: [pdfFile] } })

    expect(await screen.findByTestId('client-validation-error')).toHaveTextContent('Only CSV files (.csv) are supported')
    expect(screen.getByRole('button', { name: /Upload & Parse CSV/i })).toBeDisabled()
  })

  it('validates oversized file exceeding 10 MB before upload', async () => {
    render(<StatementUploader />)

    const fileInput = screen.getByLabelText(/Select CSV Statement/i)
    const oversizedBlob = new Blob([new Uint8Array(10 * 1024 * 1024 + 100)])
    const oversizedFile = new File([oversizedBlob], 'large.csv', { type: 'text/csv' })

    fireEvent.change(fileInput, { target: { files: [oversizedFile] } })

    expect(await screen.findByTestId('client-validation-error')).toHaveTextContent('exceeds the 10 MB maximum limit')
    expect(screen.getByRole('button', { name: /Upload & Parse CSV/i })).toBeDisabled()
  })

  it('renders success state with sample transactions and non-import notice after upload', async () => {
    let uploadCalled = false
    vi.mocked(fetch).mockImplementation(async (input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input instanceof Request ? input.url : input.toString()
      if (url.includes('/api/accounts')) {
        return { ok: true, json: async () => [] } as Response
      }
      if (url.includes('/api/auth/csrf-token')) {
        return { ok: true, json: async () => ({ token: 'mock-csrf-token' }) } as Response
      }
      if (url.includes('/api/statements/upload')) {
        uploadCalled = true
        return {
          ok: true,
          json: async () => ({
            statementId: 's-123',
            originalFileName: 'bank-march.csv',
            status: 'Parsed',
            parsedTransactionCount: 2,
            sampleRows: [
              { rowNumber: 2, date: '2026-03-01', description: 'Grocery Market', amount: 85.5, direction: 'Debit', balance: 1400 },
              { rowNumber: 3, date: '2026-03-02', description: 'Monthly Salary', amount: 3200, direction: 'Credit', balance: 4600 },
            ],
            errors: [],
            message: 'Statement parsed successfully (2 transactions found). 0 transactions imported. Ledger mutation requires explicit import confirmation.',
          }),
        } as Response
      }
      if (url.includes('/api/statements')) {
        if (uploadCalled) {
          return {
            ok: true,
            json: async () => [
              {
                id: 's-123',
                householdId: 'hh-1',
                uploaderUserId: 'partner-1',
                originalFileName: 'bank-march.csv',
                format: 'Csv',
                fileSizeBytes: 2048,
                uploadedAt: '2026-03-15T10:00:00Z',
                status: 'Parsed',
                parsedTransactionCount: 2,
              },
            ],
          } as Response
        }
        return { ok: true, json: async () => [] } as Response
      }
      return { ok: true, json: async () => ({}) } as Response
    })

    render(<StatementUploader />)

    const fileInput = screen.getByLabelText(/Select CSV Statement/i)
    const validFile = new File(['Date,Description,Amount\n2026-03-01,Grocery Market,-85.50'], 'bank-march.csv', { type: 'text/csv' })

    fireEvent.change(fileInput, { target: { files: [validFile] } })

    const uploadButton = screen.getByRole('button', { name: /Upload & Parse CSV/i })
    expect(uploadButton).not.toBeDisabled()
    fireEvent.click(uploadButton)

    await waitFor(() => {
      expect(screen.getByTestId('upload-status-banner')).toHaveTextContent('Status: Parsed — bank-march.csv')
      expect(screen.getByText(/0 transactions imported/i)).toBeInTheDocument()
      expect(screen.getByTestId('sample-transactions-preview')).toBeInTheDocument()
      expect(screen.getByText('Grocery Market')).toBeInTheDocument()
      expect(screen.getByText('Monthly Salary')).toBeInTheDocument()
    })
  })

  it('renders parse errors when uploaded CSV is malformed', async () => {
    vi.mocked(fetch).mockImplementation(async (input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input instanceof Request ? input.url : input.toString()
      if (url.includes('/api/accounts')) {
        return { ok: true, json: async () => [] } as Response
      }
      if (url.includes('/api/auth/csrf-token')) {
        return { ok: true, json: async () => ({ token: 'mock-csrf-token' }) } as Response
      }
      if (url.includes('/api/statements/upload')) {
        return {
          ok: true,
          json: async () => ({
            statementId: 's-999',
            originalFileName: 'bad.csv',
            status: 'Failed',
            parsedTransactionCount: 0,
            errorMessage: 'CSV parsing failed.',
            sampleRows: [],
            errors: [
              { rowNumber: 2, message: 'Ambiguous debit/credit: row contains both debit (50) and credit (50) amounts.' },
            ],
            message: "Statement processing completed with status 'Failed'. 0 transactions imported.",
          }),
        } as Response
      }
      if (url.includes('/api/statements')) {
        return { ok: true, json: async () => [] } as Response
      }
      return { ok: true, json: async () => ({}) } as Response
    })

    render(<StatementUploader />)

    const fileInput = screen.getByLabelText(/Select CSV Statement/i)
    const file = new File(['Date,Description,Debit,Credit\n2026-01-01,Test,50,50'], 'bad.csv', { type: 'text/csv' })
    fireEvent.change(fileInput, { target: { files: [file] } })

    fireEvent.click(screen.getByRole('button', { name: /Upload & Parse CSV/i }))

    await waitFor(() => {
      expect(screen.getByTestId('parse-errors-list')).toHaveTextContent('Row 2: Ambiguous debit/credit')
    })
  })
})
