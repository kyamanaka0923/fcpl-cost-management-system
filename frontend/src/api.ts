// バックエンド API クライアント。DTO はサーバ側 (CostManagement.Application) と対応する。

import { authEnabled, getAccessToken, login } from './auth'
import type { components } from './api-schema'

/** 予算区分(バックエンドでは文字列。画面では4値に絞って扱う)。 */
export type BudgetCategory = 'Revenue' | 'Processing' | 'Outsourcing' | 'PeriodCost'
/** 予算の状態(策定中 / 承認済 / 失効)。 */
export type BudgetStatus = 'Draft' | 'Approved' | 'Superseded'

/**
 * バックエンドの DTO の型は OpenAPI 定義(docs/api/openapi.json)から生成した `api-schema.d.ts` を正とする。
 * 生成し直すには `npm run gen:api`。バックエンドで項目を変えると、ここで型エラーになって気づける。
 */
type Schemas = components['schemas']

/**
 * 生成された型の一部の項目を、画面で扱う狭い型(区分の4値など)に置き換える。
 * 置き換える項目はスキーマに実在し、元の型と互換でなければならない(名前の変更や型の変更は型エラーになる)。
 */
type Override<T, U extends { [K in keyof U]: K extends keyof T ? T[K] : never }> = Omit<
  T,
  keyof U
> &
  U

/** リクエスト本文: null を許す項目は省略可能にする(サーバは省略を null として受け取る)。 */
type RequestBody<T> = {
  [K in keyof T as null extends T[K] ? never : K]: T[K]
} & {
  [K in keyof T as null extends T[K] ? K : never]?: T[K]
}

/** 部。 */
export type Division = Schemas['DivisionDto']
/** 課(部に属する)。 */
export type Department = Schemas['DepartmentDto']
/** 区分ごとの部合計(予算・実績・差異)。 */
export type CategorySummary = Override<Schemas['CategorySummaryDto'], { category: BudgetCategory }>
/** 部の課別内訳の1行。 */
export type DepartmentSummaryLine = Override<
  Schemas['DepartmentSummaryLineDto'],
  { categories: CategorySummary[] }
>
/** 部の予実サマリ。 */
export type DivisionBudgetSummary = Override<
  Schemas['DivisionBudgetSummaryDto'],
  { categories: CategorySummary[]; departmentLines: DepartmentSummaryLine[] }
>
/** 案件(課に属するマスタ)。 */
export type Project = Schemas['ProjectDto']
/** 費目(期間費用のマスタ)。 */
export type CostElement = Schemas['CostElementDto']
/** 予算明細。monthlyAmounts は月インデックス(1..6)→ 金額。 */
export type BudgetLine = Override<Schemas['BudgetLineDto'], { category: BudgetCategory }>
/** 予算の一覧用サマリ(区分合計・計画損益つき)。 */
export type BudgetSummary = Override<Schemas['BudgetSummaryDto'], { status: BudgetStatus }>
/** 予算の詳細(明細つき)。 */
export type BudgetDetail = Override<
  Schemas['BudgetDetailDto'],
  { status: BudgetStatus; lines: BudgetLine[] }
>
/** 実績。month は半期内の月(1..6)、null は半期一括。 */
export type ActualEntry = Override<Schemas['ActualEntryDto'], { category: BudgetCategory }>
/** 予実差異の明細。 */
export type VarianceLine = Override<Schemas['VarianceLineDto'], { category: BudgetCategory }>
/** 区分ごとの予実差異。 */
export type CategoryVariance = Override<
  Schemas['CategoryVarianceDto'],
  { category: BudgetCategory; lines: VarianceLine[] }
>
/** 予実差異分析の結果。 */
export type VarianceReport = Override<
  Schemas['VarianceReportDto'],
  { categories: CategoryVariance[] }
>
/** 予算バージョン間比較の明細。 */
export type BudgetComparisonLine = Override<
  Schemas['BudgetComparisonLineDto'],
  { category: BudgetCategory }
