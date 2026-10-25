// @vitest-environment node
import {
  cellAction,
  detailedElementCodes,
  findLine,
  monthlyTotal,
  parseNewDetail,
  periodDetailsFor,
  periodSubtotal,
  projectAmount,
  projectProfit,
  toMonthlyAmounts,
} from '@app/budgetLines'
import { describe, expect, test } from 'vitest'
import { 明細 } from '../support/fakeBudgetApi'

const lines = [
  明細('Revenue', 2_000_000, { projectId: 'A' }),
  明細('Processing', 1_400_000, { projectId: 'A' }),
  明細('Revenue', 1_000_000, { projectId: 'B' }),
  明細('Outsourcing', 700_000, { projectId: 'B' }),
  明細('PeriodCost', 300_000, { elementCode: 'PERSONNEL' }),
  明細('PeriodCost', 200_000, { elementCode: 'LICENSE', periodDetail: 'GitHub' }),
  明細('PeriodCost', 300_000, { elementCode: 'LICENSE', periodDetail: 'AWS' }),
]

describe('明細の検索と案件別の金額', () => {
  test('区分・案件・費目・明細名がすべて一致する明細だけを見つける', () => {
    expect(findLine(lines, 'PeriodCost', null, 'LICENSE', 'AWS')?.amount).toBe(300_000)
    expect(findLine(lines, 'PeriodCost', null, 'PERSONNEL')?.amount).toBe(300_000)
  })

  test('明細名を省くと費目一括の明細を探し、明細名つきの明細とは区別する', () => {
    expect(findLine(lines, 'PeriodCost', null, 'LICENSE')).toBeUndefined()
  })

  test('案件 × 区分の金額は明細がなければ0になる', () => {
    expect(projectAmount(lines, 'A', 'Revenue')).toBe(2_000_000)
    expect(projectAmount(lines, 'A', 'Outsourcing')).toBe(0)
    expect(projectAmount(lines, '存在しない案件', 'Revenue')).toBe(0)
  })

  test.each([
    ['A', 600_000], // 200万 − 140万
    ['B', 300_000], // 100万 − 70万
  ])(
    '案件 %s の損益は売上高から加工費と外注費を引いた %d になる(期間費用は含めない)',
    (id, profit) => {
      expect(projectProfit(lines, id)).toBe(profit)
    },
  )

  test('コストが売上高を上回る案件の損益はマイナスになる', () => {
    const 赤字 = [
      明細('Revenue', 100, { projectId: 'C' }),
      明細('Outsourcing', 300, { projectId: 'C' }),
    ]
    expect(projectProfit(赤字, 'C')).toBe(-200)
  })
})

describe('期間費用の明細', () => {
  test('費目内の明細名つきの明細は明細名の順に並び、費目一括の明細は含めない', () => {
    expect(periodDetailsFor(lines, 'LICENSE').map((l) => l.periodDetail)).toEqual(['AWS', 'GitHub'])
    expect(periodDetailsFor(lines, 'PERSONNEL')).toEqual([])
  })

  test('費目の小計は費目内の明細の合計になる', () => {
    expect(periodSubtotal(lines, 'LICENSE')).toBe(500_000)
    expect(periodSubtotal(lines, 'PERSONNEL')).toBe(300_000)
    expect(periodSubtotal(lines, 'TRAVEL')).toBe(0)
  })

  test('明細名つきの明細を持つ費目だけを重複なく挙げる', () => {
    expect(detailedElementCodes(lines)).toEqual(['LICENSE'])
  })

  test.each([
    ['AWS', '300000', { name: 'AWS', amount: 300_000 }],
    ['  AWS  ', '1', { name: 'AWS', amount: 1 }], // 前後の空白は取り除く
    ['', '300000', null], // 明細名がない
    ['   ', '300000', null], // 空白だけの明細名
    ['AWS', '', null], // 金額がない
    ['AWS', '0', null], // 0円の明細は作らない
    ['AWS', 'abc', null], // 数値でない
    [undefined, undefined, null], // まだ何も入力していない
  ])('明細名 %j・金額 %j で明細を追加しようとすると %j になる', (name, amount, expected) => {
    expect(parseNewDetail(name, amount)).toEqual(expected)
  })
})

describe('金額セルから離れたときの保存の判断', () => {
  test.each([
    ['3000000', 0, { kind: 'upsert', amount: 3_000_000 }], // 新しく入力した
    ['3500000', 3_000_000, { kind: 'upsert', amount: 3_500_000 }], // 金額を変えた
    [' 3500000 ', 3_000_000, { kind: 'upsert', amount: 3_500_000 }], // 前後の空白は無視する
    ['1.5', 0, { kind: 'upsert', amount: 1.5 }], // 小数もそのまま送る(丸めはサーバ側)
    ['', 3_000_000, { kind: 'remove' }], // 空欄にした = 明細の削除
    ['0', 3_000_000, { kind: 'remove' }], // 0にした = 明細の削除
    ['3000000', 3_000_000, { kind: 'none' }], // 変わっていない
    ['', 0, { kind: 'none' }], // もともと明細がなく、空欄のまま
    ['0', 0, { kind: 'none' }],
    ['-100', 3_000_000, { kind: 'none' }], // 負の数は受け付けない
    ['abc', 3_000_000, { kind: 'none' }], // 数値でない
  ])('入力 %j・今の金額 %d のときは %j', (raw, current, expected) => {
    expect(cellAction(raw, current)).toEqual(expected)
  })
})

describe('月次入力ダイアログ', () => {
  test('半期合計は入力した月の合計になる', () => {
    expect(monthlyTotal('2026-H1', { 1: '1000000', 2: '500000' })).toBe(1_500_000)
  })

  test('空欄と数値でない月は0として数え、合計を止めない', () => {
    expect(monthlyTotal('2026-H1', { 1: '1000000', 2: '', 3: 'abc', 6: '200' })).toBe(1_000_200)
  })

  test('半期の6か月の外にある月は数えない', () => {
    expect(monthlyTotal('2026-H2', { 1: '100', 7: '999' })).toBe(100)
  })

  test('保存する月別金額には正の金額の月だけを入れる', () => {
    expect(
      toMonthlyAmounts('2026-H1', { 1: '1000000', 2: '0', 3: '', 4: 'abc', 5: '-1', 6: '300' }),
    ).toEqual({
      1: 1_000_000,
      6: 300,
    })
  })

  test('どの月にも金額がなければ空になる(画面は明細の削除として扱う)', () => {
    expect(toMonthlyAmounts('2026-H1', {})).toEqual({})
    expect(toMonthlyAmounts('2026-H1', { 1: '0', 2: '' })).toEqual({})
  })
})
