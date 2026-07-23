// バックエンド API クライアント。DTO はサーバ側 (CostManagement.Application) と対応する。

export type BudgetCategory = 'Revenue' | 'Processing' | 'Outsourcing' | 'PeriodCost'
export type BudgetStatus = 'Draft' | 'Approved' | 'Superseded'

export interface Division {
  id: string
  code: string
  name: string
  createdAt: string
}

export interface Department {
  id: string
  divisionId: string
  code: string
  name: string
  createdAt: string
}

export interface CategorySummary {
  category: BudgetCategory
  plannedAmount: number
  actualAmount: number
  variance: number
}

export interface DepartmentSummaryLine {
  departmentId: string
  departmentCode: string
  departmentName: string
  hasApprovedBudget: boolean
  plannedRevenue: number
  actualRevenue: number
  plannedCost: number
  actualCost: number
  plannedProfit: number
  actualProfit: number
  profitVariance: number
  categories: CategorySummary[]
}

export interface DivisionBudgetSummary {
  categories: CategorySummary[]
  plannedRevenue: number
  actualRevenue: number
  revenueVariance: number
  plannedCost: number
  actualCost: number
  costVariance: number
  plannedProfit: number
  actualProfit: number
  profitVariance: number
  departmentLines: DepartmentSummaryLine[]
  isApproved: boolean
  approvedAt: string | null
  canApprove: boolean
}

export interface Project {
  id: string
  departmentId: string
  code: string
  name: string
  createdAt: string
}

export interface CostElement {
  code: string
  name: string
}

export interface BudgetLine {
  id: string
  category: BudgetCategory
  projectId: string | null
  elementCode: string | null
  /** 期間費用の明細名(費目内を細分)。null = 費目一括。 */
  periodDetail: string | null
  amount: number
  isMonthly: boolean
  /** 月次モードの月別金額(月インデックス "1".."6" → 金額)。半期一括モードでは空。 */
  monthlyAmounts: Record<string, number>
}

export interface BudgetSummary {
  id: string
  departmentId: string
  fiscalHalf: string
  version: number
  label: string
  status: BudgetStatus
  createdAt: string
  approvedAt: string | null
  revenueTotal: number
  processingTotal: number
  outsourcingTotal: number
  periodCostTotal: number
  plannedProfit: number
}

export interface BudgetDetail extends BudgetSummary {
  lines: BudgetLine[]
}

export interface ActualEntry {
  id: string
  departmentId: string
  fiscalHalf: string
  category: BudgetCategory
  projectId: string | null
  elementCode: string | null
  /** 期間費用の明細名(計画明細に対する計上)。null = 費目一括。 */
  periodDetail: string | null
  /** 計上対象の月(半期内 1..6)。半期一括の計上は null。 */
  month: number | null
  amount: number
  note: string | null
  recordedAt: string
}

export interface VarianceLine {
  category: BudgetCategory
  projectId: string | null
  projectName: string | null
  elementCode: string | null
  elementName: string | null
  periodDetail: string | null
  plannedAmount: number
  actualAmount: number
  variance: number
  isUnplanned: boolean
  isFavorable: boolean
  isAdverse: boolean
}

export interface CategoryVariance {
  category: BudgetCategory
  lines: VarianceLine[]
  plannedAmount: number
  actualAmount: number
  variance: number
}

export interface VarianceReport {
  budgetId: string
  budgetVersion: number
  budgetLabel: string
  categories: CategoryVariance[]
  plannedRevenue: number
  actualRevenue: number
  revenueVariance: number
  plannedCost: number
  actualCost: number
  costVariance: number
}

export interface BudgetComparisonLine {
  category: BudgetCategory
  projectId: string | null
  projectName: string | null
  elementCode: string | null
  elementName: string | null
  periodDetail: string | null
  baseAmount: number
  targetAmount: number
  difference: number
}

export interface CategoryComparison {
  category: BudgetCategory
  lines: BudgetComparisonLine[]
  baseAmount: number
  targetAmount: number
  difference: number
}

export interface BudgetComparison {
  baseVersion: number
  baseLabel: string
  targetVersion: number
  targetLabel: string
  categories: CategoryComparison[]
}

export interface ProjectProfitLine {
  projectId: string
  projectCode: string
  projectName: string
  plannedRevenue: number
  actualRevenue: number
  plannedProcessing: number
  actualProcessing: number
  plannedOutsourcing: number
  actualOutsourcing: number
  plannedProfit: number
  actualProfit: number
  profitVariance: number
}

