import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, test } from 'vitest'
import { 偽の予算API, 明細 } from '../../support/fakeBudgetApi'
import { 案件A, 予算編集画面を開く, タイル } from '../../support/budgetEditPage'

describe('予算編集画面: 月次入力', () => {
  test('月ごとの金額を入れると半期合計を自動計算し、保存すると月次バッジつきで表示する', async () => {
    const fake = new 偽の予算API({ projects: [案件A] })
    const { user } = await 予算編集画面を開く(fake)

    await user.click(screen.getByRole('button', { name: '案件A 売上高 を月次入力' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByLabelText('月次 4月'), '1000000')
    await user.type(within(dialog).getByLabelText('月次 5月'), '500000')
    expect(within(dialog).getByText('¥1,500,000')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: '保存' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(fake.writes[0].body).toEqual({
      category: 'Revenue',
      projectId: 'pa',
      elementCode: null,
      amount: 0,
      monthlyAmounts: { 1: 1_000_000, 2: 500_000 },
      periodDetail: null,
    })
    const 行A = screen.getByText('案件A').closest('tr') as HTMLElement
    expect(within(行A).getByText('月次')).toBeInTheDocument()
    // 月次にした明細は半期一括の入力欄を出さない
    expect(screen.queryByLabelText('案件A 売上高', { exact: true })).not.toBeInTheDocument()
    expect(タイル('売上高')).toHaveTextContent('¥1,500,000')
  })

  test('下期の月次入力は10月から3月の6か月を並べる', async () => {
    const { user } = await 予算編集画面を開く(new 偽の予算API({ projects: [案件A] }), '2026-H2')

    await user.click(screen.getByRole('button', { name: '案件A 売上高 を月次入力' }))
    const labels = within(screen.getByRole('dialog'))
      .getAllByLabelText(/^月次 \d+月$/)
      .map((el) => el.getAttribute('aria-label'))
    expect(labels).toEqual([
      '月次 10月',
      '月次 11月',
      '月次 12月',
      '月次 1月',
      '月次 2月',
      '月次 3月',
    ])
  })

  test('どの月にも金額を入れずに保存すると明細を削除する', async () => {
    const fake = new 偽の予算API({
      projects: [案件A],
      lines: [明細('Revenue', 300_000, { projectId: 'pa', monthlyAmounts: { 1: 300_000 } })],
    })
    const { user } = await 予算編集画面を開く(fake)

    await user.click(screen.getByRole('button', { name: '案件A 売上高 を月次入力' }))
    const dialog = screen.getByRole('dialog')
    await user.clear(within(dialog).getByLabelText('月次 4月'))
    await user.click(within(dialog).getByRole('button', { name: '保存' }))

    await waitFor(() => expect(fake.writes).toHaveLength(1))
    expect(fake.writes[0]).toMatchObject({
      method: 'DELETE',
      query: '?category=Revenue&projectId=pa',
    })
  })

  test('「半期一括に戻す」と月次の合計を半期一括の金額として登録し直す', async () => {
    const fake = new 偽の予算API({
      projects: [案件A],
      lines: [
        明細('Revenue', 1_500_000, {
          projectId: 'pa',
          monthlyAmounts: { 1: 1_000_000, 2: 500_000 },
        }),
      ],
    })
    const { user } = await 予算編集画面を開く(fake)

    await user.click(screen.getByRole('button', { name: '案件A 売上高 を月次入力' }))
    // 保存済みの月別金額をダイアログに読み込んでいる
    expect(within(screen.getByRole('dialog')).getByLabelText('月次 4月')).toHaveValue('1,000,000')
    await user.click(screen.getByRole('button', { name: '半期一括に戻す' }))

    await waitFor(() => expect(fake.writes).toHaveLength(1))
    expect(fake.writes[0].body).toEqual({
      category: 'Revenue',
      projectId: 'pa',
      elementCode: null,
      amount: 1_500_000,
      periodDetail: null,
    })
    expect(await screen.findByLabelText('案件A 売上高', { exact: true })).toHaveValue('1,500,000')
  })

  test('取消で閉じると何も保存しない', async () => {
    const fake = new 偽の予算API({ projects: [案件A] })
    const { user } = await 予算編集画面を開く(fake)

    await user.click(screen.getByRole('button', { name: '案件A 売上高 を月次入力' }))
    await user.type(within(screen.getByRole('dialog')).getByLabelText('月次 4月'), '1000000')
    await user.click(screen.getByRole('button', { name: '取消' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(fake.writes).toEqual([])
  })
})
