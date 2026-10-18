import React, { useState } from 'react'
import {
  bulkUpdateRows,
  commitImportBatch,
  getImportBatch,
  updateStagingRow,
} from '../api/importBatchesApi'
import type {
  CommitImportBatchResultDto,
  ImportBatchDetailDto,
  ImportStagingRowDto,
} from '../types/importBatch'

interface ImportBatchReviewProps {
  batchDetail: ImportBatchDetailDto
  onBack: () => void
  onCommitted?: (result: CommitImportBatchResultDto) => void
}

type FilterType = 'all' | 'errors' | 'duplicates' | 'selected' | 'excluded'

export const ImportBatchReview: React.FC<ImportBatchReviewProps> = ({
  batchDetail: initialDetail,
  onBack,
  onCommitted,
}) => {
  const [detail, setDetail] = useState<ImportBatchDetailDto>(initialDetail)
  const [activeFilter, setActiveFilter] = useState<FilterType>('all')
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [commitResult, setCommitResult] = useState<CommitImportBatchResultDto | null>(null)
  const [showConfirmModal, setShowConfirmModal] = useState<boolean>(false)

  // Editing state for manual correction modal/popover
  const [editingRow, setEditingRow] = useState<ImportStagingRowDto | null>(null)
  const [editDate, setEditDate] = useState<string>('')
  const [editDesc, setEditDesc] = useState<string>('')
  const [editAmount, setEditAmount] = useState<string>('')
  const [editType, setEditType] = useState<'Debit' | 'Credit'>('Debit')
  const [editNotes, setEditNotes] = useState<string>('')

  const { batch, rows } = detail

  const refreshBatch = async () => {
    try {
      const updated = await getImportBatch(batch.id)
      setDetail(updated)
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Failed to reload batch.')
    }
  }

  const handleToggleSelect = async (row: ImportStagingRowDto) => {
    setIsSubmitting(true)
    setErrorMessage(null)
    try {
      const updated = await updateStagingRow(batch.id, row.id, {
        isSelected: !row.isSelected,
      })
      setDetail(updated)
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Failed to update row selection.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleApproveDuplicate = async (row: ImportStagingRowDto) => {
    setIsSubmitting(true)
    setErrorMessage(null)
    try {
      const updated = await updateStagingRow(batch.id, row.id, {
        isSelected: true,
        resolution: 'ApprovedAsIs',
        resolutionNotes: 'Approved by user during import review',
      })
      setDetail(updated)
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Failed to approve duplicate.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleExcludeRow = async (row: ImportStagingRowDto) => {
    setIsSubmitting(true)
    setErrorMessage(null)
    try {
      const updated = await updateStagingRow(batch.id, row.id, {
        isSelected: false,
        resolution: 'Excluded',
        resolutionNotes: 'Excluded by user during import review',
      })
      setDetail(updated)
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Failed to exclude row.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleOpenEdit = (row: ImportStagingRowDto) => {
    setEditingRow(row)
    setEditDate(row.effectiveDate || row.transactionDate || '')
    setEditDesc(row.effectiveDescription || row.description || '')
    setEditAmount(row.effectiveAmount != null ? row.effectiveAmount.toString() : (row.amount != null ? row.amount.toString() : ''))
    setEditType((row.effectiveType || row.type || 'Debit') as 'Debit' | 'Credit')
    setEditNotes(row.resolutionNotes || '')
  }

  const handleSaveCorrection = async () => {
    if (!editingRow) return

    const parsedAmt = parseFloat(editAmount)
    if (isNaN(parsedAmt) || parsedAmt <= 0) {
      setErrorMessage('Amount must be a positive number.')
      return
    }

    if (!editDesc.trim()) {
      setErrorMessage('Description cannot be empty.')
      return
    }

    if (!editDate.trim()) {
      setErrorMessage('Transaction date is required.')
      return
    }

    setIsSubmitting(true)
    setErrorMessage(null)
    try {
      const updated = await updateStagingRow(batch.id, editingRow.id, {
        isSelected: true,
        resolution: 'ManuallyCorrected',
        resolutionNotes: editNotes.trim() || 'Corrected during import review',
        resolvedDate: editDate.trim(),
        resolvedDescription: editDesc.trim(),
        resolvedAmount: parsedAmt,
        resolvedType: editType,
      })
      setDetail(updated)
      setEditingRow(null)
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Failed to save row correction.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleBulkAction = async (action: 'selectAllValid' | 'excludeAllDuplicates' | 'unselectAll') => {
    setIsSubmitting(true)
    setErrorMessage(null)
    try {
      let updated: ImportBatchDetailDto
      if (action === 'selectAllValid') {
        const validIds = rows
          .filter((r) => r.validationStatus === 'Valid' && r.duplicateStatus === 'None')
          .map((r) => r.id)
        updated = await bulkUpdateRows(batch.id, {
          rowIds: validIds,
          isSelected: true,
          resolution: 'None',
        })
      } else if (action === 'excludeAllDuplicates') {
        const dupIds = rows
          .filter((r) => r.duplicateStatus !== 'None' && r.resolution !== 'ApprovedAsIs')
          .map((r) => r.id)
        updated = await bulkUpdateRows(batch.id, {
          rowIds: dupIds,
          isSelected: false,
          resolution: 'Excluded',
          resolutionNotes: 'Excluded bulk duplicates',
        })
      } else {
        updated = await bulkUpdateRows(batch.id, {
          selectAll: true,
          isSelected: false,
          resolution: 'None',
        })
      }
      setDetail(updated)
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Bulk operation failed.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleCommitConfirm = async () => {
    setIsSubmitting(true)
    setErrorMessage(null)
    setShowConfirmModal(false)

    try {
      const res = await commitImportBatch(batch.id)
      setCommitResult(res)
      if (onCommitted) {
        onCommitted(res)
      }
      await refreshBatch()
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Failed to commit import batch.')
    } finally {
      setIsSubmitting(false)
    }
  }

  // Filtered rows
  const filteredRows = rows.filter((r) => {
    switch (activeFilter) {
      case 'errors':
        return r.validationStatus === 'Invalid' && r.resolution !== 'Excluded'
      case 'duplicates':
        return r.duplicateStatus !== 'None'
      case 'selected':
        return r.isSelected
      case 'excluded':
        return r.resolution === 'Excluded' || !r.isSelected
      default:
        return true
    }
  })

  const hasUnresolvedIssues =
    batch.errorCount > 0 ||
    rows.some((r) => r.duplicateStatus !== 'None' && r.resolution === 'None')

  const canCommit =
    batch.status !== 'Committed' &&
    batch.readyCount > 0 &&
    batch.selectedRowCount > 0 &&
    !hasUnresolvedIssues

  if (commitResult) {
    return (
      <div data-testid="commit-success-screen" style={{ background: '#fff', border: '1px solid #86efac', borderRadius: 8, padding: '2rem', marginTop: '1.5rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '1rem' }}>
          <div style={{ background: '#dcfce7', color: '#15803d', width: 44, height: 44, borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1.5rem', fontWeight: 'bold' }}>
            ✓
          </div>
          <div>
            <h2 style={{ margin: 0, color: '#166534', fontSize: '1.25rem' }}>Import Completed Successfully</h2>
            <p style={{ margin: '4px 0 0 0', color: '#64748b', fontSize: '0.9rem' }}>
              Ledger transactions have been persisted and import batch marked Committed.
            </p>
          </div>
        </div>

        <div style={{ background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: 6, padding: '1rem', marginBottom: '1.5rem' }}>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '1rem', fontSize: '0.9rem' }}>
            <div>
              <span style={{ color: '#64748b', display: 'block', fontSize: '0.8rem' }}>Target Account</span>
              <strong>{batch.accountName} ({batch.accountCurrency})</strong>
            </div>
            <div>
              <span style={{ color: '#64748b', display: 'block', fontSize: '0.8rem' }}>Imported Transactions</span>
              <strong data-testid="result-imported-count" style={{ color: '#16a34a' }}>{commitResult.importedCount}</strong>
            </div>
            <div>
              <span style={{ color: '#64748b', display: 'block', fontSize: '0.8rem' }}>Excluded Rows</span>
              <strong>{commitResult.excludedCount}</strong>
            </div>
            <div>
              <span style={{ color: '#64748b', display: 'block', fontSize: '0.8rem' }}>Committed At</span>
              <strong>{new Date(commitResult.committedAt).toLocaleString()}</strong>
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '1rem' }}>
          <button
            type="button"
            data-testid="return-to-statements-btn"
            onClick={onBack}
            style={{
              background: '#2563eb',
              color: '#fff',
              border: 'none',
              borderRadius: 6,
              padding: '0.6rem 1.25rem',
              fontWeight: 500,
              cursor: 'pointer',
            }}
          >
            ← Back to Statements
          </button>
        </div>
      </div>
    )
  }

  return (
    <div data-testid="import-batch-review-container" style={{ marginTop: '1.5rem' }}>
      {/* Header & Breadcrumb */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
        <div>
          <button
            type="button"
            onClick={onBack}
            style={{ background: 'none', border: 'none', color: '#2563eb', cursor: 'pointer', padding: 0, fontSize: '0.9rem', marginBottom: '0.25rem' }}
          >
            ← Back to Statement Upload
          </button>
          <h2 style={{ margin: 0, fontSize: '1.3rem' }}>
            Review Import Batch: <span style={{ fontWeight: 400 }}>{batch.originalFileName}</span>
          </h2>
          <p style={{ margin: '4px 0 0 0', color: '#64748b', fontSize: '0.85rem' }}>
            Target Account: <strong>{batch.accountName}</strong> ({batch.accountCurrency})
          </p>
        </div>

        <div style={{ textAlign: 'right' }}>
          <span
            data-testid="batch-status-badge"
            style={{
              display: 'inline-block',
              padding: '0.3rem 0.75rem',
              borderRadius: 6,
              fontWeight: 600,
              fontSize: '0.85rem',
              background:
                batch.status === 'Committed'
                  ? '#dcfce7'
                  : batch.status === 'ReadyToCommit'
                  ? '#eff6ff'
                  : '#fef3c7',
              color:
                batch.status === 'Committed'
                  ? '#166534'
                  : batch.status === 'ReadyToCommit'
                  ? '#1d4ed8'
                  : '#b45309',
            }}
          >
            {batch.status === 'ReadyToCommit'
              ? 'Ready to Commit'
              : batch.status === 'ReviewRequired'
              ? 'Review Required'
              : batch.status}
          </span>
        </div>
      </div>

      {errorMessage && (
        <div data-testid="review-error-banner" style={{ background: '#fef2f2', border: '1px solid #f87171', color: '#991b1b', padding: '0.75rem 1rem', borderRadius: 6, marginBottom: '1rem', fontSize: '0.875rem' }}>
          <strong>Error:</strong> {errorMessage}
        </div>
      )}

      {/* Summary KPI Cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: '0.75rem', marginBottom: '1.5rem' }}>
        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: 6, padding: '0.75rem 1rem' }}>
          <span style={{ fontSize: '0.75rem', color: '#64748b', textTransform: 'uppercase', fontWeight: 600 }}>Total Rows</span>
          <div data-testid="stat-total-rows" style={{ fontSize: '1.4rem', fontWeight: 'bold', color: '#1e293b' }}>{batch.totalRowCount}</div>
        </div>

        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: 6, padding: '0.75rem 1rem' }}>
          <span style={{ fontSize: '0.75rem', color: '#64748b', textTransform: 'uppercase', fontWeight: 600 }}>Selected for Import</span>
          <div data-testid="stat-selected-rows" style={{ fontSize: '1.4rem', fontWeight: 'bold', color: '#2563eb' }}>{batch.selectedRowCount}</div>
        </div>

        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: 6, padding: '0.75rem 1rem' }}>
          <span style={{ fontSize: '0.75rem', color: '#64748b', textTransform: 'uppercase', fontWeight: 600 }}>Ready to Commit</span>
          <div data-testid="stat-ready-rows" style={{ fontSize: '1.4rem', fontWeight: 'bold', color: '#16a34a' }}>{batch.readyCount}</div>
        </div>

        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: 6, padding: '0.75rem 1rem' }}>
          <span style={{ fontSize: '0.75rem', color: '#64748b', textTransform: 'uppercase', fontWeight: 600 }}>Attention (Errors)</span>
          <div data-testid="stat-error-rows" style={{ fontSize: '1.4rem', fontWeight: 'bold', color: batch.errorCount > 0 ? '#b91c1c' : '#64748b' }}>{batch.errorCount}</div>
        </div>

        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: 6, padding: '0.75rem 1rem' }}>
          <span style={{ fontSize: '0.75rem', color: '#64748b', textTransform: 'uppercase', fontWeight: 600 }}>Possible Duplicates</span>
          <div data-testid="stat-duplicate-rows" style={{ fontSize: '1.4rem', fontWeight: 'bold', color: batch.duplicateCount > 0 ? '#d97706' : '#64748b' }}>{batch.duplicateCount}</div>
        </div>
      </div>

      {/* Safety Notice Banner */}
      <div style={{ background: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: 6, padding: '0.75rem 1rem', marginBottom: '1.5rem', fontSize: '0.85rem', color: '#1e40af' }}>
        <strong>Staging Review Mode:</strong> Transactions shown below are staged candidates. <strong>No ledger entries have been created</strong>.
        Resolve any errors or duplicates, verify your selections, and click <strong>Commit Import</strong> when ready.
      </div>

      {/* Filter Tabs & Bulk Actions */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem', marginBottom: '1rem' }}>
        <div style={{ display: 'flex', gap: '0.35rem' }}>
          {(['all', 'errors', 'duplicates', 'selected', 'excluded'] as FilterType[]).map((filter) => (
            <button
              key={filter}
              type="button"
              data-testid={`filter-${filter}`}
              onClick={() => setActiveFilter(filter)}
              style={{
                background: activeFilter === filter ? '#2563eb' : '#f1f5f9',
                color: activeFilter === filter ? '#fff' : '#475569',
                border: 'none',
                borderRadius: 4,
                padding: '0.35rem 0.75rem',
                fontSize: '0.8rem',
                fontWeight: 500,
                cursor: 'pointer',
              }}
            >
              {filter.charAt(0).toUpperCase() + filter.slice(1)}
              {filter === 'errors' && batch.errorCount > 0 && ` (${batch.errorCount})`}
              {filter === 'duplicates' && batch.duplicateCount > 0 && ` (${batch.duplicateCount})`}
            </button>
          ))}
        </div>

        {batch.status !== 'Committed' && (
          <div style={{ display: 'flex', gap: '0.5rem' }}>
            <button
              type="button"
              onClick={() => handleBulkAction('selectAllValid')}
              disabled={isSubmitting}
              style={{ background: '#f8fafc', border: '1px solid #cbd5e1', borderRadius: 4, padding: '0.35rem 0.65rem', fontSize: '0.75rem', cursor: 'pointer' }}
            >
              Select All Valid
            </button>
            <button
              type="button"
              onClick={() => handleBulkAction('excludeAllDuplicates')}
              disabled={isSubmitting}
              style={{ background: '#f8fafc', border: '1px solid #cbd5e1', borderRadius: 4, padding: '0.35rem 0.65rem', fontSize: '0.75rem', cursor: 'pointer' }}
            >
              Exclude All Duplicates
            </button>
            <button
              type="button"
              onClick={() => handleBulkAction('unselectAll')}
              disabled={isSubmitting}
              style={{ background: '#f8fafc', border: '1px solid #cbd5e1', borderRadius: 4, padding: '0.35rem 0.65rem', fontSize: '0.75rem', cursor: 'pointer' }}
            >
              Clear Selections
            </button>
          </div>
        )}
      </div>

      {/* Candidate Rows Table */}
      <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: 6, overflowX: 'auto', marginBottom: '1.5rem' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem', textAlign: 'left' }}>
          <thead>
            <tr style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0' }}>
              <th style={{ padding: '0.6rem', width: 40, textAlign: 'center' }}>Import?</th>
              <th style={{ padding: '0.6rem', width: 50 }}>Row</th>
              <th style={{ padding: '0.6rem', width: 100 }}>Date</th>
              <th style={{ padding: '0.6rem' }}>Description</th>
              <th style={{ padding: '0.6rem', width: 80 }}>Type</th>
              <th style={{ padding: '0.6rem', width: 110, textAlign: 'right' }}>Amount ({batch.accountCurrency})</th>
              <th style={{ padding: '0.6rem' }}>Diagnostics / Duplicate Status</th>
              <th style={{ padding: '0.6rem', width: 180, textAlign: 'right' }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {filteredRows.length === 0 ? (
              <tr>
                <td colSpan={8} style={{ padding: '2rem', textAlign: 'center', color: '#94a3b8' }}>
                  No rows match the selected filter.
                </td>
              </tr>
            ) : (
              filteredRows.map((row) => {
                const isInvalid = row.validationStatus === 'Invalid' && row.resolution !== 'Excluded'
                const isDup = row.duplicateStatus !== 'None' && row.resolution === 'None'

                return (
                  <tr
                    key={row.id}
                    data-testid={`staging-row-${row.sourceRowNumber}`}
                    style={{
                      borderBottom: '1px solid #f1f5f9',
                      background: isInvalid
                        ? '#fff5f5'
                        : isDup
                        ? '#fffbeb'
                        : !row.isSelected
                        ? '#fcfcfc'
                        : '#fff',
                    }}
                  >
                    <td style={{ padding: '0.6rem', textAlign: 'center' }}>
                      <input
                        type="checkbox"
                        checked={row.isSelected}
                        disabled={batch.status === 'Committed' || isSubmitting}
                        onChange={() => handleToggleSelect(row)}
                        title={row.isSelected ? 'Row is selected for import' : 'Row is excluded from import'}
                      />
                    </td>
                    <td style={{ padding: '0.6rem', color: '#64748b' }}>{row.sourceRowNumber}</td>
                    <td style={{ padding: '0.6rem' }}>
                      {row.effectiveDate || '—'}
                      {row.resolvedTransactionDate && (
                        <span style={{ fontSize: '0.7rem', color: '#2563eb', display: 'block' }}>(corrected)</span>
                      )}
                    </td>
                    <td style={{ padding: '0.6rem' }}>
                      {row.effectiveDescription || '—'}
                      {row.resolvedDescription && (
                        <span style={{ fontSize: '0.7rem', color: '#2563eb', display: 'block' }}>(corrected)</span>
                      )}
                    </td>
                    <td style={{ padding: '0.6rem' }}>
                      <span
                        style={{
                          display: 'inline-block',
                          padding: '0.15rem 0.4rem',
                          borderRadius: 4,
                          fontSize: '0.7rem',
                          fontWeight: 600,
                          background: row.effectiveType === 'Credit' ? '#dcfce7' : '#fee2e2',
                          color: row.effectiveType === 'Credit' ? '#15803d' : '#b91c1c',
                        }}
                      >
                        {row.effectiveType || '—'}
                      </span>
                    </td>
                    <td style={{ padding: '0.6rem', textAlign: 'right', fontWeight: 500 }}>
                      {row.effectiveAmount != null
                        ? row.effectiveAmount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                        : '—'}
                    </td>
                    <td style={{ padding: '0.6rem' }}>
                      {row.validationStatus === 'Invalid' && (
                        <div style={{ color: '#b91c1c', fontSize: '0.75rem', fontWeight: 500 }}>
                          ⚠ {row.validationErrors || 'Validation failed'}
                        </div>
                      )}

                      {row.duplicateStatus !== 'None' && (
                        <div style={{ color: '#b45309', fontSize: '0.75rem', marginTop: '2px' }}>
                          ℹ {row.duplicateReason || 'Possible duplicate'}
                        </div>
                      )}

                      {row.resolution !== 'None' && (
                        <div style={{ marginTop: '2px' }}>
                          <span
                            style={{
                              display: 'inline-block',
                              padding: '0.1rem 0.35rem',
                              borderRadius: 4,
                              fontSize: '0.65rem',
                              fontWeight: 600,
                              background:
                                row.resolution === 'ApprovedAsIs'
                                  ? '#dbeafe'
                                  : row.resolution === 'Excluded'
                                  ? '#f1f5f9'
                                  : '#fef3c7',
                              color:
                                row.resolution === 'ApprovedAsIs'
                                  ? '#1d4ed8'
                                  : row.resolution === 'Excluded'
                                  ? '#475569'
                                  : '#92400e',
                            }}
                          >
                            {row.resolution === 'ApprovedAsIs'
                              ? 'Approved As Is'
                              : row.resolution === 'ManuallyCorrected'
                              ? 'Corrected'
                              : 'Excluded'}
                          </span>
                          {row.resolutionNotes && (
                            <span style={{ fontSize: '0.7rem', color: '#64748b', marginLeft: '4px' }}>
                              {row.resolutionNotes}
                            </span>
                          )}
                        </div>
                      )}
                    </td>
                    <td style={{ padding: '0.6rem', textAlign: 'right' }}>
                      {batch.status !== 'Committed' && (
                        <div style={{ display: 'flex', gap: '0.35rem', justifyContent: 'flex-end' }}>
                          {row.duplicateStatus !== 'None' && row.resolution !== 'ApprovedAsIs' && (
                            <button
                              type="button"
                              data-testid={`approve-dup-btn-${row.sourceRowNumber}`}
                              onClick={() => handleApproveDuplicate(row)}
                              disabled={isSubmitting}
                              style={{ background: '#dbeafe', color: '#1d4ed8', border: 'none', borderRadius: 4, padding: '0.25rem 0.5rem', fontSize: '0.75rem', cursor: 'pointer', fontWeight: 500 }}
                            >
                              Approve
                            </button>
                          )}

                          <button
                            type="button"
                            data-testid={`edit-btn-${row.sourceRowNumber}`}
                            onClick={() => handleOpenEdit(row)}
                            disabled={isSubmitting}
                            style={{ background: '#f1f5f9', color: '#334155', border: '1px solid #cbd5e1', borderRadius: 4, padding: '0.25rem 0.5rem', fontSize: '0.75rem', cursor: 'pointer' }}
                          >
                            Edit
                          </button>

                          {row.resolution !== 'Excluded' ? (
                            <button
                              type="button"
                              data-testid={`exclude-btn-${row.sourceRowNumber}`}
                              onClick={() => handleExcludeRow(row)}
                              disabled={isSubmitting}
                              style={{ background: '#fee2e2', color: '#b91c1c', border: 'none', borderRadius: 4, padding: '0.25rem 0.5rem', fontSize: '0.75rem', cursor: 'pointer' }}
                            >
                              Exclude
                            </button>
                          ) : (
                            <button
                              type="button"
                              data-testid={`include-btn-${row.sourceRowNumber}`}
                              onClick={() => handleToggleSelect(row)}
                              disabled={isSubmitting}
                              style={{ background: '#dcfce7', color: '#15803d', border: 'none', borderRadius: 4, padding: '0.25rem 0.5rem', fontSize: '0.75rem', cursor: 'pointer' }}
                            >
                              Include
                            </button>
                          )}
                        </div>
                      )}
                    </td>
                  </tr>
                )
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Commit Trigger Footer */}
      {batch.status !== 'Committed' && (
        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: 8, padding: '1.25rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <h4 style={{ margin: '0 0 0.25rem 0', fontSize: '1rem' }}>Ready to Commit Import?</h4>
            <p style={{ margin: 0, fontSize: '0.85rem', color: '#64748b' }}>
              {batch.selectedRowCount} row(s) selected ({batch.readyCount} eligible). Excluded rows will not be committed.
            </p>
            {hasUnresolvedIssues && (
              <p style={{ margin: '4px 0 0 0', fontSize: '0.8rem', color: '#b91c1c', fontWeight: 500 }}>
                ⚠ You must resolve or exclude all errors and duplicates before committing.
              </p>
            )}
          </div>

          <div>
            <button
              type="button"
              data-testid="open-commit-modal-btn"
              disabled={!canCommit || isSubmitting}
              onClick={() => setShowConfirmModal(true)}
              style={{
                background: canCommit && !isSubmitting ? '#16a34a' : '#94a3b8',
                color: '#fff',
                border: 'none',
                borderRadius: 6,
                padding: '0.7rem 1.5rem',
                fontSize: '0.95rem',
                fontWeight: 600,
                cursor: canCommit && !isSubmitting ? 'pointer' : 'not-allowed',
              }}
            >
              {isSubmitting ? 'Processing...' : `Commit ${batch.readyCount} Transactions`}
            </button>
          </div>
        </div>
      )}

      {/* Confirmation Modal */}
      {showConfirmModal && (
        <div
          data-testid="commit-confirm-modal"
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: 'rgba(0,0,0,0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
          }}
        >
          <div style={{ background: '#fff', borderRadius: 8, maxWidth: 500, width: '90%', padding: '1.5rem', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }}>
            <h3 style={{ margin: '0 0 0.75rem 0', fontSize: '1.2rem', color: '#1e293b' }}>
              Confirm Transaction Commit
            </h3>

            <div style={{ background: '#fef2f2', border: '1px solid #fecaca', borderRadius: 6, padding: '0.75rem 1rem', marginBottom: '1rem', fontSize: '0.875rem', color: '#991b1b' }}>
              <strong>Permanent Ledger Warning:</strong> This operation will create{' '}
              <strong>{batch.readyCount}</strong> real ledger transactions in account{' '}
              <strong>'{batch.accountName}'</strong> ({batch.accountCurrency}). This cannot be automatically undone.
            </div>

            <p style={{ fontSize: '0.875rem', color: '#475569', marginBottom: '1.5rem' }}>
              Excluded and unresolved rows will remain staged in this batch for audit history and will not create ledger transactions.
            </p>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
              <button
                type="button"
                onClick={() => setShowConfirmModal(false)}
                disabled={isSubmitting}
                style={{ background: '#f1f5f9', color: '#475569', border: '1px solid #cbd5e1', borderRadius: 6, padding: '0.5rem 1rem', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                type="button"
                data-testid="confirm-commit-submit-btn"
                onClick={handleCommitConfirm}
                disabled={isSubmitting}
                style={{ background: '#16a34a', color: '#fff', border: 'none', borderRadius: 6, padding: '0.5rem 1.25rem', fontWeight: 600, cursor: 'pointer' }}
              >
                {isSubmitting ? 'Committing...' : 'Yes, Commit Transactions'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Row Edit Modal */}
      {editingRow && (
        <div
          data-testid="edit-row-modal"
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: 'rgba(0,0,0,0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
          }}
        >
          <div style={{ background: '#fff', borderRadius: 8, maxWidth: 450, width: '90%', padding: '1.5rem' }}>
            <h3 style={{ margin: '0 0 1rem 0', fontSize: '1.1rem' }}>
              Edit Staging Row #{editingRow.sourceRowNumber}
            </h3>

            <div style={{ marginBottom: '0.75rem' }}>
              <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 500, marginBottom: '0.25rem' }}>Date (yyyy-MM-dd):</label>
              <input
                data-testid="edit-input-date"
                type="text"
                value={editDate}
                onChange={(e) => setEditDate(e.target.value)}
                style={{ width: '100%', padding: '0.4rem', border: '1px solid #cbd5e1', borderRadius: 4 }}
              />
            </div>

            <div style={{ marginBottom: '0.75rem' }}>
              <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 500, marginBottom: '0.25rem' }}>Description:</label>
              <input
                data-testid="edit-input-desc"
                type="text"
                value={editDesc}
                onChange={(e) => setEditDesc(e.target.value)}
                style={{ width: '100%', padding: '0.4rem', border: '1px solid #cbd5e1', borderRadius: 4 }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 500, marginBottom: '0.25rem' }}>Amount ({batch.accountCurrency}):</label>
                <input
                  data-testid="edit-input-amount"
                  type="number"
                  step="0.01"
                  min="0.01"
                  value={editAmount}
                  onChange={(e) => setEditAmount(e.target.value)}
                  style={{ width: '100%', padding: '0.4rem', border: '1px solid #cbd5e1', borderRadius: 4 }}
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 500, marginBottom: '0.25rem' }}>Type:</label>
                <select
                  data-testid="edit-select-type"
                  value={editType}
                  onChange={(e) => setEditType(e.target.value as 'Debit' | 'Credit')}
                  style={{ width: '100%', padding: '0.4rem', border: '1px solid #cbd5e1', borderRadius: 4 }}
                >
                  <option value="Debit">Debit</option>
                  <option value="Credit">Credit</option>
                </select>
              </div>
            </div>

            <div style={{ marginBottom: '1.25rem' }}>
              <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 500, marginBottom: '0.25rem' }}>Resolution Notes:</label>
              <input
                data-testid="edit-input-notes"
                type="text"
                placeholder="Reason for change"
                value={editNotes}
                onChange={(e) => setEditNotes(e.target.value)}
                style={{ width: '100%', padding: '0.4rem', border: '1px solid #cbd5e1', borderRadius: 4 }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
              <button
                type="button"
                onClick={() => setEditingRow(null)}
                style={{ background: '#f1f5f9', color: '#475569', border: '1px solid #cbd5e1', borderRadius: 4, padding: '0.4rem 0.8rem', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                type="button"
                data-testid="save-edit-btn"
                onClick={handleSaveCorrection}
                style={{ background: '#2563eb', color: '#fff', border: 'none', borderRadius: 4, padding: '0.4rem 1rem', fontWeight: 500, cursor: 'pointer' }}
              >
                Save Correction
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
