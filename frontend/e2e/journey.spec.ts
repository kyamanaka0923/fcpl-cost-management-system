import { expect, test, type Page } from '@playwright/test'

// 実行のたびに一意なコードを使う(同じDBで再実行しても衝突しない)
const suffix = Date.now() % 1_000_000
const divCode = `DIV-${suffix}`
const divName = `営業本部E2E-${suffix}`
const deptCode = `DEV-${suffix}`
const deptName = `開発課E2E-${suffix}`
const projectACode = `PJA-${suffix}`
const projectBCode = `PJB-${suffix}`

test.describe.configure({ mode: 'serial' })

async function 部詳細を開く(page: Page) {
  await page.goto('/')
  await page.getByRole('link', { name: divName }).click()
  await expect(page.getByRole('heading', { name: new RegExp(divCode) })).toBeVisible()
}

async function 課詳細を開く(page: Page) {
  await 部詳細を開く(page)
  await page.getByRole('link', { name: deptName }).click()
  await expect(page.getByRole('heading', { name: new RegExp(deptCode) })).toBeVisible()
}

test('計画策定: 部と課を登録し予算編集で案件別に金額を入力して承認できる', async ({ page }) => {
  // ---- 部の登録 ----
  await page.goto('/')
  await page.getByLabel('部コード').fill(divCode)
  await page.getByLabel('部名').fill(divName)
  await page.getByRole('button', { name: '登録' }).click()
  await expect(page.getByRole('link', { name: divName })).toBeVisible()

  // ---- 部詳細で課を登録 ----
  await 部詳細を開く(page)
  await page.getByLabel('課コード').fill(deptCode)
  await page.getByLabel('課名').fill(deptName)
  await page.getByRole('button', { name: '登録' }).click()
  await expect(page.getByRole('link', { name: deptName })).toBeVisible()

  await 課詳細を開く(page)

  // ---- 当初予算のドラフト作成(予算編集ページへ遷移) ----
  const 予算カード = page.locator('.card', { hasText: '予算バージョン' })
  await 予算カード.getByLabel('予算名').fill('当初予算')
  await 予算カード.getByRole('button', { name: 'ドラフト作成' }).click()
  await expect(page.getByRole('heading', { name: /当初予算/ })).toBeVisible()

  // ---- 予算編集の中で案件を追加する(案件マスタ画面は廃止) ----
  const 案件を追加 = async (code: string, name: string) => {
    const form = page.locator('form', { hasText: '案件コード' })
    await form.getByLabel('案件コード').fill(code)
    await form.getByLabel('案件名').fill(name)
    await form.getByRole('button', { name: '追加' }).click()
    await expect(page.getByRole('cell', { name: new RegExp(name) })).toBeVisible()
  }
  await 案件を追加(projectACode, '案件A')
  await 案件を追加(projectBCode, '案件B')

  // 案件Bを終了しても、予算を承認するまでは金額を編集できる
  const 案件B行 = page.locator('tr', { hasText: '案件B' })
  await 案件B行.getByRole('button', { name: '終了' }).click()
  await expect(案件B行.locator('.badge', { hasText: '終了' })).toBeVisible()

  // ---- 案件×区分のグリッドで金額を入力(セルを離れると自動保存) ----
  const セル入力 = async (案件: string, 区分: string, 金額: string) => {
    const cell = page.getByLabel(`${案件} ${区分}`)
    await cell.fill(金額)
    await cell.blur()
  }
  await セル入力('案件A', '売上高', '2000000')
  await セル入力('案件A', '加工費', '1400000')
  // 終了済みの案件Bでも金額を入力・保存できる
  await セル入力('案件B', '売上高', '1000000')
  await セル入力('案件B', '外注費', '700000')

  // ---- 期間費用(費目別。案件と同じくセルに直接入力) ----
  const 人件費セル = page.getByLabel('人件費 金額')
  await 人件費セル.fill('300000')
  await 人件費セル.blur()

  // 課の区分合計 = 案件明細の合計(サマリタイルで確認)
  await expect(page.getByText('¥3,000,000').first()).toBeVisible() // 売上高
  await expect(page.getByText('¥600,000').first()).toBeVisible()   // 計画損益

  // ---- 承認 ----
  await page.getByRole('button', { name: 'この予算を承認する' }).click()
  await expect(page.getByText('承認済')).toBeVisible()

  // ---- 部詳細の予算(計画)タブに配下課の予算が集計される(既定タブ) ----
  await 部詳細を開く(page)
  // 部の計画損益の粗利率(60万 / 300万 = 20.0%)
  await expect(page.getByText('粗利率 20.0%').first()).toBeVisible()
  const 予算比較 = page.locator('.card', { hasText: '課ごとの予算比較' })
  await expect(予算比較.getByRole('link', { name: deptName })).toBeVisible()
  // 課ごとの予算比較グリッド: 売上高 300万・計画損益 60万(課行と部合計行に出るため first)
  await expect(予算比較.getByRole('cell', { name: '¥3,000,000' }).first()).toBeVisible()
  await expect(予算比較.getByRole('cell', { name: '¥600,000' }).first()).toBeVisible()

  // 予実サマリタブに切り替えると予実の内訳が見られる
  await page.getByRole('button', { name: '予実サマリ' }).click()
  await expect(page.getByRole('heading', { name: '区分別の予実(部合計)' })).toBeVisible()
  const 課別内訳 = page.locator('.card', { hasText: '課別の内訳' })
  await expect(課別内訳.getByRole('link', { name: deptName })).toBeVisible()

  // 配下課がすべて承認済みなので部予算を承認できる
  const 承認カード = page.locator('.card', { hasText: '部予算の承認' })
  await 承認カード.getByRole('button', { name: '部予算を承認する' }).click()
  await expect(承認カード.getByText('承認済')).toBeVisible()
  // 取り消せる
  await 承認カード.getByRole('button', { name: '部承認を取り消す' }).click()
  await expect(承認カード.getByText('未承認')).toBeVisible()
})