export interface ProfitSummary {
  budgetVersion: number
  budgetLabel: string
  plannedRevenue: number
  actualRevenue: number
  plannedTotalCost: number
  actualTotalCost: number
  plannedPeriodCost: number
  actualPeriodCost: number
  plannedProfit: number
  actualProfit: number
  profitVariance: number
  plannedMarginRate: number | null
  actualMarginRate: number | null
  projectLines: ProjectProfitLine[]
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`/api${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...init,
  })
  if (!res.ok) {
    let message = `${res.status} ${res.statusText}`
    try {
      const body = (await res.json()) as { error?: string }
      if (body.error) message = body.error
    } catch {
      // JSON でないエラー応答はステータス行をそのまま使う
    }
    throw new Error(message)
  }
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

export const api = {
  // ---- 部 ----
  listDivisions: () => request<Division[]>('/divisions'),
  getDivision: (id: string) => request<Division>(`/divisions/${id}`),
  createDivision: (body: { code: string; name: string }) =>
    request<Division>('/divisions', { method: 'POST', body: JSON.stringify(body) }),
  getDivisionBudgetSummary: (divisionId: string, fiscalHalf: string) =>
    request<DivisionBudgetSummary>(
      `/divisions/${divisionId}/budget-summary?fiscalHalf=${encodeURIComponent(fiscalHalf)}`,
    ),
  approveDivisionBudget: (divisionId: string, fiscalHalf: string) =>
    request<void>(
      `/divisions/${divisionId}/budget-approval?fiscalHalf=${encodeURIComponent(fiscalHalf)}`,
      { method: 'POST' },
    ),
  revokeDivisionBudget: (divisionId: string, fiscalHalf: string) =>
    request<void>(
      `/divisions/${divisionId}/budget-approval?fiscalHalf=${encodeURIComponent(fiscalHalf)}`,
      { method: 'DELETE' },
    ),

  // ---- 課(部に属する) ----
  listDepartments: (divisionId: string) =>
    request<Department[]>(`/divisions/${divisionId}/departments`),
  getDepartment: (id: string) => request<Department>(`/departments/${id}`),
  createDepartment: (divisionId: string, body: { code: string; name: string }) =>
    request<Department>(`/divisions/${divisionId}/departments`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  // ---- 案件(課に属するマスタ) ----
  listProjects: (departmentId: string) =>
    request<Project[]>(`/departments/${departmentId}/projects`),
  createProject: (departmentId: string, body: { code: string; name: string }) =>
    request<Project>(`/departments/${departmentId}/projects`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  updateProject: (projectId: string, body: { code: string; name: string }) =>
    request<Project>(`/projects/${projectId}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),

  // ---- 費目マスタ(期間費用) ----
  listCostElements: () => request<CostElement[]>('/cost-elements'),
  createCostElement: (body: { code: string; name: string }) =>
    request<CostElement>('/cost-elements', { method: 'POST', body: JSON.stringify(body) }),

  // ---- 課予算 ----
  listBudgets: (departmentId: string, fiscalHalf: string) =>
    request<BudgetSummary[]>(
      `/departments/${departmentId}/budgets?fiscalHalf=${encodeURIComponent(fiscalHalf)}`,
    ),
  getBudget: (budgetId: string) => request<BudgetDetail>(`/budgets/${budgetId}`),
  createBudget: (
    departmentId: string,
    body: { fiscalHalf: string; label: string; baseBudgetId?: string | null },
  ) =>
    request<BudgetDetail>(`/departments/${departmentId}/budgets`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  upsertBudgetLine: (
    budgetId: string,
    body: {
      category: BudgetCategory
      projectId?: string | null
      elementCode?: string | null
      amount: number
      /** 指定すると月次モード(月インデックス 1..6 → 金額)。未指定なら amount による半期一括。 */
      monthlyAmounts?: Record<number, number>
      /** 期間費用の明細名(費目内を細分)。未指定/空は費目一括。 */
      periodDetail?: string | null
    },
  ) =>
    request<BudgetDetail>(`/budgets/${budgetId}/lines`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  removeBudgetLine: (
    budgetId: string,
    category: BudgetCategory,
    projectId: string | null,
    elementCode: string | null,
    periodDetail?: string | null,
  ) => {
    const params = new URLSearchParams({ category })
    if (projectId) params.set('projectId', projectId)
    if (elementCode) params.set('elementCode', elementCode)
    if (periodDetail) params.set('periodDetail', periodDetail)
    return request<BudgetDetail>(`/budgets/${budgetId}/lines?${params}`, { method: 'DELETE' })
  },
  approveBudget: (budgetId: string) =>
    request<BudgetDetail>(`/budgets/${budgetId}/approve`, { method: 'POST' }),

  // ---- 実績 ----
  listActuals: (departmentId: string, fiscalHalf: string) =>
    request<ActualEntry[]>(
      `/departments/${departmentId}/actuals?fiscalHalf=${encodeURIComponent(fiscalHalf)}`,
    ),
  recordActual: (
    departmentId: string,
    body: {
      fiscalHalf: string
      category: BudgetCategory
      projectId?: string | null
      elementCode?: string | null
      amount: number
      /** 指定すると特定月の計上(半期内 1..6)。未指定なら半期一括。 */
      month?: number | null
      note?: string | null
      /** 期間費用の明細名(計画明細に対する計上)。未指定/空は費目一括。 */
      periodDetail?: string | null
    },
  ) =>
    request<ActualEntry>(`/departments/${departmentId}/actuals`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  deleteActual: (actualId: string) =>
    request<void>(`/actuals/${actualId}`, { method: 'DELETE' }),

  // ---- 分析 ----
  getVariance: (departmentId: string, fiscalHalf: string, opts?: { budgetId?: string }) => {
    const params = new URLSearchParams({ fiscalHalf })
    if (opts?.budgetId) params.set('budgetId', opts.budgetId)
    return request<VarianceReport>(`/departments/${departmentId}/variance?${params}`)
  },
  compareBudgets: (
    departmentId: string,
    fiscalHalf: string,
    baseVersion: number,
    targetVersion: number,
  ) => {
    const params = new URLSearchParams({
      fiscalHalf,
      baseVersion: String(baseVersion),
      targetVersion: String(targetVersion),
    })
    return request<BudgetComparison>(`/departments/${departmentId}/budget-comparison?${params}`)
  },
  getProfit: (departmentId: string, fiscalHalf: string, opts?: { budgetId?: string }) => {
    const params = new URLSearchParams({ fiscalHalf })
    if (opts?.budgetId) params.set('budgetId', opts.budgetId)
    return request<ProfitSummary>(`/departments/${departmentId}/profit?${params}`)
  },
}

export const formatYen = (value: number): string =>
  new Intl.NumberFormat('ja-JP', { maximumFractionDigits: 0 }).format(value)

export const formatSignedYen = (value: number): string =>
  (value > 0 ? '+' : '') + formatYen(value)

/** 粗利率などの割合表示。null / 分母0 は「—」。 */
export const formatPercent = (rate: number | null): string =>
  rate === null ? '—' : `${(rate * 100).toFixed(1)}%`

/** 粗利率 = 損益 ÷ 売上高。売上高が0なら null。 */
export const marginRate = (profit: number, revenue: number): number | null =>
  revenue === 0 ? null : profit / revenue

/** 予算区分の表示名。 */
export const categoryLabel: Record<BudgetCategory, string> = {
  Revenue: '売上高',
  Processing: '加工費',
  Outsourcing: '外注費',
  PeriodCost: '期間費用',
}

/** 案件別に明細を持つ区分(売上高・加工費・外注費)。 */
export const projectCategories: BudgetCategory[] = ['Revenue', 'Processing', 'Outsourcing']

/** "2026-H1" → "2026年度 上期" の表示名。 */
export const halfLabel = (fiscalHalf: string): string => {
  const [year, half] = fiscalHalf.split('-')
  return `${year}年度 ${half === 'H1' ? '上期' : '下期'}`
}

/**
 * 半期を構成する6ヶ月を、月インデックス(1..6)と暦月ラベルで返す。
 * 上期(H1)= 4〜9月、下期(H2)= 10〜3月。
 */
export const halfMonths = (fiscalHalf: string): { index: number; label: string }[] => {
  const isH1 = fiscalHalf.endsWith('H1')
  const startMonth = isH1 ? 4 : 10
  return Array.from({ length: 6 }, (_, i) => ({
    index: i + 1,
    label: `${((startMonth - 1 + i) % 12) + 1}月`,
  }))
}

/** 今日の日付が属する会計半期(年度は4月始まり: 4〜9月 = 上期、10〜3月 = 前年度の下期)。 */
export const currentFiscalHalf = (): string => {
  const now = new Date()
  const month = now.getMonth() + 1
  const year = now.getFullYear()
  if (month >= 4 && month <= 9) return `${year}-H1`
  return month >= 10 ? `${year}-H2` : `${year - 1}-H2`
}

/** 半期セレクタ用の候補(前年度〜翌年度の6半期)。 */
export const fiscalHalfOptions = (): string[] => {
  const currentYear = Number(currentFiscalHalf().split('-')[0])
  const options: string[] = []
  for (let y = currentYear - 1; y <= currentYear + 1; y++) {
    options.push(`${y}-H1`, `${y}-H2`)
  }
  return options
}
