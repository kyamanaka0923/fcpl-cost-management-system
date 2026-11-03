import { expect, type Page, type Response } from '@playwright/test'
import { Given, Then, When, type 画面 } from '../support/fixtures'

/*
 * 同じ予算への保存の楽観ロック(FR-4.18・FR-4.19・FR-4.20、BR-BUD-008、SPEC-004)。
 * 「画面 "B"」は、同じブラウザでもう 1 つ開いたページ。主語のないステップ(予算編集画面で…)は元のページで操作する。
 */

/** 予算への保存の応答か(明細の保存・削除・明細名の変更・承認)。 */
const 予算への保存の応答 = (res: Response) =>
  /\/api\/budgets\/[^/]+\/(lines|rename-period-detail|approve)/.test(res.url()) &&
  res.request().method() !== 'GET'

const 画面を取得 = ({ 業務 }: 画面, name: string): Page => {
  const page = 業務.画面.get(name)
  if (!page) throw new Error(`画面 "${name}" は開いていません。`)
  return page
}

const 金額の入力欄 = (page: Page, project: string, category: string) =>
  page.getByLabel(`${project} ${category}`, { exact: true })

/** 予算バージョンの一覧で、名称が label の行。 */
const 予算バージョンの行 = (page: Page, label: string) =>
  page
    .locator('.card', { hasText: '予算バージョン' })
    .locator('tr', { has: page.getByRole('link', { name: label, exact: true }) })

// ---- 画面を開く ----

Given(
  '同じ予算の予算編集画面を画面 {string} としてもう 1 つ開いている',
  async ({ page, 業務 }, name: string) => {
    const other = await page.context().newPage()
    await other.goto(page.url())
    await expect(other.getByRole('button', { name: 'この予算を承認する' })).toBeVisible()
    業務.画面.set(name, other)
  },
)

Given(
  '画面 {string} で課 {string} の画面を開いている',
  async ({ page, 業務 }, name: string, department: string) => {
    const org = 業務.課を取得(department)
    const other = await page.context().newPage()
    await other.goto(`/departments/${org.id}`)
    await expect(other.getByRole('heading', { name: new RegExp(org.code) })).toBeVisible()
    業務.画面.set(name, other)
  },
)

When('予算編集画面を開き直す', async ({ page }) => {
  await page.reload()
  await expect(page.getByRole('button', { name: 'この予算を承認する' })).toBeVisible()
})

// ---- 画面 B の操作 ----

When(
  '画面 {string} で案件 {string} の {string} に {int} を入力する',
  async ({ page, 業務 }, name: string, project: string, category: string, amount: number) => {
    const other = 画面を取得({ page, 業務 }, name)
    const cell = 金額の入力欄(other, project, category)
    await cell.fill(String(amount))
    const saved = other.waitForResponse(予算への保存の応答)
    await cell.blur()
    await saved
  },
)

When('画面 {string} でこの予算を承認しようとする', async ({ page, 業務 }, name: string) => {
  const other = 画面を取得({ page, 業務 }, name)
  const saved = other.waitForResponse(予算への保存の応答)
  await other.getByRole('button', { name: 'この予算を承認する' }).click()
  await saved
})

When(
  '画面 {string} で課の画面から予算 {string} を承認しようとする',
  async ({ page, 業務 }, name: string, label: string) => {
    const other = 画面を取得({ page, 業務 }, name)
    const saved = other.waitForResponse(予算への保存の応答)
    await 予算バージョンの行(other, label).getByRole('button', { name: '承認' }).click()
    await saved
  },
)

// ---- 保存の応答を待たない入力(FR-4.20) ----

When('保存の応答を待たずに次の金額を続けて入力する:', async ({ page }, table) => {
  const rows = table.hashes()
  let 応答の数 = 0
  const 数える = (res: Response) => {
    if (予算への保存の応答(res)) 応答の数++
  }
  page.on('response', 数える)
  for (const row of rows) {
    const cell = 金額の入力欄(page, row['案件'], row['区分'])
    await cell.fill(row['金額'])
    await cell.blur()
  }
  // 入力した数だけ保存の応答が返るまで待つ(画面が 1 つずつ送る)
  await expect.poll(() => 応答の数).toBe(rows.length)
  page.off('response', 数える)
})

// ---- 画面 B の表示 ----

Then(
  '画面 {string} の案件 {string} の {string} の金額の入力欄の値は {string} である',
  async ({ page, 業務 }, name: string, project: string, category: string, value: string) => {
    await expect(金額の入力欄(画面を取得({ page, 業務 }, name), project, category)).toHaveValue(
      value,
    )
  },
)

Then(
  '画面 {string} の案件 {string} の {string} の金額の入力欄は空である',
  async ({ page, 業務 }, name: string, project: string, category: string) => {
    await expect(金額の入力欄(画面を取得({ page, 業務 }, name), project, category)).toHaveValue('')
  },
)

Then('画面 {string} の予算は策定中のままである', async ({ page, 業務 }, name: string) => {
  const other = 画面を取得({ page, 業務 }, name)
  await expect(other.locator('h2 .badge', { hasText: '策定中' })).toBeVisible()
  await expect(other.getByRole('button', { name: 'この予算を承認する' })).toBeVisible()
})

Then(
  '画面 {string} の予算バージョンの一覧で予算 {string} は {string} である',
  async ({ page, 業務 }, name: string, label: string, status: string) => {
    const 行 = 予算バージョンの行(画面を取得({ page, 業務 }, name), label)
    await expect(行.locator('.badge')).toHaveText(status)
  },
)

Then(
  '画面 {string} の予算バージョンの一覧で予算 {string} の売上高は {string} である',
  async ({ page, 業務 }, name: string, label: string, amount: string) => {
    const 行 = 予算バージョンの行(画面を取得({ page, 業務 }, name), label)
    // 列は Ver・名称・状態・売上高の順
    await expect(行.locator('td').nth(3)).toHaveText(amount)
  },
)

Then(
  '画面 {string} に {string} が表示される',
  async ({ page, 業務 }, name: string, text: string) => {
    await expect(画面を取得({ page, 業務 }, name).locator('.toast-error')).toContainText(text)
  },
)

Then('画面 {string} にエラーは表示されない', async ({ page, 業務 }, name: string) => {
  await expect(画面を取得({ page, 業務 }, name).locator('.toast-error')).toHaveCount(0)
})