test('実績入力: 区分ごとに案件別・費目別の実績を計上できる', async ({ page }) => {
  await 課詳細を開く(page)
  await page.getByRole('button', { name: '実績入力' }).click()

  const 実績を計上 = async (区分: string, 相手: string, 金額: string) => {
    await page.getByLabel('区分').selectOption({ label: 区分 })
    if (区分 === '期間費用') {
      await page.getByLabel('費目').selectOption({ label: 相手 })
    } else {
      await page.getByLabel('案件').selectOption({ label: 相手 })
    }
    await page.getByLabel(/金額/).fill(金額)
    await page.getByRole('button', { name: '計上' }).click()
  }

  await 実績を計上('売上高', `案件A(${projectACode})`, '2100000')
  await 実績を計上('売上高', `案件B(${projectBCode})`, '900000')
  await 実績を計上('加工費', `案件A(${projectACode})`, '1480000')
  await 実績を計上('外注費', `案件B(${projectBCode})`, '650000')
  await 実績を計上('期間費用', '人件費(PERSONNEL)', '320000')

  // 明細行と合計行の両方に同額が出るため first で確認
  await expect(page.getByRole('cell', { name: '¥2,100,000' }).first()).toBeVisible()
  await expect(page.getByRole('cell', { name: '¥900,000' }).first()).toBeVisible()   // 案件B 売上
  await expect(page.getByRole('cell', { name: '¥650,000' }).first()).toBeVisible()   // 案件B 外注
  await expect(page.getByRole('cell', { name: '¥320,000' }).first()).toBeVisible()
})

test('分析: 区分別の予実差異と案件別の損益が表示される', async ({ page }) => {
  await 課詳細を開く(page)
  await page.getByRole('button', { name: '予実差異分析・損益' }).click()

  // ---- 予実差異タブ(既定) ----
  // コスト: 予算240万(140万+70万+30万) / 実績245万 → 差異 +5万(不利)。
  // 集計タイルと明細テーブルの両方に出るため first で確認
  await expect(page.getByText('¥2,400,000').first()).toBeVisible()
  await expect(page.getByText('¥+50,000').first()).toBeVisible()

  // 加工費の明細に案件名が表示される
  const 加工費カード = page.locator('.card', { hasText: '加工費 差異明細' })
  await expect(加工費カード.getByRole('cell', { name: '案件A' })).toBeVisible()

  // ---- 損益タブ ----
  await page.getByRole('button', { name: '損益', exact: true }).click()
  const 案件別損益 = page.locator('.card', { hasText: '案件別 損益' }).first()
  await expect(案件別損益.getByRole('cell', { name: /案件A/ })).toBeVisible()
  await expect(案件別損益.getByRole('cell', { name: '期間費用(課共通)' })).toBeVisible()
  // 案件Aの損益実績: 210万 − 148万 = 62万
  await expect(案件別損益.getByRole('cell', { name: '¥620,000' }).first()).toBeVisible()
  // 課全体の損益実績: 300万 − 245万 = 55万
  await expect(page.getByText('¥550,000').first()).toBeVisible()
})

test('計画変更: 改定版の承認で旧バージョンが失効しバージョン比較で増減を確認できる', async ({ page }) => {
  // ---- 改定版(v2)を作成して加工費を増額・承認 ----
  await 課詳細を開く(page)
  const 予算カード = page.locator('.card', { hasText: '予算バージョン' })
  await 予算カード.getByLabel('予算名').fill('上期見直し')
  await 予算カード.getByRole('button', { name: 'ドラフト作成' }).click()
  await expect(page.getByRole('heading', { name: /上期見直し/ })).toBeVisible()

  // 引き継いだ案件Aの加工費セルを 140万 → 150万 に変更(セルを離れると自動保存)
  const 加工費セル = page.getByLabel('案件A 加工費')
  await 加工費セル.fill('1500000')
  await 加工費セル.blur()
  await page.getByRole('button', { name: 'この予算を承認する' }).click()
  await expect(page.getByText('承認済')).toBeVisible()

  // ---- 旧バージョンは失効として履歴に残る ----
  await 課詳細を開く(page)
  const 一覧 = page.locator('.card', { hasText: '予算バージョン' })
  await expect(一覧.locator('.badge', { hasText: '失効' })).toBeVisible()
  await expect(一覧.getByRole('link', { name: '上期見直し' })).toBeVisible()

  // ---- バージョン比較: v1 → v2 で加工費 +10万 ----
  await page.getByRole('button', { name: '予算バージョン比較' }).click()
  const 加工費増減 = page.locator('.stat-tile', { hasText: '加工費 増減' })
  await expect(加工費増減.getByText('¥+100,000')).toBeVisible()
})
