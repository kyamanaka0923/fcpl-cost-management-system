// 予算明細の計算(予算編集画面から切り出した純粋関数)。画面の状態を持たないので単独でテストできる。

import { halfMonths, type BudgetCategory, type BudgetLine } from './api'

/** 区分・案件・費目・明細名が一致する明細を探す(null と未設定は同じものとして扱う)。 */
export const findLine = (
  lines: BudgetLine[],
  category: BudgetCategory,
  projectId: string | null,
  elementCode: string | null,
  periodDetail: string | null = null,
): BudgetLine | undefined =>
  lines.find(
    (l) =>
      l.category === category &&
      (l.projectId ?? null) === projectId &&
      (l.elementCode ?? null) === elementCode &&
      (l.periodDetail ?? null) === periodDetail,
  )

/** 案件 × 区分の金額(明細がなければ 0)。 */
export const projectAmount = (
  lines: BudgetLine[],
  projectId: string,
  category: BudgetCategory,
): number => lines.find((l) => l.category === category && l.projectId === projectId)?.amount ?? 0

/** 案件別損益 = 売上高 − 加工費 − 外注費(期間費用は課共通なので含めない)。 */
export const projectProfit = (lines: BudgetLine[], projectId: string): number =>
  projectAmount(lines, projectId, 'Revenue') -
  projectAmount(lines, projectId, 'Processing') -
  projectAmount(lines, projectId, 'Outsourcing')

/** 費目に属する全明細(費目一括 + 明細名つき)。 */
export const periodLinesFor = (lines: BudgetLine[], code: string): BudgetLine[] =>
  lines.filter((l) => l.category === 'PeriodCost' && l.elementCode === code)

/** 費目内の明細名つきの明細(明細名の順)。 */
export const periodDetailsFor = (lines: BudgetLine[], code: string): BudgetLine[] =>
  periodLinesFor(lines, code)
    .filter((l) => l.periodDetail != null)
    .sort((a, b) => (a.periodDetail ?? '').localeCompare(b.periodDetail ?? ''))

/** 費目の小計(費目一括 + 明細の合計)。 */
export const periodSubtotal = (lines: BudgetLine[], code: string): number =>
  periodLinesFor(lines, code).reduce((sum, l) => sum + l.amount, 0)

/** 明細名つきの明細を持つ費目コードの一覧(重複なし)。 */
export const detailedElementCodes = (lines: BudgetLine[]): string[] => [
  ...new Set(
    lines
      .filter((l) => l.category === 'PeriodCost' && l.periodDetail != null)
      .map((l) => l.elementCode!),
  ),
]

/** 金額セルから離れたときに行う操作。 */
export type CellAction = { kind: 'none' } | { kind: 'upsert'; amount: number } | { kind: 'remove' }

/**
 * 金額セルの入力値と今の金額から、保存の操作を決める。
 * 空欄は 0 として扱い、0 なら明細の削除、正の数なら登録・更新。
 * 数値でない・負の数・今の金額と同じときは何もしない(入力は元に戻す)。
 */
export const cellAction = (raw: string, current: number): CellAction => {
  const trimmed = raw.trim()
  const num = trimmed === '' ? 0 : Number(trimmed)
  if (Number.isNaN(num) || num < 0 || num === current) return { kind: 'none' }
  return num > 0 ? { kind: 'upsert', amount: num } : { kind: 'remove' }
}

/** 入力欄の文字列を数値にする(空欄は 0、数値でなければ NaN)。 */
const toNumber = (raw: string | undefined): number => {
  const trimmed = (raw ?? '').trim()
  return trimmed === '' ? 0 : Number(trimmed)
}

/** 月次入力ダイアログの半期合計(数値でない月は 0 として数える)。 */
export const monthlyTotal = (half: string, edits: Record<number, string>): number =>
  halfMonths(half).reduce((sum, { index }) => {
    const n = toNumber(edits[index])
    return sum + (Number.isNaN(n) ? 0 : n)
  }, 0)

/** 月次入力ダイアログの入力を、保存する月別金額にする(0・空欄・数値でない月は含めない)。 */
export const toMonthlyAmounts = (
  half: string,
  edits: Record<number, string>,
): Record<number, number> => {
  const amounts: Record<number, number> = {}
  for (const { index } of halfMonths(half)) {
    const n = toNumber(edits[index])
    if (!Number.isNaN(n) && n > 0) amounts[index] = n
  }
  return amounts
}

/** 期間費用の明細を追加するときの入力チェック。問題なければ金額を返す。 */
export const parseNewDetail = (
  name: string | undefined,
  rawAmount: string | undefined,
): { name: string; amount: number } | null => {
  const trimmedName = (name ?? '').trim()
  const amount = toNumber(rawAmount)
  if (trimmedName === '' || Number.isNaN(amount) || amount <= 0) return null
  return { name: trimmedName, amount }
}
