import type { ActualEntry, BudgetSummary } from '@app/api'
import ActualsPage from '@app/pages/ActualsPage'
import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, test } from 'vitest'
import { DEPARTMENT_ID, 標準の費目, 明細, 案件 } from '../support/fakeBudgetApi'
import { 画面を開く } from '../support/render'
import { server } from '../support/server'

const ROUTE = '/departments/:departmentId/actuals'

type RecordBody = Partial<ActualEntry> & { fiscalHalf: string }

/** 実績入力画面のための偽 API。計上した実績を一覧に返し、受け取ったリクエストを記録する。 */
function 偽の実績API(
  opts: {
    entries?: ActualEntry[]
    budgets?: BudgetSummary[]
    budgetLines?: ReturnType<typeof 明細>[]
  } = {},
) {
  const state = {
    entries: opts.entries ?? [],
    posted: [] as RecordBody[],
    deleted: [] as string[],
  }
  server.use(
    http.get('/api/departments/:id/actuals', () => HttpResponse.json(state.entries)),
    http.get('/api/departments/:id/projects', () =>
      HttpResponse.json([案件('pa', 'PJ-A', '案件A')]),
    ),
    http.get('/api/cost-elements', () => HttpResponse.json(標準の費目)),
    http.get('/api/departments/:id/budgets', () => HttpResponse.json(opts.budgets ?? [])),
    http.get('/api/budgets/:id', ({ params }) =>
      HttpResponse.json({ id: params.id, lines: opts.budgetLines ?? [] }),
    ),
    http.post('/api/departments/:id/actuals', async ({ request }) => {
      const body = (await request.json()) as RecordBody
      state.posted.push(body)
      const entry = 実績(body.category!, body.amount!, body)
      state.entries = [...state.entries, entry]
      return HttpResponse.json(entry, { status: 201 })
    }),
    http.delete('/api/actuals/:id', ({ params }) => {
      state.deleted.push(params.id as string)
      state.entries = state.entries.filter((e) => e.id !== params.id)
      return new HttpResponse(null, { status: 204 })
    }),
  )
  return state
}

function 実績(
  category: ActualEntry['category'],
  amount: number,
  opts: Partial<ActualEntry> = {},
): ActualEntry {
  return {
    id: crypto.randomUUID(),
    departmentId: DEPARTMENT_ID,
    fiscalHalf: '2026-H1',
    category,
    projectId: opts.projectId ?? null,
    elementCode: opts.elementCode ?? null,
    periodDetail: opts.periodDetail ?? null,
    month: opts.month ?? null,
    amount,
    note: opts.note ?? null,
    recordedAt: '2026-05-01T10:00:00Z',
  }
}

const 予算 = (id: string, status: BudgetSummary['status']) => ({ id, status }) as BudgetSummary

async function 実績入力画面を開く(half = '2026-H1') {
  const view = 画面を開く(
    <ActualsPage />,
    ROUTE,
    `/departments/${DEPARTMENT_ID}/actuals?half=${half}`,
  )
  await screen.findByRole('option', { name: '案件A(PJ-A)' })
  return view
}

describe('実績入力画面: 入力欄', () => {
  test('売上高・加工費・外注費は案件を、期間費用は費目と明細名を選ぶ', async () => {
    偽の実績API()
    const { user } = await 実績入力画面を開く()

    expect(screen.getByLabelText('案件')).toBeInTheDocument()
    expect(screen.queryByLabelText('費目')).not.toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('区分'), '期間費用')
    expect(screen.queryByLabelText('案件')).not.toBeInTheDocument()
    expect(screen.getByLabelText('費目')).toBeInTheDocument()
    expect(screen.getByLabelText('明細名')).toBeInTheDocument()
  })

  test.each([
    ['2026-H1', ['半期一括', '4月', '5月', '6月', '7月', '8月', '9月']],
    ['2026-H2', ['半期一括', '10月', '11月', '12月', '1月', '2月', '3月']],
  ])('%s の計上月は半期一括と半期の6か月から選ぶ', async (half, labels) => {
    偽の実績API()
    await 実績入力画面を開く(half)
    const options = within(screen.getByLabelText('月')).getAllByRole('option')
    expect(options.map((o) => o.textContent)).toEqual(labels)
  })

  test('明細名の候補には、承認済みの予算で計画した期間費用の明細名を出す', async () => {
    偽の実績API({
      budgets: [予算('draft', 'Draft'), 予算('approved', 'Approved')],
      budgetLines: [
        明細('PeriodCost', 300_000, { elementCode: 'LICENSE', periodDetail: 'AWS' }),
        明細('PeriodCost', 200_000, { elementCode: 'LICENSE', periodDetail: 'GitHub' }),
        明細('PeriodCost', 100_000, { elementCode: 'PERSONNEL' }), // 費目一括は候補にしない
      ],
    })
    const { user } = await 実績入力画面を開く()

    await user.selectOptions(screen.getByLabelText('区分'), '期間費用')
    await user.selectOptions(screen.getByLabelText('費目'), 'ライセンス費(LICENSE)')

    const list = document.getElementById('planned-details-LICENSE')!
    await waitFor(() =>
      expect([...list.querySelectorAll('option')].map((o) => o.value)).toEqual(['AWS', 'GitHub']),
    )
  })
})

