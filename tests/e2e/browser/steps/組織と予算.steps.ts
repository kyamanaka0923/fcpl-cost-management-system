import { expect } from '@playwright/test'
import { Given, Then, When, type 画面 } from '../support/fixtures'

// ---- 部・課の登録(画面) ----

When('部 {string} を登録する', async ({ page, 業務 }, division: string) => {
  const org = 業務.部を予約(division)
  await page.getByLabel('部コード').fill(org.code)
  await page.getByLabel('部名').fill(org.name)
  await page.getByRole('button', { name: '登録' }).click()
})

Then('部の一覧に {string} が表示される', async ({ page, 業務 }, division: string) => {
  await expect(page.getByRole('link', { name: 業務.部を取得(division).name })).toBeVisible()
})

When('部 {string} の画面を一覧から開く', async ({ page, 業務 }, division: string) => {
  const org = 業務.部を取得(division)
  await page.goto('/')
  await page.getByRole('link', { name: org.name }).click()
  await expect(page.getByRole('heading', { name: new RegExp(org.code) })).toBeVisible()
  // 画面から登録した部の ID を、以降の画面移動のために URL から覚えておく
  org.id = page.url().split('/').pop() ?? ''
})

When('課 {string} を登録する', async ({ page, 業務 }, department: string) => {
  const org = 業務.課を予約(department)
  await page.getByLabel('課コード').fill(org.code)
  await page.getByLabel('課名').fill(org.name)
  await page.getByRole('button', { name: '登録' }).click()
})

Then('課の一覧に {string} が表示される', async ({ page, 業務 }, department: string) => {
  await expect(page.getByRole('link', { name: 業務.課を取得(department).name })).toBeVisible()
})

When('課 {string} の画面を一覧から開く', async ({ page, 業務 }, department: string) => {
  await page.getByRole('link', { name: 業務.課を取得(department).name }).click()
})

Then('課 {string} の画面が表示されている', async ({ page, 業務 }, department: string) => {
  const org = 業務.課を取得(department)
  await expect(page.getByRole('heading', { name: new RegExp(org.code) })).toBeVisible()
})

// ---- 予算編集 ----

const ドラフトを作成する = async ({ page }: 画面, label: string) => {
  const 予算カード = page.locator('.card', { hasText: '予算バージョン' })
  await 予算カード.getByLabel('予算名').fill(label)
  await 予算カード.getByRole('button', { name: 'ドラフト作成' }).click()
  await expect(page.getByRole('heading', { name: new RegExp(label) })).toBeVisible()
}
When('予算 {string} のドラフトを作成する', ドラフトを作成する)
Given('予算 {string} のドラフトを作成している', ドラフトを作成する)

const 案件を追加する = async ({ page }: 画面, code: string, name: string) => {
  const form = page.locator('form', { hasText: '案件コード' })
  await form.getByLabel('案件コード').fill(code)
  await form.getByLabel('案件名').fill(name)
  await form.getByRole('button', { name: '追加' }).click()
}

const 次の案件を追加する = async (
  { page, 業務 }: 画面,
  table: { hashes(): Record<string, string>[] },
) => {
  for (const row of table.hashes()) {
    await 案件を追加する({ page, 業務 }, row['コード'], row['名称'])
    await expect(page.getByRole('cell', { name: new RegExp(row['名称']) }).first()).toBeVisible()
  }
}
When('予算編集画面で次の案件を追加する:', 次の案件を追加する)
Given('予算編集画面で次の案件を追加している:', 次の案件を追加する)

When('予算編集画面で次の金額を入力する:', async ({ page }, table) => {
  for (const row of table.hashes()) {
    // 月次入力ボタンの aria-label が前方一致するため exact で金額入力欄だけを特定する
    const cell = page.getByLabel(`${row['案件']} ${row['区分']}`, { exact: true })
    await cell.fill(row['金額'])
    // セルを離れると自動保存される。保存が重なるとサーバ側で後の保存が先の保存を上書きしうるため、
    // 1セルずつ保存の応答を待ってから次へ進む
    const saved = page.waitForResponse(
      (res) => res.url().includes('/lines') && res.request().method() === 'PUT',
    )
    await cell.blur()
    await saved
  }
})

When('期間費用 {string} に {int} を入力する', async ({ page }, element: string, amount: number) => {
  const cell = page.getByLabel(`${element} 金額`)
  await cell.fill(String(amount))
  const saved = page.waitForResponse(
    (res) => res.url().includes('/lines') && res.request().method() === 'PUT',
  )
  await cell.blur()
  await saved
})

Then(
  '案件 {string} の行に粗利率 {string} が表示される',
  async ({ page }, project: string, rate: string) => {
    await expect(
      page.locator('tr', { hasText: project }).getByRole('cell', { name: rate }),
    ).toBeVisible()
  },
)

When('この予算を承認する', async ({ page }) => {
  await page.getByRole('button', { name: 'この予算を承認する' }).click()
})

Then('予算は承認済になる', async ({ page }) => {
  await expect(page.getByText('承認済')).toBeVisible()
})

Then('予算バージョンの一覧に {string} の版がある', async ({ page }, status: string) => {
  const 一覧 = page.locator('.card', { hasText: '予算バージョン' })
  await expect(一覧.locator('.badge', { hasText: status })).toBeVisible()
})

Then('予算バージョンの一覧に {string} がある', async ({ page }, label: string) => {
  const 一覧 = page.locator('.card', { hasText: '予算バージョン' })
  await expect(一覧.getByRole('link', { name: label })).toBeVisible()
})

// ---- 部の画面 ----

Then(
  '課ごとの予算比較に課 {string} と次の金額が表示される:',
  async ({ page, 業務 }, department: string, table) => {
    const 予算比較 = page.locator('.card', { hasText: '課ごとの予算比較' })
    await expect(予算比較.getByRole('link', { name: 業務.課を取得(department).name })).toBeVisible()
    // 課の行と部合計の行の両方に出るため first で確かめる
    for (const row of table.hashes()) {
      await expect(予算比較.getByRole('cell', { name: row['金額'] }).first()).toBeVisible()
    }
  },
)

Then('課別の内訳に課 {string} が表示される', async ({ page, 業務 }, department: string) => {
  const 課別内訳 = page.locator('.card', { hasText: '課別の内訳' })
  await expect(課別内訳.getByRole('link', { name: 業務.課を取得(department).name })).toBeVisible()
})

When('部予算を承認する', async ({ page }) => {
  await page
    .locator('.card', { hasText: '部予算の承認' })
    .getByRole('button', { name: '部予算を承認する' })
    .click()
})

When('部承認を取り消す', async ({ page }) => {
  await page
    .locator('.card', { hasText: '部予算の承認' })
    .getByRole('button', { name: '部承認を取り消す' })
    .click()
})

Then('部予算は {string} になる', async ({ page }, status: string) => {
  await expect(page.locator('.card', { hasText: '部予算の承認' }).getByText(status)).toBeVisible()
})
