import type { BudgetCategory, BudgetDetail, BudgetLine, CostElement, Project } from '@app/api'
import { http, HttpResponse } from 'msw'
import { server } from './server'

/** 偽 API が受け取ったリクエスト(検証用)。body は JSON を解析したもの。 */
export type RecordedRequest = { method: string; path: string; query: string; body: unknown }

type LineBody = {
  category: BudgetCategory
  projectId?: string | null
  elementCode?: string | null
  amount: number
  monthlyAmounts?: Record<string, number> | null
  periodDetail?: string | null
}

export const DEPARTMENT_ID = 'dept-1'
export const BUDGET_ID = 'budget-1'

/** 標準の費目マスタ(人件費・ライセンス費)。 */
export const 標準の費目: CostElement[] = [
  { code: 'PERSONNEL', name: '人件費' },
  { code: 'LICENSE', name: 'ライセンス費' },
]

export const 案件 = (id: string, code: string, name: string): Project => ({
  id,
  departmentId: DEPARTMENT_ID,
  code,
  name,
  createdAt: '2026-04-01T00:00:00Z',
})

export const 明細 = (
  category: BudgetCategory,
  amount: number,
  opts: Partial<Pick<BudgetLine, 'projectId' | 'elementCode' | 'periodDetail'>> & {
    monthlyAmounts?: Record<string, number> | null
  } = {},
): BudgetLine => ({
  id: crypto.randomUUID(),
  category,
  projectId: opts.projectId ?? null,
  elementCode: opts.elementCode ?? null,
  periodDetail: opts.periodDetail ?? null,
  amount,
  isMonthly: opts.monthlyAmounts != null,
  monthlyAmounts: opts.monthlyAmounts ?? {},
})

const error = (message: string, status = 400) => HttpResponse.json({ error: message }, { status })

/**
 * 予算編集画面のための、メモリ上の偽バックエンド。
 * 明細の登録・削除・明細名の変更・承認・案件の追加と編集を受け付け、区分合計と計画損益を計算し直して返す。
 * 「1費目は費目一括か明細のどちらか」「承認済みは編集不可」「案件コードは課の中で一意」もサーバと同じく拒否する。
 */
export class 偽の予算API {
  readonly requests: RecordedRequest[] = []
  budget: BudgetDetail
  projects: Project[]
  elements: CostElement[]

  constructor(
    opts: {
      lines?: BudgetLine[]
      projects?: Project[]
      elements?: CostElement[]
      status?: BudgetDetail['status']
      fiscalHalf?: string
    } = {},
  ) {
    this.projects = opts.projects ?? []
    this.elements = opts.elements ?? 標準の費目
    this.budget = this.recalc({
      id: BUDGET_ID,
      departmentId: DEPARTMENT_ID,
      fiscalHalf: opts.fiscalHalf ?? '2026-H1',
      version: 1,
      label: '当初予算',
      status: opts.status ?? 'Draft',
      createdAt: '2026-04-01T00:00:00Z',
      approvedAt: null,
      revenueTotal: 0,
      processingTotal: 0,
      outsourcingTotal: 0,
      periodCostTotal: 0,
      plannedProfit: 0,
      lines: opts.lines ?? [],
    })
  }

  /** 書き込み系のリクエストだけ(GET を除く)。 */
  get writes(): RecordedRequest[] {
    return this.requests.filter((r) => r.method !== 'GET')
  }

  private recalc(b: BudgetDetail): BudgetDetail {
    const sum = (c: BudgetCategory) =>
      b.lines.filter((l) => l.category === c).reduce((s, l) => s + l.amount, 0)
    const revenueTotal = sum('Revenue')
    const processingTotal = sum('Processing')
    const outsourcingTotal = sum('Outsourcing')
    const periodCostTotal = sum('PeriodCost')
    return {
      ...b,
      revenueTotal,
      processingTotal,
      outsourcingTotal,
      periodCostTotal,
      plannedProfit: revenueTotal - processingTotal - outsourcingTotal - periodCostTotal,
    }
  }

  private same(l: BudgetLine, k: LineBody) {
    return (
      l.category === k.category &&
      (l.projectId ?? null) === (k.projectId ?? null) &&
      (l.elementCode ?? null) === (k.elementCode ?? null) &&
      (l.periodDetail ?? null) === (k.periodDetail ?? null)
    )
  }

