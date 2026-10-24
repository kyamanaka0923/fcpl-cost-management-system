import { expect } from '@playwright/test'
import { Given, Then, When, type 画面 } from './fixtures'

// ---- 月次入力 ----

When(
  '案件 {string} の {string} を月次入力で次のとおり入力する:',
  async ({ page }, project: string, category: string, table) => {
    await page.getByRole('button', { name: `${project} ${category} を月次入力` }).click()
    const dialog = page.getByRole('dialog')
    await expect(dialog).toBeVisible()
    // 画面の初期半期は実行日で決まる(上期=4〜9月 / 下期=10〜3月)ため、月名ではなく半期内の位置で指定する
    const 月次入力 = dialog.getByLabel(/^月次 \d+月$/)
    await expect(月次入力).toHaveCount(6)
    let total = 0
    for (const row of table.hashes()) {
      await 月次入力.nth(Number(row['月']) - 1).fill(row['金額'])
      total += Number(row['金額'])
    }
    await expect(dialog.getByText(`¥${total.toLocaleString('en-US')}`)).toBeVisible() // 半期合計の自動計算
    await dialog.getByRole('button', { name: '保存' }).click()
    await expect(page.getByRole('dialog')).toHaveCount(0)
  },
)

Then(
  '案件 {string} の行に月次バッジと {string} が表示される',
  async ({ page }, project: string, amount: string) => {
    const row = page.locator('tr', { hasText: project })
    await expect(row.locator('.badge-monthly')).toBeVisible()
    await expect(row).toContainText(amount)
  },
)

Then('サマリの {string} は {string} である', async ({ page }, tile: string, amount: string) => {
  await expect(page.locator('.stat-tile', { hasText: tile })).toContainText(amount)
})

// ---- 期間費用の明細 ----

const 明細を追加する = async (
  { page }: 画面,
  element: string,
  table: { hashes(): Record<string, string>[] },
) => {
  for (const row of table.hashes()) {
    await page.getByLabel(`${element} 明細名`).fill(row['明細名'])
    await page.getByLabel(`${element} 明細金額`).fill(row['金額'])
    await page.getByRole('button', { name: `${element} に明細を追加` }).click()
    await expect(page.getByText(`└ ${row['明細名']}`, { exact: true })).toBeVisible()
  }
}
When('{string} に次の明細を追加する:', 明細を追加する)
Given('{string} に次の明細を追加している:', 明細を追加する)

Then('明細 {string} が表示される', async ({ page }, detail: string) => {
  await expect(page.getByText(`└ ${detail}`, { exact: true })).toBeVisible()
})

Then('明細 {string} は表示されない', async ({ page }, detail: string) => {
  await expect(page.getByText(`└ ${detail}`, { exact: true })).toHaveCount(0)
})

When('{string} の明細を折りたたむ', async ({ page }, element: string) => {
  await page.getByRole('button', { name: `${element} の明細を折りたたむ` }).click()
})

When('{string} の明細を展開する', async ({ page }, element: string) => {
  await page.getByRole('button', { name: `${element} の明細を展開` }).click()
})

When('明細をすべて折りたたむ', async ({ page }) => {
  await page.getByRole('button', { name: '明細をすべて折りたたむ' }).click()
})

When('明細をすべて展開する', async ({ page }) => {
  await page.getByRole('button', { name: '明細をすべて展開' }).click()
})

Then('{string} の明細の追加欄が表示される', async ({ page }, element: string) => {
  await expect(page.getByLabel(`${element} 明細名`)).toBeVisible()
})

Then('{string} の明細の追加欄は表示されない', async ({ page }, element: string) => {
  await expect(page.getByLabel(`${element} 明細名`)).toHaveCount(0)
})

When(
  '{string} の明細 {string} の名称を {string} に変更する',
  async ({ page }, element: string, detail: string, newDetail: string) => {
    await page.getByRole('button', { name: `${element} ${detail} の名称変更` }).click()
    await page.getByLabel(`${element} ${detail} の明細名`).fill(newDetail)
    await page.getByRole('button', { name: '保存' }).click()
  },
)

When('{string} の明細 {string} を削除する', async ({ page }, element: string, detail: string) => {
  await page.getByRole('button', { name: `${element} ${detail} を削除` }).click()
})

// ---- 実績入力 ----

When('次の実績を計上する:', async ({ page }, table) => {
  for (const row of table.hashes()) {
    await page.getByLabel('区分').selectOption({ label: row['区分'] })
    await page
      .getByLabel(row['区分'] === '期間費用' ? '費目' : '案件')
      .selectOption({ label: row['相手'] })
    const 金額欄 = page.getByLabel(/金額/)
    await 金額欄.fill(row['金額'])
    await page.getByRole('button', { name: '計上' }).click()
    // 計上に成功すると金額欄がクリアされる。次の計上と競合しないよう完了を待つ。
    await expect(金額欄).toHaveValue('')
  }
})

Then('実績の一覧に次の金額が表示される:', async ({ page }, table) => {
  // 明細行と合計行の両方に同額が出るため first で確かめる
  for (const row of table.hashes()) {
    await expect(page.getByRole('cell', { name: row['金額'] }).first()).toBeVisible()
  }
})

// ---- 予実差異・損益・版の比較 ----

Then('{string} に案件 {string} が表示される', async ({ page }, card: string, project: string) => {
  await expect(
    page.locator('.card', { hasText: card }).getByRole('cell', { name: project }),
  ).toBeVisible()
})

Then('案件別損益に次の行が表示される:', async ({ page }, table) => {
  const 案件別損益 = page.locator('.card', { hasText: '案件別 損益' }).first()
  for (const row of table.hashes()) {
    await expect(案件別損益.getByRole('cell', { name: row['行'] }).first()).toBeVisible()
  }
})

Then('案件別損益に {string} が表示される', async ({ page }, amount: string) => {
  const 案件別損益 = page.locator('.card', { hasText: '案件別 損益' }).first()
  await expect(案件別損益.getByRole('cell', { name: amount }).first()).toBeVisible()
})

Then('{string} に {string} が表示される', async ({ page }, tile: string, amount: string) => {
  await expect(page.locator('.stat-tile', { hasText: tile }).getByText(amount)).toBeVisible()
})
