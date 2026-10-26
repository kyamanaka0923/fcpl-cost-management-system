import BudgetEditPage from '@app/pages/BudgetEditPage'
import { screen } from '@testing-library/react'
import { BUDGET_ID, DEPARTMENT_ID, 偽の予算API, 案件 } from './fakeBudgetApi'
import { 画面を開く } from './render'

export const ROUTE = '/departments/:departmentId/budgets/:budgetId'
export const url = (half = '2026-H1') =>
  `/departments/${DEPARTMENT_ID}/budgets/${BUDGET_ID}?half=${half}`

export const 案件A = 案件('pa', 'PJ-A', '案件A')
export const 案件B = 案件('pb', 'PJ-B', '案件B')

/** 予算編集画面を開き、予算の読み込みが終わるまで待つ。 */
export async function 予算編集画面を開く(fake: 偽の予算API, half?: string) {
  fake.install()
  const view = 画面を開く(<BudgetEditPage />, ROUTE, url(half))
  await screen.findByRole('heading', { name: /当初予算/ })
  return view
}

/** 上部サマリのタイル(売上高・期間費用・計画損益など)。 */
export const タイル = (label: string) =>
  screen
    .getAllByText(label, { selector: '.stat-tile .label' })[0]
    .closest('.stat-tile') as HTMLElement

export const 明細行 = (detail: string) => screen.queryByText(`└ ${detail}`, { exact: true })
