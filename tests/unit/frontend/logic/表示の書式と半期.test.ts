// @vitest-environment node
import {
  currentFiscalHalf,
  fiscalHalfOptions,
  formatPercent,
  formatSignedYen,
  formatYen,
  halfLabel,
  halfMonths,
  marginRate,
} from '@app/api'
import { afterEach, describe, expect, test, vi } from 'vitest'

describe('金額と割合の表示', () => {
  test.each([
    [0, '0'],
    [1000, '1,000'],
    [1234567, '1,234,567'],
    [-50000, '-50,000'],
    [1234.6, '1,235'], // 円未満は四捨五入して出す
  ])('金額 %d は "%s" と3桁区切りで表示する', (value, expected) => {
    expect(formatYen(value)).toBe(expected)
  })

  test.each([
    [50000, '+50,000'],
    [0, '0'],
    [-50000, '-50,000'],
  ])('差異 %d は増加なら + を付けて "%s" と表示する', (value, expected) => {
    expect(formatSignedYen(value)).toBe(expected)
  })

  test.each([
    [0.2, '20.0%'],
    [0.3, '30.0%'],
    [0.1234, '12.3%'],
    [-0.05, '-5.0%'],
    [null, '—'],
  ])('割合 %s は "%s" と表示する', (rate, expected) => {
    expect(formatPercent(rate)).toBe(expected)
  })

  test('粗利率は損益を売上高で割った値になる', () => {
    expect(marginRate(600_000, 3_000_000)).toBe(0.2)
    expect(marginRate(-100_000, 1_000_000)).toBe(-0.1)
  })

  test('売上高が0のとき粗利率は計算できないので null になる', () => {
    expect(marginRate(0, 0)).toBeNull()
    expect(marginRate(-300_000, 0)).toBeNull()
  })
})

describe('会計半期', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  test.each([
    ['2026-H1', '2026年度 上期'],
    ['2026-H2', '2026年度 下期'],
  ])('%s は "%s" と表示する', (half, expected) => {
    expect(halfLabel(half)).toBe(expected)
  })

  test('上期の6か月は4月から9月になる', () => {
    expect(halfMonths('2026-H1').map((m) => m.label)).toEqual([
      '4月',
      '5月',
      '6月',
      '7月',
      '8月',
      '9月',
    ])
  })

  test('下期の6か月は10月から翌年3月になり、年をまたいでも月の位置は1から6になる', () => {
    const months = halfMonths('2026-H2')
    expect(months.map((m) => m.label)).toEqual(['10月', '11月', '12月', '1月', '2月', '3月'])
    expect(months.map((m) => m.index)).toEqual([1, 2, 3, 4, 5, 6])
  })

  // 年度は4月始まり。1〜3月は前年度の下期になる(実行日で結果が変わるので日付を固定して確かめる)
  test.each([
    ['2026-04-01T00:00:00+09:00', '2026-H1'], // 上期の初日
    ['2026-09-30T23:59:59+09:00', '2026-H1'], // 上期の最終日
    ['2026-10-01T00:00:00+09:00', '2026-H2'], // 下期の初日
    ['2026-12-31T23:59:59+09:00', '2026-H2'], // 年末も同じ年度の下期
    ['2027-01-01T00:00:00+09:00', '2026-H2'], // 年が明けても前年度の下期
    ['2027-03-31T23:59:59+09:00', '2026-H2'], // 下期の最終日
    ['2027-04-01T00:00:00+09:00', '2027-H1'], // 次の年度の上期
  ])('今日が %s なら今期は %s になる', (now, expected) => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(now))
    expect(currentFiscalHalf()).toBe(expected)
  })

  test('半期の選択肢は今期の年度を中心に前年度から翌年度までの6半期になる', () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2027-02-15T12:00:00+09:00')) // 2026年度 下期
    expect(fiscalHalfOptions()).toEqual([
      '2025-H1',
      '2025-H2',
      '2026-H1',
      '2026-H2',
      '2027-H1',
      '2027-H2',
    ])
  })
})