describe('実績入力画面: 計上と一覧', () => {
  test('案件の売上高を半期一括で計上すると、金額と備考の欄は空に戻り、一覧に出る', async () => {
    const api = 偽の実績API()
    const { user } = await 実績入力画面を開く()

    await user.selectOptions(screen.getByLabelText('案件'), '案件A(PJ-A)')
    await user.type(screen.getByLabelText('金額(円)'), '2100000')
    await user.type(screen.getByLabelText('備考'), '検収')
    await user.click(screen.getByRole('button', { name: '計上' }))

    await waitFor(() => expect(screen.getByLabelText('金額(円)')).toHaveValue(''))
    expect(screen.getByLabelText('備考')).toHaveValue('')
    expect(api.posted).toEqual([
      {
        fiscalHalf: '2026-H1',
        category: 'Revenue',
        projectId: 'pa',
        elementCode: null,
        periodDetail: null,
        month: null,
        amount: 2_100_000,
        note: '検収',
      },
    ])
    const 行 = (await screen.findByText('案件A', { selector: 'td' })).closest('tr') as HTMLElement
    expect(行).toHaveTextContent('半期一括')
    expect(行).toHaveTextContent('¥2,100,000')
  })

  test('期間費用を明細名と計上月つきで計上する', async () => {
    const api = 偽の実績API()
    const { user } = await 実績入力画面を開く()

    await user.selectOptions(screen.getByLabelText('区分'), '期間費用')
    await user.selectOptions(screen.getByLabelText('費目'), 'ライセンス費(LICENSE)')
    await user.type(screen.getByLabelText('明細名'), 'AWS')
    await user.selectOptions(screen.getByLabelText('月'), '5月')
    await user.type(screen.getByLabelText('金額(円)'), '100000')
    await user.click(screen.getByRole('button', { name: '計上' }))

    await waitFor(() => expect(api.posted).toHaveLength(1))
    expect(api.posted[0]).toMatchObject({
      category: 'PeriodCost',
      projectId: null,
      elementCode: 'LICENSE',
      periodDetail: 'AWS',
      month: 2, // 半期内の位置(上期の5月は2か月目)
      note: null,
    })
    expect(await screen.findByText('ライセンス費 / AWS')).toBeInTheDocument()
  })

  test('明細名を空のまま期間費用を計上すると費目一括の実績になる', async () => {
    const api = 偽の実績API()
    const { user } = await 実績入力画面を開く()

    await user.selectOptions(screen.getByLabelText('区分'), '期間費用')
    await user.selectOptions(screen.getByLabelText('費目'), '人件費(PERSONNEL)')
    await user.type(screen.getByLabelText('金額(円)'), '320000')
    await user.click(screen.getByRole('button', { name: '計上' }))

    await waitFor(() => expect(api.posted).toHaveLength(1))
    expect(api.posted[0]).toMatchObject({ elementCode: 'PERSONNEL', periodDetail: null })
  })

  test('一覧は下期の計上月を暦の月で表示し、全区分の合計を出す', async () => {
    偽の実績API({
      entries: [
        実績('Revenue', 900_000, { projectId: 'pa', month: 4 }), // 下期の4か月目 = 1月
        実績('PeriodCost', 100_000, { elementCode: 'PERSONNEL' }),
      ],
    })
    await 実績入力画面を開く('2026-H2')

    const 行 = (await screen.findByText('案件A', { selector: 'td' })).closest('tr') as HTMLElement
    expect(行).toHaveTextContent('1月')
    expect(screen.getByText('合計(全区分)').closest('tr')).toHaveTextContent('¥1,000,000')
  })

  test('実績を削除すると一覧から消える', async () => {
    const entry = 実績('Revenue', 900_000, { projectId: 'pa' })
    const api = 偽の実績API({ entries: [entry] })
    const { user } = await 実績入力画面を開く()

    await user.click(await screen.findByRole('button', { name: '削除' }))

    await waitFor(() => expect(screen.getByText('実績がありません。')).toBeInTheDocument())
    expect(api.deleted).toEqual([entry.id])
  })

  test('計上に失敗したときはサーバのエラーメッセージをトーストで知らせ、入力は残す', async () => {
    偽の実績API()
    server.use(
      http.post('/api/departments/:id/actuals', () =>
        HttpResponse.json({ error: '指定された案件はこの課に属していません。' }, { status: 400 }),
      ),
    )
    const { user } = await 実績入力画面を開く()

    await user.selectOptions(screen.getByLabelText('案件'), '案件A(PJ-A)')
    await user.type(screen.getByLabelText('金額(円)'), '100000')
    await user.click(screen.getByRole('button', { name: '計上' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      '指定された案件はこの課に属していません。',
    )
    expect(screen.getByLabelText('金額(円)')).toHaveValue('100,000')
  })
})
