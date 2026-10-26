import { expect } from '@playwright/test'
import { Then, When } from '../support/fixtures'

// ---- 費目マスタ ----

When(
  '費目マスタ画面で費目 {string} を名称 {string} で追加する',
  async ({ page, 業務 }, code: string, name: string) => {
    await page.goto('/')
    await page.getByRole('link', { name: '費目マスタ' }).click()
    await expect(page.getByRole('heading', { name: '費目マスタ(期間費用)' })).toBeVisible()
    await page.getByLabel('費目コード').fill(業務.費目コード(code))
    await page.getByLabel('費目名').fill(業務.実名(name))
    await page.getByRole('button', { name: '追加' }).click()
  },
)

Then('費目マスタの一覧に費目 {string} が表示される', async ({ page, 業務 }, code: string) => {
  await expect(page.getByRole('cell', { name: 業務.費目コード(code) })).toBeVisible()
})

Then('期間費用に {string} が表示される', async ({ page, 業務 }, name: string) => {
  await expect(
    page.locator('.card', { hasText: '期間費用' }).getByText(業務.実名(name)),
  ).toBeVisible()
})

Then('予算編集画面には費目を追加するフォームがない', async ({ page }) => {
  // 費目の追加は費目マスタ画面に集約している(課の予算編集からは追加できない)
  await expect(page.getByRole('heading', { name: '費目を追加' })).toHaveCount(0)
})

// ---- 案件の編集 ----

When(
  '案件 {string} をコード {string}、名称 {string} に変更する',
  async ({ page }, project: string, code: string, name: string) => {
    await page
      .locator('tr', { hasText: project })
      .getByRole('button', { name: `${project} を編集` })
      .click()
    // 編集モードでは名称が入力欄になるため、ページ全体で入力欄を特定する
    await page.getByLabel('案件コード編集').fill(code)
    await page.getByLabel('案件名編集').fill(name)
    await page.getByRole('button', { name: '保存' }).click()
  },
)

Then(
  '案件 {string} の行にコード {string} が表示される',
  async ({ page }, project: string, code: string) => {
    await expect(page.locator('tr', { hasText: project })).toContainText(code)
  },
)
