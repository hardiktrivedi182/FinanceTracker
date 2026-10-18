import { useState, useEffect, useCallback, type FormEvent } from 'react'
import type {
  AccountDto,
  AccountType,
  AccountValuationDto,
  CreateAccountRequest,
  HouseholdNetWorthReportDto,
  OwnershipType,
  UpdateValuationRequest,
} from '../types/account'
import {
  fetchAccounts,
  createAccount,
  deactivateAccount,
  recordValuation,
  updateValuation,
  fetchValuations,
  fetchNetWorth,
} from '../api/accountsApi'

import { devLogin } from '../api/authApi'

const getTodayIsoDate = () => new Date().toISOString().split('T')[0]

export interface AccountManagerProps {
  currentUserId?: string
  onUserSwitched?: (userId: string) => void
}

export function AccountManager({ currentUserId, onUserSwitched }: AccountManagerProps = {}) {
  const [accounts, setAccounts] = useState<AccountDto[]>([])
  const [netWorthReport, setNetWorthReport] = useState<HouseholdNetWorthReportDto | null>(null)
  const [asOfDate, setAsOfDate] = useState<string>(getTodayIsoDate)
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)
  const [ownershipFilter, setOwnershipFilter] = useState<'ALL' | 'Joint' | 'Individual'>('ALL')
  const [includeInactive, setIncludeInactive] = useState<boolean>(false)

  // Modals state
  const [showAddModal, setShowAddModal] = useState<boolean>(false)
  const [selectedAccountForValuation, setSelectedAccountForValuation] = useState<AccountDto | null>(null)
  const [selectedAccountForHistory, setSelectedAccountForHistory] = useState<AccountDto | null>(null)
  const [valuationsHistory, setValuationsHistory] = useState<AccountValuationDto[]>([])
  const [loadingHistory, setLoadingHistory] = useState<boolean>(false)
  const [editingValuation, setEditingValuation] = useState<AccountValuationDto | null>(null)

  // Add Account Form State
  const [formData, setFormData] = useState<CreateAccountRequest>(() => ({
    name: '',
    accountType: 'Savings',
    ownershipType: 'Joint',
    ownerUserId: 'partner-1',
    currencyCode: 'INR',
    initialBalance: 0,
    initialValuationDate: getTodayIsoDate(),
  }))

  // Record Valuation Form State
  const [valuationAmount, setValuationAmount] = useState<number>(0)
  const [valuationDate, setValuationDate] = useState<string>(getTodayIsoDate)
  const [valuationNotes, setValuationNotes] = useState<string>('')
  const [valuationSourceRef, setValuationSourceRef] = useState<string>('')

  // Edit Valuation Form State
  const [editAmount, setEditAmount] = useState<number>(0)
  const [editDate, setEditDate] = useState<string>('')
  const [editNotes, setEditNotes] = useState<string>('')
  const [editSourceRef, setEditSourceRef] = useState<string>('')

  // Dev user state
  const [currentUser, setCurrentUser] = useState<string>(() => currentUserId || 'partner-1')

  const loadData = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      const [accountsData, netWorthData] = await Promise.all([
        fetchAccounts(includeInactive),
        fetchNetWorth(asOfDate).catch(() => null),
      ])
      setAccounts(accountsData)
      setNetWorthReport(netWorthData)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Error loading financial data')
    } finally {
      setLoading(false)
    }
  }, [includeInactive, asOfDate])

  useEffect(() => {
    let ignore = false
    const run = async () => {
      try {
        setLoading(true)
        setError(null)
        const [accs, nw] = await Promise.all([
          fetchAccounts(includeInactive),
          fetchNetWorth(asOfDate).catch(() => null),
        ])
        if (!ignore) {
          setAccounts(accs)
          setNetWorthReport(nw)
        }
      } catch (err: unknown) {
        if (!ignore) {
          setError(err instanceof Error ? err.message : 'Error loading financial data')
        }
      } finally {
        if (!ignore) {
          setLoading(false)
        }
      }
    }
    void run()
    return () => {
      ignore = true
    }
  }, [includeInactive, asOfDate, currentUser])

  const handleDevUserSwitch = async (userId: string) => {
    try {
      await devLogin(userId)
      setCurrentUser(userId)
      onUserSwitched?.(userId)
      await loadData()
    } catch {
      setCurrentUser(userId)
    }
  }

  const handleCreateAccount = async (e: FormEvent) => {
    e.preventDefault()
    try {
      setError(null)
      const currency = formData.currencyCode.trim().toUpperCase()
      if (!/^[A-Z]{3}$/.test(currency)) {
        setError('Currency code must be a 3-letter ISO 4217 ASCII code (e.g., INR, USD, EUR).')
        return
      }

      const payload: CreateAccountRequest = {
        ...formData,
        currencyCode: currency,
        ownerUserId: formData.ownershipType === 'Individual' ? (formData.ownerUserId || currentUser) : null,
      }

      await createAccount(payload)
      setShowAddModal(false)
      setFormData({
        name: '',
        accountType: 'Savings',
        ownershipType: 'Joint',
        ownerUserId: currentUser,
        currencyCode: 'INR',
        initialBalance: 0,
        initialValuationDate: getTodayIsoDate(),
      })
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create account')
    }
  }

  const handleDeactivate = async (id: string, name: string) => {
    if (!window.confirm(`Are you sure you want to deactivate "${name}"?`)) return
    try {
      setError(null)
      await deactivateAccount(id)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to deactivate account')
    }
  }

  const handleRecordValuation = async (e: FormEvent) => {
    e.preventDefault()
    if (!selectedAccountForValuation) return
    try {
      setError(null)
      await recordValuation(selectedAccountForValuation.id, {
        amount: Number(valuationAmount),
        valuationDate: valuationDate,
        notes: valuationNotes,
        sourceReference: valuationSourceRef,
      })
      setSelectedAccountForValuation(null)
      setValuationAmount(0)
      setValuationNotes('')
      setValuationSourceRef('')
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to record valuation')
    }
  }

  const openHistoryModal = async (account: AccountDto) => {
    setSelectedAccountForHistory(account)
    setLoadingHistory(true)
    try {
      const history = await fetchValuations(account.id)
      setValuationsHistory(history)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load valuation history')
    } finally {
      setLoadingHistory(false)
    }
  }

  const startEditValuation = (val: AccountValuationDto) => {
    setEditingValuation(val)
    setEditAmount(val.amount)
    setEditDate(val.valuationDate)
    setEditNotes(val.notes || '')
    setEditSourceRef(val.sourceReference || '')
  }

  const handleSaveEditedValuation = async (e: FormEvent) => {
    e.preventDefault()
    if (!selectedAccountForHistory || !editingValuation) return
    try {
      setError(null)
      const updateData: UpdateValuationRequest = {
        amount: Number(editAmount),
        valuationDate: editDate,
        notes: editNotes,
        sourceReference: editSourceRef,
      }
      await updateValuation(selectedAccountForHistory.id, editingValuation.id, updateData)
      setEditingValuation(null)
      // refresh history
      const updatedHistory = await fetchValuations(selectedAccountForHistory.id)
      setValuationsHistory(updatedHistory)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update valuation')
    }
  }

  const filteredAccounts = accounts.filter((a) => {
    if (ownershipFilter === 'ALL') return true
    return a.ownershipType === ownershipFilter
  })

  return (
    <div className="account-manager" style={{ marginTop: '1.5rem' }}>
      {/* User Switcher bar for Household Context - strictly development only */}
      {Boolean(import.meta.env.DEV) && (
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: '#eef2f6', padding: '0.75rem 1rem', borderRadius: 8, marginBottom: '1.5rem' }}>
          <div>
            <strong>Household Context:</strong> Two-User Household (Partner 1 &amp; Partner 2)
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <span>Active User:</span>
            <button
              type="button"
              style={{ fontWeight: currentUser === 'partner-1' ? 'bold' : 'normal', background: currentUser === 'partner-1' ? '#2563eb' : '#fff', color: currentUser === 'partner-1' ? '#fff' : '#000', border: '1px solid #ccc', borderRadius: 4, padding: '4px 8px', cursor: 'pointer' }}
              onClick={() => handleDevUserSwitch('partner-1')}
            >
              Partner 1 (Admin)
            </button>
            <button
              type="button"
              style={{ fontWeight: currentUser === 'partner-2' ? 'bold' : 'normal', background: currentUser === 'partner-2' ? '#2563eb' : '#fff', color: currentUser === 'partner-2' ? '#fff' : '#000', border: '1px solid #ccc', borderRadius: 4, padding: '4px 8px', cursor: 'pointer' }}
              onClick={() => handleDevUserSwitch('partner-2')}
            >
              Partner 2 (Member)
            </button>
          </div>
        </div>
      )}

      {/* As-Of Date Control & Staleness Indicator */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: '#f8fafc', padding: '0.75rem 1rem', borderRadius: 8, border: '1px solid #e2e8f0', marginBottom: '1.5rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <label htmlFor="asOfDateInput" style={{ fontWeight: 600 }}>Valuation As-Of Date:</label>
          <input
            id="asOfDateInput"
            type="date"
            value={asOfDate}
            onChange={(e) => setAsOfDate(e.target.value)}
            style={{ padding: '4px 8px', borderRadius: 4, border: '1px solid #cbd5e1' }}
          />
          <button
            type="button"
            onClick={() => setAsOfDate(getTodayIsoDate())}
            style={{ padding: '4px 8px', borderRadius: 4, border: '1px solid #cbd5e1', background: '#fff', cursor: 'pointer', fontSize: '0.85rem' }}
          >
            Reset to Today
          </button>
        </div>
        <div style={{ fontSize: '0.875rem', color: '#64748b' }}>
          Staleness Rule: &gt; 90 days without dated valuation
        </div>
      </div>

      {/* Net Worth Summary Cards per Currency */}
      {Array.isArray(netWorthReport?.currencies) && netWorthReport.currencies.length > 0 ? (
        <div style={{ marginBottom: '1.5rem' }}>
          <h3 style={{ margin: '0 0 0.75rem 0' }}>Consolidated Household Net Worth (As of {netWorthReport.asOfDate})</h3>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '1rem' }}>
            {netWorthReport.currencies.map((curr) => (
              <div key={curr.currencyCode} style={{ padding: '1.25rem', background: '#fff', borderRadius: 8, border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid #f1f5f9', paddingBottom: '0.5rem', marginBottom: '0.75rem' }}>
                  <span style={{ fontWeight: 700, fontSize: '1.1rem', color: '#0f172a' }}>{curr.currencyCode} Net Worth</span>
                  <span style={{ fontSize: '1.4rem', fontWeight: 800, color: curr.netWorth >= 0 ? '#16a34a' : '#dc2626' }}>
                    {curr.currencyCode} {curr.netWorth.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem', fontSize: '0.875rem', marginBottom: '0.75rem' }}>
                  <div>
                    <span style={{ color: '#64748b' }}>Assets: </span>
                    <strong style={{ color: '#0284c7' }}>{curr.totalAssets.toLocaleString(undefined, { minimumFractionDigits: 2 })}</strong>
                  </div>
                  <div>
                    <span style={{ color: '#64748b' }}>Liabilities: </span>
                    <strong style={{ color: '#ea580c' }}>{curr.totalLiabilities.toLocaleString(undefined, { minimumFractionDigits: 2 })}</strong>
                  </div>
                </div>
                <div style={{ background: '#f8fafc', padding: '0.5rem 0.75rem', borderRadius: 6, fontSize: '0.8rem', color: '#475569' }}>
                  <div><strong>Ownership Allocation:</strong></div>
                  <div>Joint: {curr.ownershipBreakdown.jointTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</div>
                  <div>Partner 1 Total Share: {curr.ownershipBreakdown.partner1TotalShare.toLocaleString(undefined, { minimumFractionDigits: 2 })} (Indiv: {curr.ownershipBreakdown.partner1IndividualTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })})</div>
                  <div>Partner 2 Total Share: {curr.ownershipBreakdown.partner2TotalShare.toLocaleString(undefined, { minimumFractionDigits: 2 })} (Indiv: {curr.ownershipBreakdown.partner2IndividualTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })})</div>
                </div>
                {curr.accounts.some((a) => a.hasMissingValuation) && (
                  <div style={{ marginTop: '0.5rem', background: '#fef3c7', color: '#92400e', padding: '0.35rem 0.5rem', borderRadius: 4, fontSize: '0.75rem', fontWeight: 500 }}>
                    ⚠️ {curr.accounts.filter((a) => a.hasMissingValuation).length} account(s) have no valuation on or before {netWorthReport.asOfDate} (counted as 0.00).
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      ) : null}

      {/* Controls Bar */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem', flexWrap: 'wrap', gap: '1rem' }}>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
          <label htmlFor="ownership-filter" style={{ fontWeight: 500 }}>Ownership:</label>
          <select
            id="ownership-filter"
            value={ownershipFilter}
            onChange={(e) => setOwnershipFilter(e.target.value as 'ALL' | 'Joint' | 'Individual')}
            style={{ padding: '4px 8px', borderRadius: 4 }}
          >
            <option value="ALL">All Ownerships</option>
            <option value="Joint">Joint Only</option>
            <option value="Individual">Individual Only</option>
          </select>

          <label style={{ marginLeft: '1rem', display: 'flex', alignItems: 'center', gap: '4px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(e) => setIncludeInactive(e.target.checked)}
            />
            Show Inactive Accounts
          </label>
        </div>

        <button
          type="button"
          data-testid="add-account-btn"
          onClick={() => setShowAddModal(true)}
          style={{ background: '#16a34a', color: '#fff', border: 'none', borderRadius: 4, padding: '8px 16px', fontWeight: 600, cursor: 'pointer' }}
        >
          + Add Account / Holding
        </button>
      </div>

      {/* Error Message */}
      {error && (
        <div style={{ background: '#fee2e2', color: '#b91c1c', padding: '0.75rem 1rem', borderRadius: 6, marginBottom: '1rem' }}>
          {error}
        </div>
      )}

      {/* Accounts Table */}
      {loading ? (
        <p>Loading accounts...</p>
      ) : filteredAccounts.length === 0 ? (
        <div style={{ padding: '2rem', textAlign: 'center', background: '#f8fafc', borderRadius: 8, border: '1px dashed #cbd5e1' }}>
          <p style={{ margin: 0, color: '#64748b' }}>No financial accounts found. Click &quot;Add Account / Holding&quot; above to begin.</p>
        </div>
      ) : (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', background: '#fff', borderRadius: 8, overflow: 'hidden', boxShadow: '0 1px 3px rgba(0,0,0,0.1)' }}>
            <thead>
              <tr style={{ background: '#f1f5f9', color: '#475569', fontSize: '0.875rem' }}>
                <th style={{ padding: '10px 14px' }}>Name</th>
                <th style={{ padding: '10px 14px' }}>Type</th>
                <th style={{ padding: '10px 14px' }}>Ownership</th>
                <th style={{ padding: '10px 14px' }}>Latest Balance</th>
                <th style={{ padding: '10px 14px' }}>Valuation Date</th>
                <th style={{ padding: '10px 14px' }}>Status</th>
                <th style={{ padding: '10px 14px', textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredAccounts.map((acc) => (
                <tr key={acc.id} style={{ borderBottom: '1px solid #e2e8f0', opacity: acc.isActive ? 1 : 0.6 }}>
                  <td style={{ padding: '12px 14px', fontWeight: 500 }}>
                    {acc.name}
                    {!acc.isActive && <span style={{ marginLeft: 6, fontSize: '0.75rem', background: '#e2e8f0', padding: '2px 6px', borderRadius: 4 }}>Inactive</span>}
                  </td>
                  <td style={{ padding: '12px 14px' }}>
                    <span style={{ fontSize: '0.8rem', background: '#e0f2fe', color: '#0369a1', padding: '2px 8px', borderRadius: 4 }}>
                      {acc.accountType}
                    </span>
                  </td>
                  <td style={{ padding: '12px 14px' }}>
                    {acc.ownershipType === 'Joint' ? (
                      <span style={{ color: '#047857', fontWeight: 500 }}>Joint</span>
                    ) : (
                      <span style={{ color: '#4338ca' }}>Individual ({acc.ownerUserId})</span>
                    )}
                  </td>
                  <td style={{ padding: '12px 14px', fontWeight: 600 }}>
                    {acc.latestBalance !== null ? (
                      <span>
                        {acc.currencyCode} {acc.latestBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                      </span>
                    ) : (
                      <span style={{ color: '#94a3b8' }}>No valuations</span>
                    )}
                  </td>
                  <td style={{ padding: '12px 14px', fontSize: '0.875rem' }}>
                    {acc.latestValuationDate ? (
                      <span style={{ color: acc.isStale ? '#dc2626' : '#475569', fontWeight: acc.isStale ? 600 : 'normal' }}>
                        {acc.latestValuationDate}
                        {acc.isStale && <span style={{ marginLeft: 4, color: '#dc2626', fontSize: '0.75rem' }}>(Stale)</span>}
                      </span>
                    ) : (
                      <span style={{ color: '#94a3b8' }}>—</span>
                    )}
                  </td>
                  <td style={{ padding: '12px 14px' }}>
                    <span style={{ fontSize: '0.8rem', padding: '2px 6px', borderRadius: 4, background: acc.isActive ? '#dcfce7' : '#f1f5f9', color: acc.isActive ? '#15803d' : '#64748b' }}>
                      {acc.isActive ? 'Active' : 'Closed'}
                    </span>
                  </td>
                  <td style={{ padding: '12px 14px', textAlign: 'right' }}>
                    <div style={{ display: 'inline-flex', gap: '0.5rem' }}>
                      <button
                        type="button"
                        onClick={() => openHistoryModal(acc)}
                        style={{ background: '#f8fafc', color: '#475569', border: '1px solid #cbd5e1', borderRadius: 4, padding: '4px 8px', fontSize: '0.8rem', cursor: 'pointer' }}
                      >
                        History
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setSelectedAccountForValuation(acc)
                          setValuationAmount(acc.latestBalance || 0)
                          setValuationDate(getTodayIsoDate())
                        }}
                        style={{ background: '#0284c7', color: '#fff', border: 'none', borderRadius: 4, padding: '4px 8px', fontSize: '0.8rem', cursor: 'pointer' }}
                      >
                        + Valuation
                      </button>
                      {acc.isActive && (
                        <button
                          type="button"
                          onClick={() => handleDeactivate(acc.id, acc.name)}
                          style={{ background: '#fff', color: '#dc2626', border: '1px solid #fecaca', borderRadius: 4, padding: '4px 8px', fontSize: '0.8rem', cursor: 'pointer' }}
                        >
                          Deactivate
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Add Account Modal */}
      {showAddModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ background: '#fff', padding: '1.5rem', borderRadius: 8, width: '100%', maxWidth: 480, maxHeight: '90vh', overflowY: 'auto' }}>
            <h3 style={{ marginTop: 0 }}>Add Financial Account / Asset</h3>
            <form onSubmit={handleCreateAccount}>
              <div style={{ marginBottom: '1rem' }}>
                <label style={{ display: 'block', marginBottom: 4, fontWeight: 500 }}>Account / Asset Name:</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. HDFC Main Savings, EPF Account, NPS Tier 1"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: 4, border: '1px solid #ccc', boxSizing: 'border-box' }}
                />
              </div>

              <div style={{ marginBottom: '1rem' }}>
                <label style={{ display: 'block', marginBottom: 4, fontWeight: 500 }}>Asset / Liability Type:</label>
                <select
                  value={formData.accountType}
                  onChange={(e) => setFormData({ ...formData, accountType: e.target.value as AccountType })}
                  style={{ width: '100%', padding: '8px', borderRadius: 4, border: '1px solid #ccc', boxSizing: 'border-box' }}
                >
                  <option value="Savings">Savings Account</option>
                  <option value="Checking">Checking Account</option>
                  <option value="FixedDeposit">Fixed Deposit (FD)</option>
                  <option value="Ppf">Public Provident Fund (PPF)</option>
                  <option value="Epf">Employees Provident Fund (EPF)</option>
                  <option value="Nps">National Pension System (NPS)</option>
                  <option value="Investment">Investment (Stocks/Mutual Funds)</option>
                  <option value="CreditCard">Credit Card</option>
                  <option value="Loan">Loan / Mortgage</option>
                  <option value="Other">Other</option>
                </select>
              </div>

              <div style={{ marginBottom: '1rem' }}>
                <label style={{ display: 'block', marginBottom: 4, fontWeight: 500 }}>Ownership Scope:</label>
                <select
                  value={formData.ownershipType}
                  onChange={(e) => setFormData({ ...formData, ownershipType: e.target.value as OwnershipType })}
                  style={{ width: '100%', padding: '8px', borderRadius: 4, border: '1px solid #ccc', boxSizing: 'border-box' }}
                >
                  <option value="Joint">Joint (Co-owned by both household members)</option>
                  <option value="Individual">Individual (Legally/operationally owned by one member)</option>
                </select>
              </div>

              {formData.ownershipType === 'Individual' && (
                <div style={{ marginBottom: '1rem' }}>
                  <label style={{ display: 'block', marginBottom: 4, fontWeight: 500 }}>Owner Member:</label>
                  <select
                    value={formData.ownerUserId || currentUser}
                    onChange={(e) => setFormData({ ...formData, ownerUserId: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: 4, border: '1px solid #ccc', boxSizing: 'border-box' }}
                  >
                    <option value="partner-1">Partner 1</option>
                    <option value="partner-2">Partner 2</option>
                  </select>
                </div>
              )}

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '0.75rem', marginBottom: '1rem' }}>
                <div>
                  <label style={{ display: 'block', marginBottom: 4, fontWeight: 500 }}>Currency:</label>
                  <input
                    type="text"
                    required
                    maxLength={3}
                    value={formData.currencyCode}
                    onChange={(e) => setFormData({ ...formData, currencyCode: e.target.value.toUpperCase() })}
                    style={{ width: '100%', padding: '8px', borderRadius: 4, border: '1px solid #ccc', boxSizing: 'border-box' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', marginBottom: 4, fontWeight: 500 }}>Initial Balance / Value:</label>
                  <input
                    type="number"
                    step="0.01"
                    value={formData.initialBalance || ''}
                    onChange={(e) => setFormData({ ...formData, initialBalance: parseFloat(e.target.value) || 0 })}
                    style={{ width: '100%', padding: '8px', borderRadius: 4, border: '1px solid #ccc', boxSizing: 'border-box' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '1.5rem' }}>
                <button
                  type="button"
                  onClick={() => setShowAddModal(false)}
                  style={{ padding: '8px 16px', borderRadius: 4, border: '1px solid #ccc', background: '#f8fafc', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  style={{ padding: '8px 16px', borderRadius: 4, border: 'none', background: '#16a34a', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  Save Account
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Record Valuation Modal */}
      {selectedAccountForValuation && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ background: '#fff', padding: '1.5rem', borderRadius: 8, width: '100%', maxWidth: 420 }}>
            <h3 style={{ marginTop: 0 }}>Record Dated Valuation</h3>
            <p style={{ color: '#64748b', fontSize: '0.9rem' }}>Account: <strong>{selectedAccountForValuation.name}</strong> ({selectedAccountForValuation.currencyCode})</p>
            <form onSubmit={handleRecordValuation}>
              <div style={{ marginBottom: '1rem' }}>
                <label style={{ display: 'block', marginBottom: 4, fontWeight: 500 }}>Valuation Date:</label>
                <input
                  type="date"
                  required
                  value={valuationDate}
                  onChange={(e) => setValuationDate(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: 4, border: '1px solid #ccc', boxSizing: 'border-box' }}
                />
              </div>

              <div style={{ marginBottom: '1rem' }}>
                <label style={{ display: 'block', marginBottom: 4, fontWeight: 500 }}>Amount ({selectedAccountForValuation.currencyCode}):</label>
                <input
                  type="number"
                  step="0.01"
                  required
                  value={valuationAmount}
                  onChange={(e) => setValuationAmount(parseFloat(e.target.value) || 0)}
                  style={{ width: '100%', padding: '8px', borderRadius: 4, border: '1px solid #ccc', boxSizing: 'border-box' }}
                />
              </div>

              <div style={{ marginBottom: '1rem' }}>
                <label style={{ display: 'block', marginBottom: 4, fontWeight: 500 }}>Source Reference / Passbook No:</label>
                <input
                  type="text"
                  placeholder="e.g. Passbook-2026, Statement-06-2026"
                  value={valuationSourceRef}
                  onChange={(e) => setValuationSourceRef(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: 4, border: '1px solid #ccc', boxSizing: 'border-box' }}
                />
              </div>

              <div style={{ marginBottom: '1rem' }}>
                <label style={{ display: 'block', marginBottom: 4, fontWeight: 500 }}>Notes / Provenance:</label>
                <input
                  type="text"
                  placeholder="e.g. Monthly EPF passbook update, FD renewal"
                  value={valuationNotes}
                  onChange={(e) => setValuationNotes(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: 4, border: '1px solid #ccc', boxSizing: 'border-box' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '1.5rem' }}>
                <button
                  type="button"
                  onClick={() => setSelectedAccountForValuation(null)}
                  style={{ padding: '8px 16px', borderRadius: 4, border: '1px solid #ccc', background: '#f8fafc', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  style={{ padding: '8px 16px', borderRadius: 4, border: 'none', background: '#0284c7', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  Save Valuation
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Valuation History & Audit Modal */}
      {selectedAccountForHistory && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ background: '#fff', padding: '1.5rem', borderRadius: 8, width: '100%', maxWidth: 700, maxHeight: '85vh', overflowY: 'auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
              <h3 style={{ margin: 0 }}>Valuation History: {selectedAccountForHistory.name}</h3>
              <button
                type="button"
                onClick={() => {
                  setSelectedAccountForHistory(null)
                  setEditingValuation(null)
                }}
                style={{ background: 'none', border: 'none', fontSize: '1.25rem', cursor: 'pointer' }}
              >
                ✕
              </button>
            </div>

            {loadingHistory ? (
              <p>Loading history...</p>
            ) : valuationsHistory.length === 0 ? (
              <p style={{ color: '#64748b' }}>No valuations recorded for this account yet.</p>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
                <thead>
                  <tr style={{ background: '#f1f5f9', borderBottom: '1px solid #cbd5e1' }}>
                    <th style={{ padding: '8px', textAlign: 'left' }}>Date</th>
                    <th style={{ padding: '8px', textAlign: 'left' }}>Amount</th>
                    <th style={{ padding: '8px', textAlign: 'left' }}>Source</th>
                    <th style={{ padding: '8px', textAlign: 'left' }}>Audit Trail</th>
                    <th style={{ padding: '8px', textAlign: 'right' }}>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {valuationsHistory.map((val) => (
                    <tr key={val.id} style={{ borderBottom: '1px solid #e2e8f0' }}>
                      <td style={{ padding: '8px', fontWeight: 500 }}>{val.valuationDate}</td>
                      <td style={{ padding: '8px', fontWeight: 600 }}>
                        {val.currencyCode} {val.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '8px' }}>
                        <div>{val.sourceType}</div>
                        {val.sourceReference && <div style={{ fontSize: '0.75rem', color: '#64748b' }}>Ref: {val.sourceReference}</div>}
                        {val.notes && <div style={{ fontSize: '0.75rem', color: '#475569', fontStyle: 'italic' }}>{val.notes}</div>}
                      </td>
                      <td style={{ padding: '8px', fontSize: '0.75rem', color: '#64748b' }}>
                        <div>Created: {val.createdByUserId} ({new Date(val.createdAt).toLocaleDateString()})</div>
                        {val.updatedByUserId && (
                          <div style={{ color: '#0369a1' }}>Updated: {val.updatedByUserId} ({val.updatedAt ? new Date(val.updatedAt).toLocaleDateString() : ''})</div>
                        )}
                      </td>
                      <td style={{ padding: '8px', textAlign: 'right' }}>
                        <button
                          type="button"
                          onClick={() => startEditValuation(val)}
                          style={{ padding: '4px 8px', fontSize: '0.75rem', borderRadius: 4, border: '1px solid #cbd5e1', background: '#fff', cursor: 'pointer' }}
                        >
                          Edit
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            {/* Inline Edit Form */}
            {editingValuation && (
              <div style={{ marginTop: '1.5rem', padding: '1rem', background: '#f8fafc', borderRadius: 6, border: '1px solid #cbd5e1' }}>
                <h4 style={{ margin: '0 0 0.75rem 0' }}>Edit Valuation ({editingValuation.valuationDate})</h4>
                <form onSubmit={handleSaveEditedValuation}>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginBottom: '0.75rem' }}>
                    <div>
                      <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 600 }}>Date:</label>
                      <input
                        type="date"
                        required
                        value={editDate}
                        onChange={(e) => setEditDate(e.target.value)}
                        style={{ width: '100%', padding: '6px', borderRadius: 4, border: '1px solid #cbd5e1', boxSizing: 'border-box' }}
                      />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 600 }}>Amount ({editingValuation.currencyCode}):</label>
                      <input
                        type="number"
                        step="0.01"
                        required
                        value={editAmount}
                        onChange={(e) => setEditAmount(parseFloat(e.target.value) || 0)}
                        style={{ width: '100%', padding: '6px', borderRadius: 4, border: '1px solid #cbd5e1', boxSizing: 'border-box' }}
                      />
                    </div>
                  </div>
                  <div style={{ marginBottom: '0.75rem' }}>
                    <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 600 }}>Notes:</label>
                    <input
                      type="text"
                      value={editNotes}
                      onChange={(e) => setEditNotes(e.target.value)}
                      style={{ width: '100%', padding: '6px', borderRadius: 4, border: '1px solid #cbd5e1', boxSizing: 'border-box' }}
                    />
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
                    <button
                      type="button"
                      onClick={() => setEditingValuation(null)}
                      style={{ padding: '6px 12px', borderRadius: 4, border: '1px solid #cbd5e1', background: '#fff', cursor: 'pointer' }}
                    >
                      Cancel
                    </button>
                    <button
                      type="submit"
                      style={{ padding: '6px 12px', borderRadius: 4, border: 'none', background: '#0284c7', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                    >
                      Update
                    </button>
                  </div>
                </form>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
