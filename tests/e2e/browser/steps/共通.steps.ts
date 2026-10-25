import { expect } from '@playwright/test'
import { Given, Then, When, type 画面 } from '../support/fixtures'

// ---- 準備(API) ----

Given(
  '部 {string} に課 {string} がある',
  async ({ 業務 }, division: string, department: string) => {
    await 業務.部と課を用意(division, department)
  },
)

Given('課 {string} に次の案件がある:', async ({ 業務 }, department: string, table) => {
  for (const row of table.hashes()) await 業務.案件を用意(department, row['コード'], row['名称'])
})

Given(
  '課 {string} の今期の予算が次の明細で承認されている:',
  async ({ 業務 }, department: string, table) => {
    await 業務.承認済み予算を用意(department, table.hashes())
  },
)

Given(
  '課 {string} の今期に次の実績が計上されている:',
  async ({ 業務 }, department: string, table) => {
    await 業務.実績を用意(department, table.hashes())
  },
)

// ---- 画面の移動 ----

Given('トップページを開いている', async ({ page }) => {
  await page.goto('/')
})

const 部の画面を開く = async ({ page, 業務 }: 画面, division: string) => {
  const org = 業務.部を取得(division)
  await page.goto(`/divisions/${org.id}`)
  await expect(page.getByRole('heading', { name: new RegExp(org.code) })).toBeVisible()
}
When('部 {string} の画面を開く', 部の画面を開く)

const 課の画面を開く = async ({ page, 業務 }: 画面, department: string) => {
  const org = 業務.課を取得(department)
  await page.goto(`/departments/${org.id}`)
  await expect(page.getByRole('heading', { name: new RegExp(org.code) })).toBeVisible()
}
When('課 {string} の画面を開く', 課の画面を開く)
Given('課 {string} の画面を開いている', 課の画面を開く)

const 課の画面から進む = async ({ page, 業務 }: 画面, department: string, button: string) => {
  await 課の画面を開く({ page, 業務 }, department)
  await page.getByRole('button', { name: button }).click()
}
When('課 {string} の画面から {string} へ進む', 課の画面から進む)
Given('課 {string} の画面から {string} へ進んでいる', 課の画面から進む)

When('{string} タブに切り替える', async ({ page }, tab: string) => {
  await page.getByRole('button', { name: tab, exact: true }).click()
})

// ---- 表示の確認 ----

// 金額は明細行と合計行、統計タイルとテーブルなどに重複して表示されるため first で確かめる
Then('画面に {string} が表示される', async ({ page }, text: string) => {
  await expect(page.getByText(text).first()).toBeVisible()
})

Then('見出し {string} が表示される', async ({ page }, heading: string) => {
  await expect(page.getByRole('heading', { name: heading })).toBeVisible()
})

Then('エラーは表示されない', async ({ page }) => {
  await expect(page.locator('.toast-error')).toHaveCount(0)
})
