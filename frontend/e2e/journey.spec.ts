import { expect, test, type Page } from '@playwright/test'

// 実行のたびに一意なプロジェクトコードを使う(同じDBで再実行しても衝突しない)
const projectCode = `SE-${Date.now() % 1_000_000}`
const projectName = `受託開発E2E-${projectCode}`

test.describe.configure({ mode: 'serial' })

async function プロジェクト詳細を開く(page: Page) {
  await page.goto('/')
  await page.getByRole('link', { name: projectName }).click()
  await expect(page.getByRole('heading', { name: new RegExp(projectCode) })).toBeVisible()
}

test('計画策定: プロジェクトを登録し売上予算と原価予算を承認できる', async ({ page }) => {
  // ---- プロジェクト登録 ----
  await page.goto('/')
  await page.getByLabel('コード').fill(projectCode)
  await page.getByLabel('名称').fill(projectName)
  await page.getByRole('button', { name: '作成' }).click()
  await expect(page.getByRole('link', { name: projectName })).toBeVisible()

  await プロジェクト詳細を開く(page)

  // ---- 売上予算(当初)の策定と承認 ----
  const 売上予算カード = page.locator('.card', { hasText: '売上予算バージョン' })
  await 売上予算カード.getByLabel('予算名').fill('当初売上予算')
  await 売上予算カード.getByRole('button', { name: 'ドラフト作成' }).click()

  // 明細: 案件A 200万
  await page.getByLabel('品目(案件名など)').fill('案件A')
  await page.getByLabel('年月').fill('2026-04')
  await page.getByLabel('金額(円)').fill('2000000')
  await page.getByRole('button', { name: '登録' }).click()
  await expect(page.getByRole('cell', { name: '案件A' })).toBeVisible()

  await page.getByRole('button', { name: 'この予算を承認する' }).click()
  await expect(page.getByText('承認済')).toBeVisible()

  // ---- 原価予算(当初)の策定と承認 ----
  await プロジェクト詳細を開く(page)
  const 原価予算カード = page.locator('.card', { hasText: '原価予算バージョン' })
  await 原価予算カード.getByLabel('予算名').fill('当初原価予算')
  await 原価予算カード.getByRole('button', { name: 'ドラフト作成' }).click()

  // 明細1: SE人件費 × 案件A × 140万
  await page.getByLabel('費目').selectOption({ label: 'SE人件費(LAB-SE)' })
  await page.getByLabel('売上対応品目(空欄 = 共通費)').fill('案件A')
  await page.getByLabel('年月').fill('2026-04')
  await page.getByLabel('金額(円)').fill('1400000')
  await page.getByRole('button', { name: '登録' }).click()
  await expect(page.getByRole('cell', { name: 'SE人件費' })).toBeVisible()

  // 明細2: 共通間接費(売上対応品目なし = 共通費)× 30万
  await page.getByLabel('費目').selectOption({ label: '共通間接費(OVH-COM)' })
  await page.getByLabel('売上対応品目(空欄 = 共通費)').fill('')
  await page.getByLabel('年月').fill('2026-04')
  await page.getByLabel('金額(円)').fill('300000')
  await page.getByRole('button', { name: '登録' }).click()
  await expect(page.getByRole('cell', { name: '(共通)' })).toBeVisible()

  await page.getByRole('button', { name: 'この予算を承認する' }).click()
  await expect(page.getByText('承認済')).toBeVisible()
})

test('実績入力: 売上実績と原価実績を計上できる', async ({ page }) => {
  // ---- 売上実績 ----
  await プロジェクト詳細を開く(page)
  await page.getByRole('button', { name: '売上実績入力' }).click()
  await page.getByLabel('品目(案件名など)').fill('案件A')
  await page.getByLabel('年月').fill('2026-04')
  await page.getByLabel('金額(円)').fill('2100000')
  await page.getByRole('button', { name: '計上' }).click()
  // 明細行と合計行の両方に同額が出るため first で確認
  await expect(page.getByRole('cell', { name: '¥2,100,000' }).first()).toBeVisible()

  // ---- 原価実績 ----
  await プロジェクト詳細を開く(page)
  await page.getByRole('button', { name: '原価実績入力' }).click()
  await page.getByLabel('費目').selectOption({ label: 'SE人件費(LAB-SE)' })
  await page.getByLabel('売上対応品目(空欄 = 共通費)').fill('案件A')
  await page.getByLabel('年月').fill('2026-04')
  await page.getByLabel('金額(円)').fill('1480000')
  await page.getByRole('button', { name: '計上' }).click()
  await expect(page.getByRole('cell', { name: '¥1,480,000' }).first()).toBeVisible()
})

test('分析: 原価差異と品目別の損益が表示される', async ({ page }) => {
  await プロジェクト詳細を開く(page)
  await page.getByRole('button', { name: '予実差異分析・損益' }).click()

  // ---- 原価差異タブ(既定) ----
  // 予算170万(140万+30万) / 実績148万 → 総差異 -22万(有利)。
  // 集計タイルと明細テーブルの両方に出るため first で確認
  await expect(page.getByText('¥1,700,000').first()).toBeVisible()
  await expect(page.getByText('¥-220,000').first()).toBeVisible()

  // ---- 損益(粗利)タブ ----
  await page.getByRole('button', { name: '損益(粗利)' }).click()
  const 品目別損益 = page.locator('.card', { hasText: '品目別 損益' })
  await expect(品目別損益.getByRole('cell', { name: '案件A' })).toBeVisible()
  await expect(品目別損益.getByRole('cell', { name: '(共通)' })).toBeVisible()
  // 案件Aの粗利実績: 210万 − 148万 = 62万
  await expect(品目別損益.getByRole('cell', { name: '¥620,000' }).first()).toBeVisible()
})

test('計画変更: 改定版の承認で旧バージョンが失効しバージョン比較で増減を確認できる', async ({ page }) => {
  // ---- 改定版(v2)を作成して増額・承認 ----
  await プロジェクト詳細を開く(page)
  const 原価予算カード = page.locator('.card', { hasText: '原価予算バージョン' })
  await 原価予算カード.getByLabel('予算名').fill('第2四半期改定')
  await 原価予算カード.getByRole('button', { name: 'ドラフト作成' }).click()

  await page.getByLabel('費目').selectOption({ label: 'SE人件費(LAB-SE)' })
  await page.getByLabel('売上対応品目(空欄 = 共通費)').fill('案件A')
  await page.getByLabel('年月').fill('2026-04')
  await page.getByLabel('金額(円)').fill('1500000')
  await page.getByRole('button', { name: '登録' }).click()
  await page.getByRole('button', { name: 'この予算を承認する' }).click()
  await expect(page.getByText('承認済')).toBeVisible()

  // ---- 旧バージョンは失効として履歴に残る ----
  await プロジェクト詳細を開く(page)
  const 一覧 = page.locator('.card', { hasText: '原価予算バージョン' })
  await expect(一覧.locator('.badge', { hasText: '失効' })).toBeVisible()
  await expect(一覧.getByRole('link', { name: '第2四半期改定' })).toBeVisible()

  // ---- バージョン比較: v1 → v2 で +10万 ----
  await page.getByRole('button', { name: '予算バージョン比較' }).click()
  await expect(page.getByText('¥+100,000').first()).toBeVisible()
})
