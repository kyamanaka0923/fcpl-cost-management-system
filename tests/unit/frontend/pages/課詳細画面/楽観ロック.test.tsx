import type { BudgetSummary } from '@app/api'
import DepartmentDetailPage from '@app/pages/DepartmentDetailPage'
import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, test } from 'vitest'
import { DEPARTMENT_ID, 衝突の文言 } from '../../support/fakeBudgetApi'
import { 画面を開く } from '../../support/render'
import { server } from '../../support/server'

const ROUTE = '/departments/:departmentId'

/** 予算バージョンの一覧の 1 行(策定中の当初予算)。 */
const 予算の行 = (overrides: Partial<BudgetSummary> = {}): BudgetSummary => ({
  id: 'budget-1',
  departmentId: DEPARTMENT_ID,
  fiscalHalf: '2026-H1',
  version: 1,
  label: '当初予算',
  status: 'Draft',
  createdAt: '2026-04-01T00:00:00Z',
  approvedAt: null,
  revenueTotal: 1_000_000,
  processingTotal: 0,
  outsourcingTotal: 0,
  periodCostTotal: 0,
  plannedProfit: 1_000_000,
  updateNumber: 4,
  ...overrides,
})

/**
 * 課詳細画面のための偽 API。一覧は rows() の今の値を返し、承認の応答は approve で決める。
 * 受け取った承認のクエリと、一覧の読み込みの回数を記録する。
 */
function 偽の課API(opts: { rows: () => BudgetSummary[]; approve: () => Response }) {
  const state = { approveQueries: [] as string[], listCount: 0 }
  server.use(
    http.get('/api/departments/:id', () =>
      HttpResponse.json({
        id: DEPARTMENT_ID,
        divisionId: 'div-1',
        code: '開発1課',
        name: '開発1課',
        createdAt: '2026-04-01T00:00:00Z',
      }),
    ),
    http.get('/api/divisions/:id', () =>
      HttpResponse.json({
        id: 'div-1',
        code: 'SALES',
        name: '営業本部',
        createdAt: '2026-04-01T00:00:00Z',
      }),
    ),
    http.get('/api/departments/:id/budgets', () => {
      state.listCount++
      return HttpResponse.json(opts.rows())
    }),
    http.post('/api/budgets/:id/approve', ({ request }) => {
      state.approveQueries.push(new URL(request.url).search)
      return opts.approve()
    }),
  )
  return state
}

/** 予算バージョンの一覧で、名称が label の行。 */
const 行 = (label: string) => screen.getByRole('link', { name: label }).closest('tr') as HTMLElement

/** 予算バージョンの一覧で、名称が label の行の売上高のセル(Ver・名称・状態の次)。 */
const 売上高のセル = (label: string) => 行(label).querySelectorAll('td')[3]

// SPEC-004 の具体例の行 37(画面の処理 1・4)。
describe('課詳細画面: 予算の承認の楽観ロック', () => {
  test('承認には一覧で読んだその行の更新番号を付けて送る', async () => {
    let rows = [予算の行()]
    const api = 偽の課API({
      rows: () => rows,
      approve: () => {
        rows = [予算の行({ status: 'Approved', updateNumber: 5 })]
        return HttpResponse.json({ ...rows[0], lines: [] })
      },
    })
    const { user } = 画面を開く(
      <DepartmentDetailPage />,
      ROUTE,
      `/departments/${DEPARTMENT_ID}?half=2026-H1`,
    )

    await user.click(await screen.findByRole('button', { name: '承認' }))

    await waitFor(() => expect(within(行('当初予算')).getByText('承認済')).toBeInTheDocument())
    expect(api.approveQueries).toEqual(['?updateNumber=4'])
  })

  test('行37: 承認が衝突で拒否されると、衝突の文言を出し、一覧と各行の更新番号を読み込み直す', async () => {
    let rows = [予算の行()]
    let 承認の応答 = () => HttpResponse.json({ error: 衝突の文言 }, { status: 409 })
    const api = 偽の課API({ rows: () => rows, approve: () => 承認の応答() })
    const { user } = 画面を開く(
      <DepartmentDetailPage />,
      ROUTE,
      `/departments/${DEPARTMENT_ID}?half=2026-H1`,
    )
    await screen.findByRole('button', { name: '承認' })
    // 画面が読んだあとに、ほかの画面が明細を保存した
    rows = [予算の行({ revenueTotal: 1_200_000, plannedProfit: 1_200_000, updateNumber: 5 })]
    const 読み込みの前 = api.listCount

    await user.click(screen.getByRole('button', { name: '承認' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(衝突の文言)
    await waitFor(() => expect(売上高のセル('当初予算')).toHaveTextContent('¥1,200,000'))
    expect(api.listCount).toBe(読み込みの前 + 1)
    expect(within(行('当初予算')).getByText('策定中')).toBeInTheDocument()

    // 読み込み直した行の更新番号で、もう一度承認を送る
    承認の応答 = () => HttpResponse.json({ ...rows[0], status: 'Approved', lines: [] })
    await user.click(screen.getByRole('button', { name: '承認' }))
    await waitFor(() => expect(api.approveQueries).toEqual(['?updateNumber=4', '?updateNumber=5']))
  })

  test('衝突でない承認の拒否(400)は文言を出すだけで一覧を読み込み直さない', async () => {
    const api = 偽の課API({
      rows: () => [予算の行()],
      approve: () =>
        HttpResponse.json({ error: '明細のない予算は承認できません。' }, { status: 400 }),
    })
    const { user } = 画面を開く(
      <DepartmentDetailPage />,
      ROUTE,
      `/departments/${DEPARTMENT_ID}?half=2026-H1`,
    )
    await screen.findByRole('button', { name: '承認' })
    const 読み込みの前 = api.listCount

    await user.click(screen.getByRole('button', { name: '承認' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('明細のない予算は承認できません。')
    expect(api.listCount).toBe(読み込みの前)
  })
})