  private async record(req: Request) {
    const url = new URL(req.url)
    const text = req.method === 'GET' ? '' : await req.text()
    this.requests.push({
      method: req.method,
      path: url.pathname,
      query: url.search,
      body: text ? JSON.parse(text) : undefined,
    })
    return this.requests[this.requests.length - 1]
  }

  /** MSW にハンドラを登録する。 */
  install(): this {
    server.use(
      http.get('/api/budgets/:id', async ({ request }) => {
        await this.record(request)
        return HttpResponse.json(this.budget)
      }),
      http.get('/api/departments/:id/projects', async ({ request }) => {
        await this.record(request)
        return HttpResponse.json(this.projects)
      }),
      http.get('/api/cost-elements', async ({ request }) => {
        await this.record(request)
        return HttpResponse.json(this.elements)
      }),

      http.put('/api/budgets/:id/lines', async ({ request }) => {
        const body = (await this.record(request)).body as LineBody
        if (this.budget.status !== 'Draft')
          return error('承認済みの予算は編集できません。改定版を作成してください。')
        if (body.category === 'PeriodCost') {
          const sameElement = this.budget.lines.filter(
            (l) => l.category === 'PeriodCost' && l.elementCode === body.elementCode,
          )
          if (body.periodDetail && sameElement.some((l) => l.periodDetail == null))
            return error(
              'この費目は費目一括で入力されています。明細を使うには先に費目一括の金額を削除してください。',
            )
          if (!body.periodDetail && sameElement.some((l) => l.periodDetail != null))
            return error(
              'この費目には明細があります。費目一括の金額ではなく明細で入力してください。',
            )
        }
        const monthly = body.monthlyAmounts ?? null
        const amount = monthly ? Object.values(monthly).reduce((s, v) => s + v, 0) : body.amount
        const next = 明細(body.category, amount, {
          projectId: body.projectId ?? null,
          elementCode: body.elementCode ?? null,
          periodDetail: body.periodDetail ?? null,
          monthlyAmounts: monthly,
        })
        this.budget = this.recalc({
          ...this.budget,
          lines: [...this.budget.lines.filter((l) => !this.same(l, body)), next],
        })
        return HttpResponse.json(this.budget)
      }),

      http.delete('/api/budgets/:id/lines', async ({ request }) => {
        await this.record(request)
        const q = new URL(request.url).searchParams
        const key: LineBody = {
          category: q.get('category') as BudgetCategory,
          projectId: q.get('projectId'),
          elementCode: q.get('elementCode'),
          periodDetail: q.get('periodDetail'),
          amount: 0,
        }
        this.budget = this.recalc({
          ...this.budget,
          lines: this.budget.lines.filter((l) => !this.same(l, key)),
        })
        return HttpResponse.json(this.budget)
      }),

      http.post('/api/budgets/:id/rename-period-detail', async ({ request }) => {
        const body = (await this.record(request)).body as {
          elementCode: string
          oldDetail: string
          newDetail: string
        }
        this.budget = {
          ...this.budget,
          lines: this.budget.lines.map((l) =>
            l.category === 'PeriodCost' &&
            l.elementCode === body.elementCode &&
            l.periodDetail === body.oldDetail
              ? { ...l, periodDetail: body.newDetail }
              : l,
          ),
        }
        return HttpResponse.json(this.budget)
      }),

      http.post('/api/budgets/:id/approve', async ({ request }) => {
        await this.record(request)
        this.budget = { ...this.budget, status: 'Approved', approvedAt: '2026-04-02T00:00:00Z' }
        return HttpResponse.json(this.budget)
      }),

      http.post('/api/departments/:id/projects', async ({ request }) => {
        const body = (await this.record(request)).body as { code: string; name: string }
        if (this.projects.some((p) => p.code === body.code))
          return error(`この課には既に案件コード '${body.code}' が存在します。`)
        const created = 案件(crypto.randomUUID(), body.code, body.name)
        this.projects = [...this.projects, created]
        return HttpResponse.json(created, { status: 201 })
      }),

      http.put('/api/projects/:id', async ({ request, params }) => {
        const body = (await this.record(request)).body as { code: string; name: string }
        this.projects = this.projects.map((p) => (p.id === params.id ? { ...p, ...body } : p))
        return HttpResponse.json(this.projects.find((p) => p.id === params.id))
      }),
    )
    return this
  }
}
