import { screen, waitFor } from '@testing-library/react'
import { describe, expect, test } from 'vitest'
import { 偽の予算API, 明細 } from '../../support/fakeBudgetApi'
import { 予算編集画面を開く, タイル, 明細行 } from '../../support/budgetEditPage'

describe('予算編集画面: 期間費用の明細', () => {
  const ライセンス費の明細 = () => [
    明細('PeriodCost', 300_000, { elementCode: 'LICENSE', periodDetail: 'AWS' }),
    明細('PeriodCost', 200_000, { elementCode: 'LICENSE', periodDetail: 'GitHub' }),
  ]

  test('明細を追加すると費目の小計と期間費用は明細の合計になり、入力欄は空に戻る', async () => {
    const fake = new 偽の予算API()
    const { user } = await 予算編集画面を開く(fake)

    for (const [name, amount] of [
      ['AWS', '300000'],
      ['GitHub', '200000'],
    ]) {
      await user.type(screen.getByLabelText('ライセンス費 明細名'), name)
      await user.type(screen.getByLabelText('ライセンス費 明細金額'), amount)
      await user.click(screen.getByRole('button', { name: 'ライセンス費 に明細を追加' }))
      await screen.findByText(`└ ${name}`)
    }

    expect(screen.getByText('小計 ¥500,000')).toBeInTheDocument()
    expect(タイル('期間費用')).toHaveTextContent('¥500,000')
    expect(screen.getByLabelText('ライセンス費 明細名')).toHaveValue('')
    expect(screen.getByText('明細2件')).toBeInTheDocument()
  })

  test.each([
    ['', '300000'],
    ['AWS', ''],
    ['AWS', '0'],
  ])('明細名 "%s"・金額 "%s" では追加せず、入力を促すエラーを出す', async (name, amount) => {
    const fake = new 偽の予算API()
    const { user } = await 予算編集画面を開く(fake)

    if (name) await user.type(screen.getByLabelText('ライセンス費 明細名'), name)
    if (amount) await user.type(screen.getByLabelText('ライセンス費 明細金額'), amount)
    await user.click(screen.getByRole('button', { name: 'ライセンス費 に明細を追加' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      '明細名と金額(1以上)を入力してください。',
    )
    expect(fake.writes).toEqual([])
  })

  test('費目一括で入力済みの費目に明細を追加すると、先に費目一括を削除してから明細を登録する', async () => {
    const fake = new 偽の予算API({
      lines: [明細('PeriodCost', 400_000, { elementCode: 'LICENSE' })],
    })
    const { user } = await 予算編集画面を開く(fake)
    expect(タイル('期間費用')).toHaveTextContent('¥400,000')

    await user.type(screen.getByLabelText('ライセンス費 明細名'), 'AWS')
    await user.type(screen.getByLabelText('ライセンス費 明細金額'), '250000')
    await user.click(screen.getByRole('button', { name: 'ライセンス費 に明細を追加' }))

    await screen.findByText('└ AWS')
    expect(fake.writes.map((r) => r.method)).toEqual(['DELETE', 'PUT']) // 混在はサーバが拒否するため順序が大事
    expect(fake.writes[0].query).toBe('?category=PeriodCost&elementCode=LICENSE')
    expect(タイル('期間費用')).toHaveTextContent('¥250,000')
  })

  test('費目ごとに明細を折りたたむと明細は隠れて小計は残り、展開で元に戻る', async () => {
    const { user } = await 予算編集画面を開く(new 偽の予算API({ lines: ライセンス費の明細() }))

    await user.click(screen.getByRole('button', { name: 'ライセンス費 の明細を折りたたむ' }))
    expect(明細行('AWS')).not.toBeInTheDocument()
    expect(screen.getByText('小計 ¥500,000')).toBeInTheDocument()
    expect(screen.getByLabelText('人件費 明細名')).toBeInTheDocument() // 他の費目は影響しない

    await user.click(screen.getByRole('button', { name: 'ライセンス費 の明細を展開' }))
    expect(明細行('AWS')).toBeInTheDocument()
  })

  test('すべて折りたたむと明細のない費目の追加欄も畳まれ、すべて展開で戻る', async () => {
    const { user } = await 予算編集画面を開く(new 偽の予算API({ lines: ライセンス費の明細() }))

    await user.click(screen.getByRole('button', { name: '明細をすべて折りたたむ' }))
    expect(明細行('AWS')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('人件費 明細名')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '明細をすべて展開' }))
    expect(明細行('AWS')).toBeInTheDocument()
    expect(screen.getByLabelText('人件費 明細名')).toBeInTheDocument()
  })

  test('明細名を変更しても金額は保持される', async () => {
    const fake = new 偽の予算API({ lines: ライセンス費の明細() })
    const { user } = await 予算編集画面を開く(fake)

    await user.click(screen.getByRole('button', { name: 'ライセンス費 AWS の名称変更' }))
    const input = screen.getByLabelText('ライセンス費 AWS の明細名')
    await user.clear(input)
    await user.type(input, 'AWS本番')
    await user.click(screen.getByRole('button', { name: '保存' }))

    await screen.findByText('└ AWS本番')
    expect(明細行('AWS')).not.toBeInTheDocument()
    expect(fake.writes[0].body).toEqual({
      elementCode: 'LICENSE',
      oldDetail: 'AWS',
      newDetail: 'AWS本番',
    })
    expect(タイル('期間費用')).toHaveTextContent('¥500,000')
  })

  test('明細名を空にして変更しようとするとエラーになり、何も送らない', async () => {
    const fake = new 偽の予算API({ lines: ライセンス費の明細() })
    const { user } = await 予算編集画面を開く(fake)

    await user.click(screen.getByRole('button', { name: 'ライセンス費 AWS の名称変更' }))
    await user.clear(screen.getByLabelText('ライセンス費 AWS の明細名'))
    await user.click(screen.getByRole('button', { name: '保存' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('明細名を入力してください。')
    expect(fake.writes).toEqual([])
  })

  test('明細名を変えずに保存しても何も送らず、編集を終える', async () => {
    const fake = new 偽の予算API({ lines: ライセンス費の明細() })
    const { user } = await 予算編集画面を開く(fake)

    await user.click(screen.getByRole('button', { name: 'ライセンス費 AWS の名称変更' }))
    await user.click(screen.getByRole('button', { name: '保存' }))

    expect(明細行('AWS')).toBeInTheDocument()
    expect(fake.writes).toEqual([])
  })

  test('明細を削除すると費目の合計が減る', async () => {
    const fake = new 偽の予算API({ lines: ライセンス費の明細() })
    const { user } = await 予算編集画面を開く(fake)

    await user.click(screen.getByRole('button', { name: 'ライセンス費 GitHub を削除' }))

    await waitFor(() => expect(明細行('GitHub')).not.toBeInTheDocument())
    expect(fake.writes[0].query).toBe(
      '?category=PeriodCost&elementCode=LICENSE&periodDetail=GitHub',
    )
    expect(タイル('期間費用')).toHaveTextContent('¥300,000')
  })
})
