export type AccountType =
  | 'Checking'
  | 'Savings'
  | 'CreditCard'
  | 'Investment'
  | 'Loan'
  | 'FixedDeposit'
  | 'Epf'
  | 'Ppf'
  | 'Nps'
  | 'Other'

export type OwnershipType = 'Joint' | 'Individual'

export interface AccountDto {
  id: string
  householdId: string
  name: string
  accountType: AccountType
  ownershipType: OwnershipType
  ownerUserId: string | null
  currencyCode: string
  isActive: boolean
  createdByUserId: string
  createdAt: string
  updatedAt: string
  latestBalance: number | null
  latestValuationDate: string | null
  isStale: boolean
}

export interface CreateAccountRequest {
  name: string
  accountType: AccountType
  ownershipType: OwnershipType
  ownerUserId?: string | null
  currencyCode: string
  initialBalance?: number | null
  initialValuationDate?: string | null
}

export interface RecordValuationRequest {
  valuationDate: string
  amount: number
  sourceType?: string
  notes?: string | null
  sourceReference?: string | null
}

export interface UpdateValuationRequest {
  valuationDate: string
  amount: number
  sourceType?: string
  notes?: string | null
  sourceReference?: string | null
}

export interface AccountValuationDto {
  id: string
  accountId: string
  valuationDate: string
  amount: number
  currencyCode: string
  sourceType: string
  sourceReference: string | null
  notes: string | null
  createdByUserId: string
  createdAt: string
  updatedByUserId?: string | null
  updatedAt?: string | null
}

export interface HouseholdNetWorthReportDto {
  asOfDate: string
  generatedAt: string
  stalenessThresholdDays: number
  currencies: CurrencyNetWorthDto[]
}

export interface CurrencyNetWorthDto {
  currencyCode: string
  totalAssets: number
  totalLiabilities: number
  netWorth: number
  ownershipBreakdown: HouseholdOwnershipBreakdownDto
  assetAllocation: AssetAllocationItemDto[]
  accounts: AccountNetWorthItemDto[]
}

export interface HouseholdOwnershipBreakdownDto {
  jointTotal: number
  partner1IndividualTotal: number
  partner2IndividualTotal: number
  partner1TotalShare: number
  partner2TotalShare: number
}

export interface AssetAllocationItemDto {
  accountType: AccountType
  accountTypeName: string
  totalAmount: number
  percentage: number
}

export interface AccountNetWorthItemDto {
  accountId: string
  accountName: string
  accountType: AccountType
  ownershipType: OwnershipType
  ownerUserId: string | null
  isLiability: boolean
  valuationDate: string | null
  amount: number
  isStale: boolean
  hasMissingValuation: boolean
  sourceType: string | null
}

export interface NetWorthComparisonDto {
  date1: string
  date2: string
  currencies: CurrencyComparisonDto[]
}

export interface CurrencyComparisonDto {
  currencyCode: string
  date1NetWorth: number
  date2NetWorth: number
  absoluteChange: number
  percentageChange: number | null
  date1Assets: number
  date2Assets: number
  date1Liabilities: number
  date2Liabilities: number
}
