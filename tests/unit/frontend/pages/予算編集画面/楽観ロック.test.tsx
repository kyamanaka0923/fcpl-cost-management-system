import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, test } from 'vitest'
import { 偽の予算API, 明細, 衝突の文言 } from '../../support/fakeBudgetApi'
import { 案件A, 案件B, 予算編集画面を開く, タイル } from '../../support/budgetEditPage'
import { server } from '../../support/server'

/** 記録した書き込みのクエリ updateNumber。 */
const 送った番号 = (query: string) => new URLSearchParams(query).get('updateNumber')

/** 送られるはずのない要求が届く余地を与えるために、少し待つ。 */
const 少し待つ = () => new Promise((r) => setTimeout(r, 50))

/** 予算の読み込み(GET /api/budgets/:id)の回数。 */
const 読み込みの回数 = (fake: 偽の予算API) =>
  fake.requests.filter((r) => r.method === 'GET' && r.path === '/api/budgets/budget-1').length

// SPEC-004 の具体例の行 33〜36・38(画面の処理 1〜3・5)。
describe('予算編集画面: 予算への保存の楽観ロック', () => {
  test('行33: 読んだ予算の更新番号を付けて保存し、応答の更新番号を次の保存に付ける', async () => {
    const fake = new 偽の予算API({ projects: [案件A], updateNumber: 7 })
    const { user } = await 予算編集画面を開く(fake)

    await user.type(screen.getByLabelText('案件A 売上高', { exact: true }), '100')
    await user.tab()
    await waitFor(() => expect(タイル('売上高')).toHaveTextContent('¥100'))
    await user.type(screen.getByLabelText('案件A 加工費', { exact: true }), '50')
    await user.tab()
    await waitFor(() => expect(タイル('加工費')).toHaveTextContent('¥50'))

    expect(fake.writes.map((w) => 送った番号(w.query))).toEqual(['7', '8'])
  })

  test('削除・明細名の変更・承認にも、持っている更新番号を付ける', async () => {
    const fake = new 偽の予算API({
      projects: [案件A],
      updateNumber: 3,
      lines: [
        明細('Revenue', 2_000_000, { projectId: 'pa' }),
        明細('PeriodCost', 300_000, { elementCode: 'LICENSE', periodDetail: 'AWS' }),
      ],
    })
    const { user } = await 予算編集画面を開く(fake)

    await user.clear(screen.getByLabelText('案件A 売上高', { exact: true }))
    await user.tab()
    await waitFor(() => expect(タイル('売上高')).toHaveTextContent('¥0'))
    await user.click(screen.getByRole('button', { name: 'ライセンス費 AWS の名称変更' }))
    await user.clear(screen.getByLabelText('ライセンス費 AWS の明細名'))
    await user.type(screen.getByLabelText('ライセンス費 AWS の明細名'), 'AWS本番')
    await user.click(screen.getByRole('button', { name: '保存' }))
    await screen.findByText('└ AWS本番')
    await user.type(screen.getByLabelText('案件A 売上高', { exact: true }), '1')
    await user.tab()
    await waitFor(() => expect(タイル('売上高')).toHaveTextContent('¥1'))
    await user.click(screen.getByRole('button', { name: 'この予算を承認する' }))
    await screen.findByText('承認済')

    expect(
      fake.writes.map((w) => [w.method, w.path.split('/').pop(), 送った番号(w.query)]),
    ).toEqual([
      ['DELETE', 'lines', '3'],
      ['POST', 'rename-period-detail', '4'],
      ['PUT', 'lines', '5'],
      ['POST', 'approve', '6'],
    ])
  })

  test('行34: 保存の応答を待つ間に別のセルを離れると、前の応答を受け取ってからその応答の番号で送る', async () => {
    const fake = new 偽の予算API({ projects: [案件A, 案件B], updateNumber: 1 })
    const { user } = await 予算編集画面を開く(fake)
    const 応答を流す = fake.応答を止める()

    await user.type(screen.getByLabelText('案件A 売上高', { exact: true }), '1200000')
    await user.tab()
    await user.type(screen.getByLabelText('案件B 売上高', { exact: true }), '600000')
    await user.tab()

    // 前の保存(PJ-A)の応答を待つ間は、次の保存(PJ-B)を送らない
    await waitFor(() => expect(fake.writes).toHaveLength(1))
    await 少し待つ()
    expect(fake.writes).toHaveLength(1)
    expect(fake.writes[0].body).toMatchObject({ projectId: 'pa', amount: 1_200_000 })
    応答を流す()

    await waitFor(() => expect(タイル('売上高')).toHaveTextContent('¥1,800,000'))
    expect(
      fake.writes.map((w) => [(w.body as { projectId: string }).projectId, 送った番号(w.query)]),
    ).toEqual([
      ['pa', '1'],
      ['pb', '2'],
    ])
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  test('行35: 同じセルに応答を待たずに続けて入れると、入れた順に送り、後に入れた金額で終わる', async () => {
    const fake = new 偽の予算API({ projects: [案件A], updateNumber: 1 })
    const { user } = await 予算編集画面を開く(fake)
    const 応答を流す = fake.応答を止める()
    const セル = screen.getByLabelText('案件A 売上高', { exact: true })

    await user.type(セル, '1200000')
    await user.tab()
    await user.clear(セル)
    await user.type(セル, '1')
    await user.tab()
    await waitFor(() => expect(fake.writes).toHaveLength(1))
    await 少し待つ()
    expect(fake.writes).toHaveLength(1)
    応答を流す()

    await waitFor(() => expect(fake.writes).toHaveLength(2))
    await waitFor(() => expect(タイル('売上高')).toHaveTextContent('¥1'))
    expect(
      fake.writes.map((w) => [(w.body as { amount: number }).amount, 送った番号(w.query)]),
    ).toEqual([
      [1_200_000, '1'],
      [1, '2'],
    ])
    expect(fake.budget.lines.map((l) => l.amount)).toEqual([1])
  })

  test('行36: 明細の保存が衝突で拒否されると、衝突の文言を出し、予算を読み込み直して入力を消す', async () => {
    const fake = new 偽の予算API({ projects: [案件A], updateNumber: 1 })
    const { user } = await 予算編集画面を開く(fake)
    fake.ほかの画面で保存する((b) => ({
      ...b,
      lines: [明細('Revenue', 1_200_000, { projectId: 'pa' })],
    }))
    const 読み込みの前 = 読み込みの回数(fake)

    await user.type(screen.getByLabelText('案件A 加工費', { exact: true }), '600000')
    await user.tab()

    expect(await screen.findByRole('alert')).toHaveTextContent(衝突の文言)
    await waitFor(() => expect(タイル('売上高')).toHaveTextContent('¥1,200,000'))
    expect(読み込みの回数(fake)).toBe(読み込みの前 + 1)
    expect(screen.getByLabelText('案件A 加工費', { exact: true })).toHaveValue('')
    expect(タイル('加工費')).toHaveTextContent('¥0')

    // 読み込み直した更新番号で、次の保存を受け付ける
    await user.type(screen.getByLabelText('案件A 加工費', { exact: true }), '600000')
    await user.tab()
    await waitFor(() => expect(タイル('加工費')).toHaveTextContent('¥600,000'))
    expect(送った番号(fake.writes[fake.writes.length - 1].query)).toBe('2')
  })

  test('行36: 承認が衝突で拒否されると、衝突の文言を出し、最新の内容を読み込み直して策定中のまま', async () => {
    const fake = new 偽の予算API({
      projects: [案件A],
      updateNumber: 1,
      lines: [明細('Revenue', 1_000_000, { projectId: 'pa' })],
    })
    const { user } = await 予算編集画面を開く(fake)
    fake.ほかの画面で保存する((b) => ({
      ...b,
      lines: [明細('Revenue', 1_200_000, { projectId: 'pa' })],
    }))

    await user.click(screen.getByRole('button', { name: 'この予算を承認する' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(衝突の文言)
    await waitFor(() => expect(タイル('売上高')).toHaveTextContent('¥1,200,000'))
    expect(screen.getByText('策定中')).toBeInTheDocument()
    expect(fake.budget.status).toBe('Draft')
  })

  test('行38: 衝突でない拒否(400)は、いまのとおり文言を出すだけで予算を読み込み直さない', async () => {
    const fake = new 偽の予算API({ projects: [案件A] })
    const { user } = await 予算編集画面を開く(fake)
    server.use(
      http.put('/api/budgets/:id/lines', () =>
        HttpResponse.json({ error: '金額は0以上で入力してください。' }, { status: 400 }),
      ),
    )
    const 読み込みの前 = 読み込みの回数(fake)

    await user.type(screen.getByLabelText('案件A 売上高', { exact: true }), '100')
    await user.tab()

    expect(await screen.findByRole('alert')).toHaveTextContent('金額は0以上で入力してください。')
    expect(読み込みの回数(fake)).toBe(読み込みの前)
    expect(screen.getByLabelText('案件A 売上高', { exact: true })).toHaveValue('100')
  })
})
