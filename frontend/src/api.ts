// バックエンド API クライアント。DTO はサーバ側 (CostManagement.Application) と対応する。

export interface Project {
  id: string
  code: string
  name: string
  fiscalYear: number
  status: 'Active' | 'Completed'
  createdAt: string
}

export interface CostElement {
  code: string
  name: string
  type: 'Material' | 'Labor' | 'Overhead' | 'Expense'
}

export interface PlanLine {
  id: string
  elementCode: string
  revenueItem: string | null
  period: string
  amount: number
}

export interface CostPlanSummary {
  id: string
  projectId: string
  version: number
  label: string
  status: 'Draft' | 'Approved' | 'Superseded'
  createdAt: string
  approvedAt: string | null
  totalAmount: number
}

export interface CostPlanDetail extends CostPlanSummary {
  lines: PlanLine[]
}

export interface ActualCost {
  id: string
  projectId: string
  elementCode: string
  revenueItem: string | null
  period: string
  amount: number
  note: string | null
  recordedAt: string
}

export interface VarianceLine {
  elementCode: string
  revenueItem: string | null
  period: string
  plannedAmount: number
  actualAmount: number
  totalVariance: number
  isUnplanned: boolean
  isAdverse: boolean
}

export interface VarianceReport {
  planId: string
  planVersion: number
  planLabel: string
  lines: VarianceLine[]
  totalPlannedAmount: number
  totalActualAmount: number
  totalVariance: number
}

export interface PlanComparisonLine {
  elementCode: string
  revenueItem: string | null
  period: string
  baseAmount: number
  targetAmount: number
  difference: number
}

export interface PlanComparison {
  baseVersion: number
  baseLabel: string
  targetVersion: number
  targetLabel: string
  lines: PlanComparisonLine[]
  baseTotalAmount: number
  targetTotalAmount: number
  totalDifference: number
}

export interface RevenuePlanLine {
  id: string
  itemName: string
  period: string
  amount: number
}

export interface RevenuePlanSummary {
  id: string
  projectId: string
  version: number
  label: string
  status: 'Draft' | 'Approved' | 'Superseded'
  createdAt: string
  approvedAt: string | null
  totalAmount: number
}

export interface RevenuePlanDetail extends RevenuePlanSummary {
  lines: RevenuePlanLine[]
}

export interface ActualRevenue {
  id: string
  projectId: string
  itemName: string
  period: string
  amount: number
  note: string | null
  recordedAt: string
}

export interface RevenueVarianceLine {
  itemName: string
  period: string
  plannedAmount: number
  actualAmount: number
  totalVariance: number
  isUnplanned: boolean
  isFavorable: boolean
}

export interface RevenueVarianceReport {
  planId: string
  planVersion: number
  planLabel: string
  lines: RevenueVarianceLine[]
  totalPlannedAmount: number
  totalActualAmount: number
  totalVariance: number
}

export interface RevenuePlanComparisonLine {
  itemName: string
  period: string
  baseAmount: number
  targetAmount: number
  difference: number
}

export interface RevenuePlanComparison {
  baseVersion: number
  baseLabel: string
  targetVersion: number
  targetLabel: string
  lines: RevenuePlanComparisonLine[]
  baseTotalAmount: number
  targetTotalAmount: number
  totalDifference: number
}

export interface ProfitItemLine {
  itemName: string | null
  plannedRevenue: number
  actualRevenue: number
  plannedCost: number
  actualCost: number
  plannedProfit: number
  actualProfit: number
  profitVariance: number
}

export interface ProfitPeriodLine {
  period: string
  plannedRevenue: number
  actualRevenue: number
  plannedCost: number
  actualCost: number
  plannedProfit: number
  actualProfit: number
  profitVariance: number
}

export interface ProfitSummary {
  revenuePlanVersion: number
  revenuePlanLabel: string
  costPlanVersion: number
  costPlanLabel: string
  plannedRevenue: number
  actualRevenue: number
  revenueVariance: number
  plannedCost: number
  actualCost: number
  costVariance: number
  plannedProfit: number
  actualProfit: number
  profitVariance: number
  plannedMarginRate: number | null
  actualMarginRate: number | null
  itemLines: ProfitItemLine[]
  periodLines: ProfitPeriodLine[]
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
  listProjects: () => request<Project[]>('/projects'),
  getProject: (id: string) => request<Project>(`/projects/${id}`),
  createProject: (body: { code: string; name: string; fiscalYear: number }) =>
    request<Project>('/projects', { method: 'POST', body: JSON.stringify(body) }),

  listCostElements: () => request<CostElement[]>('/cost-elements'),
  listRevenueItems: (projectId: string) =>
    request<string[]>(`/projects/${projectId}/revenue-items`),