>
/** 区分ごとのバージョン間比較。 */
export type CategoryComparison = Override<
  Schemas['CategoryComparisonDto'],
  { category: BudgetCategory; lines: BudgetComparisonLine[] }
>
/** 予算バージョン間比較の結果。 */
export type BudgetComparison = Override<
  Schemas['BudgetComparisonDto'],
  { categories: CategoryComparison[] }
>
/** 案件別の損益。 */
export type ProjectProfitLine = Schemas['ProjectProfitLineDto']
/** 損益サマリ(課全体・案件別)。 */
export type ProfitSummary = Schemas['ProfitSummaryDto']

/** 予算明細の登録(月次モードなら monthlyAmounts に月インデックス → 金額)。 */
type UpsertBudgetLineBody = Override<
  RequestBody<Schemas['UpsertBudgetLineRequest']>,
  { category: BudgetCategory }
>
/** 実績の計上(month を指定すると特定月の計上)。 */
type RecordActualBody = Override<
  RequestBody<Schemas['RecordActualRequest']>,
  { category: BudgetCategory }
>

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (authEnabled) {
    const token = await getAccessToken()
    if (token) headers.Authorization = `Bearer ${token}`
  }
  const res = await fetch(`/api${path}`, { headers, ...init })
  if (res.status === 401 && authEnabled) {
    // トークン切れ・未ログイン: ログイン画面へ移動する
    void login()
    throw new Error('ログインが必要です。ログイン画面へ移動します。')
  }
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
  createDivision: (body: Schemas['CreateDivisionRequest']) =>
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
  createDepartment: (divisionId: string, body: Schemas['CreateDepartmentRequest']) =>
    request<Department>(`/divisions/${divisionId}/departments`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  // ---- 案件(課に属するマスタ) ----
  listProjects: (departmentId: string) =>
    request<Project[]>(`/departments/${departmentId}/projects`),
  createProject: (departmentId: string, body: Schemas['CreateProjectRequest']) =>
    request<Project>(`/departments/${departmentId}/projects`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  updateProject: (projectId: string, body: Schemas['UpdateProjectRequest']) =>
    request<Project>(`/projects/${projectId}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),

  // ---- 費目マスタ(期間費用) ----
  listCostElements: () => request<CostElement[]>('/cost-elements'),
  createCostElement: (body: Schemas['CreateCostElementRequest']) =>
    request<CostElement>('/cost-elements', { method: 'POST', body: JSON.stringify(body) }),

  // ---- 課予算 ----
  listBudgets: (departmentId: string, fiscalHalf: string) =>
    request<BudgetSummary[]>(
      `/departments/${departmentId}/budgets?fiscalHalf=${encodeURIComponent(fiscalHalf)}`,
    ),
  getBudget: (budgetId: string) => request<BudgetDetail>(`/budgets/${budgetId}`),
  createBudget: (departmentId: string, body: RequestBody<Schemas['CreateBudgetRequest']>) =>
    request<BudgetDetail>(`/departments/${departmentId}/budgets`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  upsertBudgetLine: (budgetId: string, body: UpsertBudgetLineBody) =>
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
  /** 期間費用の明細名を変更する(金額・月次は保持)。 */
  renamePeriodDetail: (budgetId: string, body: Schemas['RenamePeriodDetailRequest']) =>
    request<BudgetDetail>(`/budgets/${budgetId}/rename-period-detail`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  approveBudget: (budgetId: string) =>
    request<BudgetDetail>(`/budgets/${budgetId}/approve`, { method: 'POST' }),

  // ---- 実績 ----
  listActuals: (departmentId: string, fiscalHalf: string) =>
    request<ActualEntry[]>(
      `/departments/${departmentId}/actuals?fiscalHalf=${encodeURIComponent(fiscalHalf)}`,
    ),
  recordActual: (departmentId: string, body: RecordActualBody) =>
    request<ActualEntry>(`/departments/${departmentId}/actuals`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  deleteActual: (actualId: string) => request<void>(`/actuals/${actualId}`, { method: 'DELETE' }),

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

export const formatSignedYen = (value: number): string => (value > 0 ? '+' : '') + formatYen(value)

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
