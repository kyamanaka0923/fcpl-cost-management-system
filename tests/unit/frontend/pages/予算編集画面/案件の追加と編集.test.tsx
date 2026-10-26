import { screen } from '@testing-library/react'
import { describe, expect, test } from 'vitest'
import { 偽の予算API, 案件 } from '../../support/fakeBudgetApi'
import { 予算編集画面を開く } from '../../support/budgetEditPage'

describe('予算編集画面: 案件の追加と編集', () => {
  test('案件を追加すると一覧に出る', async () => {
    const fake = new 偽の予算API()
    const { user } = await 予算編集画面を開く(fake)

    await user.type(screen.getByLabelText('案件コード'), 'PJ-A')
    await user.type(screen.getByLabelText('案件名'), '案件A')
    await user.click(screen.getByRole('button', { name: '追加' }))

    expect(await screen.findByLabelText('案件A 売上高', { exact: true })).toBeInTheDocument()
    expect(fake.writes[0].body).toEqual({ code: 'PJ-A', name: '案件A' })
  })

  test('案件コードが重複して追加できないときは、サーバのエラーをトーストで知らせ、×で閉じられる', async () => {
    const { user } = await 予算編集画面を開く(
      new 偽の予算API({ projects: [案件('px', 'PJ-DUP', '案件X')] }),
    )

    await user.type(screen.getByLabelText('案件コード'), 'PJ-DUP')
    await user.type(screen.getByLabelText('案件名'), '案件Y')
    await user.click(screen.getByRole('button', { name: '追加' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "この課には既に案件コード 'PJ-DUP' が存在します。",
    )
    await user.click(screen.getByRole('button', { name: 'エラーを閉じる' }))
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  test('案件のコードと名称を後から変更できる', async () => {
    const fake = new 偽の予算API({ projects: [案件('pe', 'PJE-1', '編集前案件')] })
    const { user } = await 予算編集画面を開く(fake)

    await user.click(screen.getByRole('button', { name: '編集前案件 を編集' }))
    await user.clear(screen.getByLabelText('案件コード編集'))
    await user.type(screen.getByLabelText('案件コード編集'), 'PJE-2')
    await user.clear(screen.getByLabelText('案件名編集'))
    await user.type(screen.getByLabelText('案件名編集'), '編集後案件')
    await user.click(screen.getByRole('button', { name: '保存' }))

    const 行 = (await screen.findByText('編集後案件')).closest('tr') as HTMLElement
    expect(行).toHaveTextContent('PJE-2')
    expect(fake.writes[0]).toMatchObject({
      method: 'PUT',
      path: '/api/projects/pe',
      body: { code: 'PJE-2', name: '編集後案件' },
    })
  })
})
