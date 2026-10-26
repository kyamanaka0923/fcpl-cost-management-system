import { screen } from '@testing-library/react'
import { describe, expect, test } from 'vitest'
import { 偽の予算API, 明細 } from '../../support/fakeBudgetApi'
import { 案件A, 予算編集画面を開く, 明細行 } from '../../support/budgetEditPage'

describe('予算編集画面: 承認', () => {
  test('承認すると承認済になり、金額を入力できなくなる', async () => {
    const fake = new 偽の予算API({
      projects: [案件A],
      lines: [明細('Revenue', 2_000_000, { projectId: 'pa' })],
    })
    const { user } = await 予算編集画面を開く(fake)

    await user.click(screen.getByRole('button', { name: 'この予算を承認する' }))

    expect(await screen.findByText('承認済')).toBeInTheDocument()
    expect(screen.queryByLabelText('案件A 売上高', { exact: true })).not.toBeInTheDocument()
    expect(screen.getByText(/このバージョンは編集できません/)).toBeInTheDocument()
  })

  test('承認済みの予算は編集の操作を出さず、期間費用は明細のある費目だけを並べる', async () => {
    await 予算編集画面を開く(
      new 偽の予算API({
        status: 'Approved',
        projects: [案件A],
        lines: [
          明細('Revenue', 2_000_000, { projectId: 'pa' }),
          明細('PeriodCost', 300_000, { elementCode: 'LICENSE', periodDetail: 'AWS' }),
        ],
      }),
    )
    expect(screen.queryByRole('button', { name: 'この予算を承認する' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /月次入力/ })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('案件コード')).not.toBeInTheDocument()
    expect(screen.queryByText('人件費')).not.toBeInTheDocument() // 明細のない費目は出さない
    expect(明細行('AWS')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'ライセンス費 AWS の名称変更' }),
    ).not.toBeInTheDocument()
  })
})
