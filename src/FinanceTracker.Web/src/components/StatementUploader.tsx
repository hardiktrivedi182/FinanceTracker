import React, { useEffect, useState } from 'react'
import { fetchAccounts } from '../api/accountsApi'
import { createImportBatch, getImportBatch, getImportBatches } from '../api/importBatchesApi'
import { fetchStatements, uploadStatement } from '../api/statementsApi'
import type { AccountDto } from '../types/account'
import type { ImportBatchDetailDto } from '../types/importBatch'
import type { StatementSummaryDto, StatementUploadResultDto } from '../types/statement'
import { ImportBatchReview } from './ImportBatchReview'

const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024 // 10 MB

export const StatementUploader: React.FC = () => {
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [targetAccountId, setTargetAccountId] = useState<string>('')
  const [accounts, setAccounts] = useState<AccountDto[]>([])
  const [clientError, setClientError] = useState<string | null>(null)
  const [isUploading, setIsUploading] = useState<boolean>(false)
  const [uploadResult, setUploadResult] = useState<StatementUploadResultDto | null>(null)
  const [uploadError, setUploadError] = useState<string | null>(null)
  const [statements, setStatements] = useState<StatementSummaryDto[]>([])
  const [isLoadingHistory, setIsLoadingHistory] = useState<boolean>(true)

  // Active batch review view
  const [activeBatchDetail, setActiveBatchDetail] = useState<ImportBatchDetailDto | null>(null)
  const [isLoadingBatch, setIsLoadingBatch] = useState<boolean>(false)

  useEffect(() => {
    let ignore = false

    fetchStatements()
      .then((data) => {
        if (!ignore) setStatements(data)
      })
      .catch(() => {
        // Ignore initial failure if offline or unauthenticated
      })
      .finally(() => {
        if (!ignore) setIsLoadingHistory(false)
      })

    fetchAccounts()
      .then((accs) => {
        if (!ignore) {
          setAccounts(accs.filter((a) => a.isActive))
          if (accs.length > 0) {
            setTargetAccountId(accs[0].id)
          }
        }
      })
      .catch(() => {
        // Ignore
      })

    return () => {
      ignore = true
    }
  }, [])

  const reloadHistory = async () => {
    try {
      const data = await fetchStatements()
      setStatements(data)
    } catch {
      // Ignore
    }
  }

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setClientError(null)
    setUploadError(null)
    setUploadResult(null)

    const file = e.target.files?.[0]
    if (!file) {
      setSelectedFile(null)
      return
    }

    if (file.size === 0) {
      setClientError('The selected file is empty (0 bytes).')
      setSelectedFile(null)
      return
    }

    if (file.size > MAX_FILE_SIZE_BYTES) {
      const sizeMb = (file.size / (1024 * 1024)).toFixed(2)
      setClientError(`File size (${sizeMb} MB) exceeds the 10 MB maximum limit.`)
      setSelectedFile(null)
      return
    }

    if (!file.name.toLowerCase().endsWith('.csv')) {
      setClientError('Invalid file type. Only CSV files (.csv) are supported in this release.')
      setSelectedFile(null)
      return
    }

    setSelectedFile(file)
  }

  const handleUpload = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedFile) return

    setIsUploading(true)
    setUploadError(null)
    setUploadResult(null)

    try {
      const result = await uploadStatement(selectedFile, targetAccountId || undefined)
      setUploadResult(result)
      reloadHistory()

      if (result.importBatchId) {
        // Automatically load and open review if batch was initialized
        setIsLoadingBatch(true)
        const batch = await getImportBatch(result.importBatchId)
        setActiveBatchDetail(batch)
      }
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : 'Failed to upload statement.')
    } finally {
      setIsUploading(false)
      setIsLoadingBatch(false)
    }
  }

  const handleOpenReview = async (statementId: string) => {
    if (!targetAccountId) {
      setUploadError('Please select a target account to review and import this statement.')
      return
    }

    setIsLoadingBatch(true)
    setUploadError(null)
    try {
      // Check if an import batch already exists for this statement
      const batches = await getImportBatches(statementId)
      if (batches.length > 0) {
        const detail = await getImportBatch(batches[0].id)
        setActiveBatchDetail(detail)
      } else {
        const created = await createImportBatch(statementId, targetAccountId)
        setActiveBatchDetail(created)
      }
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : 'Failed to launch import review.')
    } finally {
      setIsLoadingBatch(false)
    }
  }

  const handleReset = () => {
    setSelectedFile(null)
    setUploadResult(null)
    setUploadError(null)
    setClientError(null)
  }

  // If currently in active review mode, render ImportBatchReview
  if (activeBatchDetail) {
    return (
      <ImportBatchReview
        batchDetail={activeBatchDetail}
        onBack={() => {
          setActiveBatchDetail(null)
          reloadHistory()
        }}
        onCommitted={() => {
          reloadHistory()
        }}
      />
    )
  }

  return (
    <div style={{ marginTop: '2rem' }}>
      <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: 8, padding: '1.5rem', marginBottom: '2rem' }}>
        <h2 style={{ margin: '0 0 0.5rem 0', fontSize: '1.25rem' }}>Upload Financial Statement</h2>
        <p style={{ margin: '0 0 1rem 0', color: '#64748b', fontSize: '0.9rem' }}>
          Securely upload bank or credit card CSV statements. Uploaded statements are parsed in an isolated sandbox.
        </p>

        <div style={{ background: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: 6, padding: '0.75rem 1rem', marginBottom: '1.5rem', fontSize: '0.875rem', color: '#1e40af' }}>
          <strong>Notice:</strong> Statement upload and parsing is read-only. <strong>No transactions are imported into the ledger</strong>.
          Maximum file size is <strong>10 MB</strong>. Only generic CSV files are supported.
        </div>

        <form onSubmit={handleUpload}>
          <div style={{ marginBottom: '1rem' }}>
            <label htmlFor="statement-file-input" style={{ display: 'block', fontWeight: 500, marginBottom: '0.5rem', fontSize: '0.9rem' }}>
              Select CSV Statement (Max 10 MB):
            </label>
            <input
              id="statement-file-input"
              type="file"
              accept=".csv,text/csv"
              onChange={handleFileChange}
              disabled={isUploading || isLoadingBatch}
              style={{ display: 'block', width: '100%', padding: '0.5rem', border: '1px solid #cbd5e1', borderRadius: 6 }}
            />
          </div>

          <div style={{ marginBottom: '1.25rem' }}>
            <label htmlFor="target-account-select" style={{ display: 'block', fontWeight: 500, marginBottom: '0.5rem', fontSize: '0.9rem' }}>
              Target Account for Import:
            </label>
            {accounts.length === 0 ? (
              <p style={{ margin: 0, color: '#64748b', fontSize: '0.85rem' }}>
                No active accounts found. Target account can be selected later during import review.
              </p>
            ) : (
              <select
                id="target-account-select"
                data-testid="target-account-select"
                value={targetAccountId}
                onChange={(e) => setTargetAccountId(e.target.value)}
                disabled={isUploading || isLoadingBatch}
                style={{ display: 'block', width: '100%', padding: '0.5rem', border: '1px solid #cbd5e1', borderRadius: 6 }}
              >
                <option value="">-- Choose target account (optional) --</option>
                {accounts.map((acc) => (
                  <option key={acc.id} value={acc.id}>
                    {acc.name} ({acc.accountType} — {acc.currencyCode})
                  </option>
                ))}
              </select>
            )}
          </div>

          {clientError && (
            <div data-testid="client-validation-error" style={{ color: '#b91c1c', background: '#fef2f2', border: '1px solid #fecaca', padding: '0.5rem 0.75rem', borderRadius: 6, marginBottom: '1rem', fontSize: '0.875rem' }}>
              {clientError}
            </div>
          )}

          <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
            <button
              type="submit"
              disabled={!selectedFile || isUploading || isLoadingBatch || Boolean(clientError)}
              style={{
                background: !selectedFile || isUploading || isLoadingBatch || Boolean(clientError) ? '#94a3b8' : '#2563eb',
                color: '#fff',
                border: 'none',
                borderRadius: 6,
                padding: '0.6rem 1.2rem',
                cursor: !selectedFile || isUploading || isLoadingBatch || Boolean(clientError) || accounts.length === 0 ? 'not-allowed' : 'pointer',
                fontWeight: 500,
              }}
            >
              {isUploading ? 'Uploading & Parsing...' : 'Upload & Parse CSV'}
            </button>

            {(selectedFile || uploadResult || uploadError) && (
              <button
                type="button"
                onClick={handleReset}
                disabled={isUploading || isLoadingBatch}
                style={{
                  background: '#f1f5f9',
                  color: '#475569',
                  border: '1px solid #cbd5e1',
                  borderRadius: 6,
                  padding: '0.6rem 1.2rem',
                  cursor: isUploading || isLoadingBatch ? 'not-allowed' : 'pointer',
                }}
              >
                Reset
              </button>
            )}
          </div>
        </form>

        {(isUploading || isLoadingBatch) && (
          <div data-testid="upload-loading-state" style={{ marginTop: '1.5rem', padding: '1rem', background: '#f8fafc', borderRadius: 6, border: '1px dashed #cbd5e1', textAlign: 'center', color: '#64748b' }}>
            {isUploading ? 'Streaming statement securely and validating CSV structure...' : 'Loading import review batch...'}
          </div>
        )}

        {uploadError && (
          <div data-testid="upload-error-banner" style={{ marginTop: '1.5rem', padding: '1rem', background: '#fef2f2', border: '1px solid #f87171', borderRadius: 6, color: '#991b1b' }}>
            <h4 style={{ margin: '0 0 0.5rem 0', fontSize: '1rem' }}>Upload Failed</h4>
            <p style={{ margin: 0, fontSize: '0.875rem' }}>{uploadError}</p>
            <div style={{ marginTop: '0.75rem' }}>
              <button
                type="button"
                onClick={handleReset}
                style={{ background: '#b91c1c', color: '#fff', border: 'none', borderRadius: 4, padding: '0.4rem 0.8rem', fontSize: '0.8rem', cursor: 'pointer' }}
              >
                Try Again
              </button>
            </div>
          </div>
        )}

        {uploadResult && (
          <div data-testid="upload-result-container" style={{ marginTop: '1.5rem' }}>
            <div
              data-testid="upload-status-banner"
              style={{
                padding: '1rem',
                borderRadius: 6,
                background: uploadResult.status === 'Parsed' ? '#f0fdf4' : '#fffbeb',
                border: `1px solid ${uploadResult.status === 'Parsed' ? '#86efac' : '#fcd34d'}`,
                color: uploadResult.status === 'Parsed' ? '#166534' : '#92400e',
              }}
            >
              <h4 style={{ margin: '0 0 0.25rem 0', fontSize: '1rem' }}>
                Status: {uploadResult.status} — {uploadResult.originalFileName}
              </h4>
              <p style={{ margin: 0, fontSize: '0.875rem' }}>{uploadResult.message}</p>
              {uploadResult.errorMessage && (
                <p style={{ margin: '0.5rem 0 0 0', fontWeight: 600, color: '#b91c1c', fontSize: '0.875rem' }}>
                  Error: {uploadResult.errorMessage}
                </p>
              )}

              {uploadResult.status === 'Parsed' && (
                <div style={{ marginTop: '0.75rem' }}>
                  <button
                    type="button"
                    data-testid="start-review-btn"
                    onClick={() => handleOpenReview(uploadResult.statementId)}
                    style={{
                      background: '#16a34a',
                      color: '#fff',
                      border: 'none',
                      borderRadius: 4,
                      padding: '0.45rem 0.9rem',
                      fontSize: '0.85rem',
                      fontWeight: 600,
                      cursor: 'pointer',
                    }}
                  >
                    Start Import Review &amp; Duplicate Check →
                  </button>
                </div>
              )}
            </div>

            {uploadResult.errors && uploadResult.errors.length > 0 && (
              <div data-testid="parse-errors-list" style={{ marginTop: '1rem', padding: '1rem', background: '#fef2f2', border: '1px solid #fca5a5', borderRadius: 6 }}>
                <h5 style={{ margin: '0 0 0.5rem 0', color: '#991b1b' }}>
                  Parse Errors ({uploadResult.errors.length}):
                </h5>
                <ul style={{ margin: 0, paddingLeft: '1.25rem', fontSize: '0.85rem', color: '#b91c1c' }}>
                  {uploadResult.errors.map((err, i) => (
                    <li key={i}>
                      {err.rowNumber ? <strong>Row {err.rowNumber}: </strong> : null}
                      {err.message}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {uploadResult.sampleRows && uploadResult.sampleRows.length > 0 && (
              <div data-testid="sample-transactions-preview" style={{ marginTop: '1.5rem' }}>
                <h4 style={{ margin: '0 0 0.5rem 0', fontSize: '0.95rem', color: '#334155' }}>
                  Transaction Preview (First {uploadResult.sampleRows.length} Rows — Not Imported):
                </h4>
                <div style={{ overflowX: 'auto' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem', textAlign: 'left' }}>
                    <thead>
                      <tr style={{ background: '#f1f5f9', borderBottom: '1px solid #cbd5e1' }}>
                        <th style={{ padding: '0.5rem' }}>Row</th>
                        <th style={{ padding: '0.5rem' }}>Date</th>
                        <th style={{ padding: '0.5rem' }}>Description</th>
                        <th style={{ padding: '0.5rem' }}>Direction</th>
                        <th style={{ padding: '0.5rem', textAlign: 'right' }}>Amount</th>
                        <th style={{ padding: '0.5rem', textAlign: 'right' }}>Balance</th>
                      </tr>
                    </thead>
                    <tbody>
                      {uploadResult.sampleRows.map((row) => (
                        <tr key={row.rowNumber} style={{ borderBottom: '1px solid #f1f5f9' }}>
                          <td style={{ padding: '0.5rem', color: '#64748b' }}>{row.rowNumber}</td>
                          <td style={{ padding: '0.5rem' }}>{row.date}</td>
                          <td style={{ padding: '0.5rem' }}>{row.description}</td>
                          <td style={{ padding: '0.5rem' }}>
                            <span
                              style={{
                                display: 'inline-block',
                                padding: '0.15rem 0.4rem',
                                borderRadius: 4,
                                fontSize: '0.75rem',
                                fontWeight: 600,
                                background: row.direction === 'Credit' ? '#dcfce7' : '#fee2e2',
                                color: row.direction === 'Credit' ? '#15803d' : '#b91c1c',
                              }}
                            >
                              {row.direction}
                            </span>
                          </td>
                          <td style={{ padding: '0.5rem', textAlign: 'right', fontWeight: 500 }}>
                            {row.amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                          </td>
                          <td style={{ padding: '0.5rem', textAlign: 'right', color: '#64748b' }}>
                            {row.balance != null ? row.balance.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '—'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: 8, padding: '1.5rem' }}>
        <h3 style={{ margin: '0 0 1rem 0', fontSize: '1.1rem' }}>Uploaded Statements History</h3>
        {isLoadingHistory ? (
          <div style={{ color: '#64748b', fontSize: '0.9rem' }}>Loading statements...</div>
        ) : statements.length === 0 ? (
          <div data-testid="no-statements-message" style={{ color: '#94a3b8', fontSize: '0.9rem' }}>No statements have been uploaded for this household yet.</div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem', textAlign: 'left' }}>
              <thead>
                <tr style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0' }}>
                  <th style={{ padding: '0.5rem' }}>Filename</th>
                  <th style={{ padding: '0.5rem' }}>Format</th>
                  <th style={{ padding: '0.5rem' }}>Size</th>
                  <th style={{ padding: '0.5rem' }}>Uploaded</th>
                  <th style={{ padding: '0.5rem' }}>Status</th>
                  <th style={{ padding: '0.5rem', textAlign: 'right' }}>Parsed Txns</th>
                  <th style={{ padding: '0.5rem', textAlign: 'right' }}>Action</th>
                </tr>
              </thead>
              <tbody>
                {statements.map((s) => (
                  <tr key={s.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                    <td style={{ padding: '0.5rem', fontWeight: 500 }}>{s.originalFileName}</td>
                    <td style={{ padding: '0.5rem', color: '#64748b' }}>{s.format}</td>
                    <td style={{ padding: '0.5rem', color: '#64748b' }}>{(s.fileSizeBytes / 1024).toFixed(1)} KB</td>
                    <td style={{ padding: '0.5rem', color: '#64748b' }}>{new Date(s.uploadedAt).toLocaleString()}</td>
                    <td style={{ padding: '0.5rem' }}>
                      <span
                        style={{
                          display: 'inline-block',
                          padding: '0.15rem 0.4rem',
                          borderRadius: 4,
                          fontSize: '0.75rem',
                          fontWeight: 600,
                          background:
                            s.status === 'Committed'
                              ? '#dcfce7'
                              : s.status === 'Parsed'
                              ? '#eff6ff'
                              : s.status === 'Failed'
                              ? '#fee2e2'
                              : '#fef3c7',
                          color:
                            s.status === 'Committed'
                              ? '#166534'
                              : s.status === 'Parsed'
                              ? '#1d4ed8'
                              : s.status === 'Failed'
                              ? '#b91c1c'
                              : '#b45309',
                        }}
                      >
                        {s.status}
                      </span>
                    </td>
                    <td style={{ padding: '0.5rem', textAlign: 'right' }}>{s.parsedTransactionCount ?? '—'}</td>
                    <td style={{ padding: '0.5rem', textAlign: 'right' }}>
                      {s.status === 'Parsed' && (
                        <button
                          type="button"
                          data-testid={`review-btn-${s.id}`}
                          onClick={() => handleOpenReview(s.id)}
                          disabled={isLoadingBatch}
                          style={{
                            background: '#2563eb',
                            color: '#fff',
                            border: 'none',
                            borderRadius: 4,
                            padding: '0.25rem 0.6rem',
                            fontSize: '0.75rem',
                            cursor: 'pointer',
                            fontWeight: 500,
                          }}
                        >
                          Review &amp; Import
                        </button>
                      )}
                      {s.status === 'Committed' && (
                        <button
                          type="button"
                          data-testid={`view-batch-btn-${s.id}`}
                          onClick={() => handleOpenReview(s.id)}
                          disabled={isLoadingBatch}
                          style={{
                            background: '#f1f5f9',
                            color: '#475569',
                            border: '1px solid #cbd5e1',
                            borderRadius: 4,
                            padding: '0.25rem 0.6rem',
                            fontSize: '0.75rem',
                            cursor: 'pointer',
                          }}
                        >
                          View Batch
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