  // ---- 原価予算 ----
  listPlans: (projectId: string) =>
    request<CostPlanSummary[]>(`/projects/${projectId}/plans`),
  getPlan: (planId: string) => request<CostPlanDetail>(`/plans/${planId}`),
  createPlan: (projectId: string, body: { label: string; basePlanId?: string | null }) =>
    request<CostPlanDetail>(`/projects/${projectId}/plans`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  upsertPlanLine: (
    planId: string,
    body: { elementCode: string; revenueItem?: string | null; period: string; amount: number },
  ) =>
    request<CostPlanDetail>(`/plans/${planId}/lines`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  removePlanLine: (planId: string, elementCode: string, revenueItem: string | null, period: string) => {
    const params = new URLSearchParams({ elementCode, period })
    if (revenueItem) params.set('revenueItem', revenueItem)
    return request<CostPlanDetail>(`/plans/${planId}/lines?${params}`, { method: 'DELETE' })
  },
  approvePlan: (planId: string) =>
    request<CostPlanDetail>(`/plans/${planId}/approve`, { method: 'POST' }),

  // ---- 原価実績 ----
  listActuals: (projectId: string) =>
    request<ActualCost[]>(`/projects/${projectId}/actuals`),
  recordActual: (
    projectId: string,
    body: {
      elementCode: string
      revenueItem?: string | null
      period: string
      amount: number
      note?: string | null
    },
  ) =>
    request<ActualCost>(`/projects/${projectId}/actuals`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  deleteActual: (actualId: string) =>
    request<void>(`/actuals/${actualId}`, { method: 'DELETE' }),

  // ---- 売上予算 ----
  listRevenuePlans: (projectId: string) =>
    request<RevenuePlanSummary[]>(`/projects/${projectId}/revenue-plans`),
  getRevenuePlan: (planId: string) => request<RevenuePlanDetail>(`/revenue-plans/${planId}`),
  createRevenuePlan: (projectId: string, body: { label: string; basePlanId?: string | null }) =>
    request<RevenuePlanDetail>(`/projects/${projectId}/revenue-plans`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  upsertRevenuePlanLine: (
    planId: string,
    body: { itemName: string; period: string; amount: number },
  ) =>
    request<RevenuePlanDetail>(`/revenue-plans/${planId}/lines`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  removeRevenuePlanLine: (planId: string, itemName: string, period: string) =>
    request<RevenuePlanDetail>(
      `/revenue-plans/${planId}/lines?itemName=${encodeURIComponent(itemName)}&period=${encodeURIComponent(period)}`,
      { method: 'DELETE' },
    ),
  approveRevenuePlan: (planId: string) =>
    request<RevenuePlanDetail>(`/revenue-plans/${planId}/approve`, { method: 'POST' }),

  // ---- 売上実績 ----
  listActualRevenues: (projectId: string) =>
    request<ActualRevenue[]>(`/projects/${projectId}/actual-revenues`),
  recordActualRevenue: (
    projectId: string,
    body: { itemName: string; period: string; amount: number; note?: string | null },
  ) =>
    request<ActualRevenue>(`/projects/${projectId}/actual-revenues`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  deleteActualRevenue: (actualId: string) =>
    request<void>(`/actual-revenues/${actualId}`, { method: 'DELETE' }),

  // ---- 分析 ----
  getVariance: (projectId: string, opts?: { planId?: string; from?: string; to?: string }) => {
    const params = new URLSearchParams()
    if (opts?.planId) params.set('planId', opts.planId)
    if (opts?.from) params.set('from', opts.from)
    if (opts?.to) params.set('to', opts.to)
    const qs = params.toString()
    return request<VarianceReport>(`/projects/${projectId}/variance${qs ? `?${qs}` : ''}`)
  },
  getRevenueVariance: (
    projectId: string,
    opts?: { planId?: string; from?: string; to?: string },
  ) => {
    const params = new URLSearchParams()
    if (opts?.planId) params.set('planId', opts.planId)
    if (opts?.from) params.set('from', opts.from)
    if (opts?.to) params.set('to', opts.to)
    const qs = params.toString()
    return request<RevenueVarianceReport>(
      `/projects/${projectId}/revenue-variance${qs ? `?${qs}` : ''}`,
    )
  },
  comparePlans: (projectId: string, baseVersion: number, targetVersion: number) =>
    request<PlanComparison>(
      `/projects/${projectId}/plan-comparison?baseVersion=${baseVersion}&targetVersion=${targetVersion}`,
    ),
  compareRevenuePlans: (projectId: string, baseVersion: number, targetVersion: number) =>
    request<RevenuePlanComparison>(
      `/projects/${projectId}/revenue-plan-comparison?baseVersion=${baseVersion}&targetVersion=${targetVersion}`,
    ),
  getProfit: (projectId: string, opts?: { from?: string; to?: string }) => {
    const params = new URLSearchParams()
    if (opts?.from) params.set('from', opts.from)
    if (opts?.to) params.set('to', opts.to)
    const qs = params.toString()
    return request<ProfitSummary>(`/projects/${projectId}/profit${qs ? `?${qs}` : ''}`)
  },
}

export const formatYen = (value: number): string =>
  new Intl.NumberFormat('ja-JP', { maximumFractionDigits: 0 }).format(value)

export const formatSignedYen = (value: number): string =>
  (value > 0 ? '+' : '') + formatYen(value)

/** 売上対応品目の表示名(null = 共通費)。 */
export const revenueItemLabel = (item: string | null): string => item ?? '(共通)'
