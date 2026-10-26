import BudgetEditPage from '@app/pages/BudgetEditPage'
import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, test } from 'vitest'
import { 偽の予算API, 明細 } from '../../support/fakeBudgetApi'
import { ROUTE, url, 案件A, 案件B, 予算編集画面を開く, タイル } from '../../support/budgetEditPage'
import { 画面を開く } from '../../support/render'
import { server } from '../../support/server'

describe('予算編集画面: 表示', () => {
  test('区分合計・計画損益・粗利率と、案件ごとの損益・粗利率を表示する', async () => {
    await 予算編集画面を開く(
      new 偽の予算API({
        projects: [案件A, 案件B],
        lines: [
          明細('Revenue', 2_000_000, { projectId: 'pa' }),
          明細('Processing', 1_400_000, { projectId: 'pa' }),
          明細('Revenue', 1_000_000, { projectId: 'pb' }),
          明細('Outsourcing', 700_000, { projectId: 'pb' }),
          明細('PeriodCost', 300_000, { elementCode: 'PERSONNEL' }),
        ],
      }),
    )
    expect(タイル('売上高')).toHaveTextContent('¥3,000,000')
    expect(タイル('期間費用')).toHaveTextContent('¥300,000')
    expect(タイル('計画損益')).toHaveTextContent('¥600,000')
    expect(タイル('計画損益')).toHaveTextContent('粗利率 20.0%')

    const 行A = screen.getByText('案件A').closest('tr') as HTMLElement
    expect(within(行A).getByText('¥600,000')).toBeInTheDocument() // 200万 − 140万
    expect(within(行A).getByText('30.0%')).toBeInTheDocument()
  })

  test('売上高のない案件の粗利率は「—」と表示する', async () => {
    await 予算編集画面を開く(
      new 偽の予算API({
        projects: [案件A],
        lines: [明細('Processing', 100_000, { projectId: 'pa' })],
      }),
    )
    const 行A = screen.getByText('案件A').closest('tr') as HTMLElement
    expect(within(行A).getByText('—')).toBeInTheDocument()
  })

  test('明細のない予算は承認できない', async () => {
    await 予算編集画面を開く(new 偽の予算API())
    expect(screen.getByRole('button', { name: 'この予算を承認する' })).toBeDisabled()
  })

  test('予算を読み込めなかったときはサーバのエラーメッセージをトーストで知らせる', async () => {
    new 偽の予算API().install()
    server.use(
      http.get('/api/budgets/:id', () =>
        HttpResponse.json({ error: '予算が見つかりません: budget-1' }, { status: 404 }),
      ),
    )
    画面を開く(<BudgetEditPage />, ROUTE, url())
    expect(await screen.findByRole('alert')).toHaveTextContent('予算が見つかりません')
  })
})
