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
  isQuantityManaged: boolean
}

export interface PlanLine {
  id: string
  elementCode: string
  period: string
  quantity: number
  unitPrice: number
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
  period: string
  quantity: number
  unitPrice: number
  amount: number
  note: string | null
  recordedAt: string
}

export interface VarianceLine {
  elementCode: string
  period: string
  plannedQuantity: number
  plannedUnitPrice: number
  plannedAmount: number
  actualQuantity: number
  actualUnitPrice: number
  actualAmount: number
  totalVariance: number
  priceVariance: number | null
  quantityVariance: number | null
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
    body: { elementCode: string; period: string; quantity: number; unitPrice: number },
  ) =>
    request<CostPlanDetail>(`/plans/${planId}/lines`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  removePlanLine: (planId: string, elementCode: string, period: string) =>
    request<CostPlanDetail>(
      `/plans/${planId}/lines?elementCode=${encodeURIComponent(elementCode)}&period=${encodeURIComponent(period)}`,
      { method: 'DELETE' },
    ),
  approvePlan: (planId: string) =>
    request<CostPlanDetail>(`/plans/${planId}/approve`, { method: 'POST' }),

  listActuals: (projectId: string) =>
    request<ActualCost[]>(`/projects/${projectId}/actuals`),
  recordActual: (
    projectId: string,
    body: {
      elementCode: string
      period: string
      quantity: number
      unitPrice: number
      note?: string | null
    },
  ) =>
    request<ActualCost>(`/projects/${projectId}/actuals`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  deleteActual: (actualId: string) =>
    request<void>(`/actuals/${actualId}`, { method: 'DELETE' }),

  getVariance: (projectId: string, opts?: { planId?: string; from?: string; to?: string }) => {
    const params = new URLSearchParams()
    if (opts?.planId) params.set('planId', opts.planId)
    if (opts?.from) params.set('from', opts.from)
    if (opts?.to) params.set('to', opts.to)
    const qs = params.toString()
    return request<VarianceReport>(`/projects/${projectId}/variance${qs ? `?${qs}` : ''}`)
  },
  comparePlans: (projectId: string, baseVersion: number, targetVersion: number) =>
    request<PlanComparison>(
      `/projects/${projectId}/plan-comparison?baseVersion=${baseVersion}&targetVersion=${targetVersion}`,
    ),
}

export const formatYen = (value: number): string =>
  new Intl.NumberFormat('ja-JP', { maximumFractionDigits: 0 }).format(value)

export const formatSignedYen = (value: number): string =>
  (value > 0 ? '+' : '') + formatYen(value)
