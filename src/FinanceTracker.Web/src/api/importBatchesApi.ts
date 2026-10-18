import type {
  BulkUpdateRowsRequest,
  CommitImportBatchResultDto,
  CreateImportBatchRequest,
  ImportBatchDetailDto,
  ImportBatchSummaryDto,
  UpdateStagingRowRequest,
} from '../types/importBatch'
import { ensureXsrfToken } from './statementsApi'

async function getHeaders(): Promise<Record<string, string>> {
  const token = await ensureXsrfToken()
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  }
  if (token) {
    headers['X-XSRF-Token'] = token
  }
  return headers
}

export async function createImportBatch(statementId: string, accountId: string): Promise<ImportBatchDetailDto> {
  const headers = await getHeaders()
  const payload: CreateImportBatchRequest = { statementId, accountId }

  const res = await fetch('/api/import-batches', {
    method: 'POST',
    credentials: 'include',
    headers,
    body: JSON.stringify(payload),
  })

  if (!res.ok) {
    const errorBody = await res.json().catch(() => ({}))
    throw new Error(errorBody.error || errorBody.detail || `Failed to create import batch (${res.status})`)
  }

  return res.json()
}

export async function getImportBatch(id: string): Promise<ImportBatchDetailDto> {
  const res = await fetch(`/api/import-batches/${id}`, {
    credentials: 'include',
  })

  if (!res.ok) {
    const errorBody = await res.json().catch(() => ({}))
    throw new Error(errorBody.error || errorBody.detail || `Failed to fetch import batch (${res.status})`)
  }

  return res.json()
}

export async function getImportBatches(statementId?: string): Promise<ImportBatchSummaryDto[]> {
  const url = statementId
    ? `/api/import-batches?statementId=${encodeURIComponent(statementId)}`
    : '/api/import-batches'

  const res = await fetch(url, {
    credentials: 'include',
  })

  if (!res.ok) {
    throw new Error(`Failed to list import batches (${res.status})`)
  }

  return res.json()
}

export async function updateStagingRow(
  batchId: string,
  rowId: string,
  request: UpdateStagingRowRequest
): Promise<ImportBatchDetailDto> {
  const headers = await getHeaders()

  const res = await fetch(`/api/import-batches/${batchId}/rows/${rowId}`, {
    method: 'PUT',
    credentials: 'include',
    headers,
    body: JSON.stringify(request),
  })

  if (!res.ok) {
    const errorBody = await res.json().catch(() => ({}))
    throw new Error(errorBody.error || errorBody.detail || `Failed to update staging row (${res.status})`)
  }

  return res.json()
}

export async function bulkUpdateRows(
  batchId: string,
  request: BulkUpdateRowsRequest
): Promise<ImportBatchDetailDto> {
  const headers = await getHeaders()

  const res = await fetch(`/api/import-batches/${batchId}/rows/bulk`, {
    method: 'POST',
    credentials: 'include',
    headers,
    body: JSON.stringify(request),
  })

  if (!res.ok) {
    const errorBody = await res.json().catch(() => ({}))
    throw new Error(errorBody.error || errorBody.detail || `Failed to bulk update rows (${res.status})`)
  }

  return res.json()
}

export async function commitImportBatch(batchId: string): Promise<CommitImportBatchResultDto> {
  const headers = await getHeaders()

  const res = await fetch(`/api/import-batches/${batchId}/commit`, {
    method: 'POST',
    credentials: 'include',
    headers,
  })

  if (!res.ok) {
    const errorBody = await res.json().catch(() => ({}))
    throw new Error(errorBody.error || errorBody.detail || `Failed to commit import batch (${res.status})`)
  }

  return res.json()
}
