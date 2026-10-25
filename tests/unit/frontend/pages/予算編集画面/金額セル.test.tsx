import { screen, waitFor } from '@testing-library/react'
import { describe, expect, test } from 'vitest'
import { 偽の予算API, 明細 } from '../../support/fakeBudgetApi'
import { 案件A, 予算編集画面を開く, タイル } from '../../support/budgetEditPage'

describe('予算編集画面: 案件別の金額セル', () => {
  test('金額を入力してセルから離れると明細を登録し、上部サマリに反映する', async () => {
    const fake = new 偽の予算API({ projects: [案件A] })
    const { user } = await 予算編集画面を開く(fake)

    await user.type(screen.getByLabelText('案件A 売上高', { exact: true }), '2000000')
    await user.tab()

    await waitFor(() => expect(タイル('売上高')).toHaveTextContent('¥2,000,000'))
    expect(fake.writes).toEqual([
      expect.objectContaining({
        method: 'PUT',
        body: { category: 'Revenue', projectId: 'pa', amount: 2_000_000 },
      }),
    ])
  })

  test('金額を変えずにセルから離れても保存しない', async () => {
    const fake = new 偽の予算API({
      projects: [案件A],
      lines: [明細('Revenue', 2_000_000, { projectId: 'pa' })],
    })
    const { user } = await 予算編集画面を開く(fake)

    await user.click(screen.getByLabelText('案件A 売上高', { exact: true }))
    await user.tab()

    expect(fake.writes).toEqual([])
  })

  test('金額を消してセルから離れると明細を削除する', async () => {
    const fake = new 偽の予算API({
      projects: [案件A],
      lines: [明細('Revenue', 2_000_000, { projectId: 'pa' })],
    })
    const { user } = await 予算編集画面を開く(fake)

    await user.clear(screen.getByLabelText('案件A 売上高', { exact: true }))
    await user.tab()

    await waitFor(() => expect(タイル('売上高')).toHaveTextContent('¥0'))
    expect(fake.writes).toEqual([
      expect.objectContaining({ method: 'DELETE', query: '?category=Revenue&projectId=pa' }),
    ])
  })
})
