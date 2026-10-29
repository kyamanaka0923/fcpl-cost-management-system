import { expect, type Locator, type Page } from '@playwright/test'
import { Given, Then, When } from '../support/fixtures'

/*
 * 金額の入力欄が小数点を入れないこと(FR-4.13・FR-4.14・FR-6.9・FR-6.10、SPEC-002)の確認。
 * 入力欄ごと・画面ごとに通しで確かめる。入り方の網羅は単体(tests/unit/frontend)で持つため、
 * ここでは入り方ごとに「同じ入り方に当たるイベント」を起こす。
 *  - 打鍵・貼り付け・ドラッグ&ドロップ: ブラウザの本物の操作
 *  - IME の確定: CDP で変換中の文字列を入れて確定する
 *  - ブラウザの自動入力: beforeinput を伴わず値だけが差し替わる入力
 */

type 引数 = (string | number)[]
type 入力欄の取り方 = (page: Page, ...args: 引数) => Locator

/** 入力欄を使うステップの前半(「…の入力欄」まで)と、その入力欄の取り方。 */
const 入力欄たち: [string, 入力欄の取り方][] = [
  [
    '案件 {string} の {string} の金額の入力欄',
    (page, project, category) => page.getByLabel(`${project} ${category}`, { exact: true }),
  ],
  [
    '期間費用 {string} の金額の入力欄',
    (page, element) => page.getByLabel(`${element} 金額`, { exact: true }),
  ],
  [
    '期間費用 {string} の明細 {string} の金額の入力欄',
    (page, element, detail) => page.getByLabel(`${element} ${detail} 金額`, { exact: true }),
  ],
  [
    '期間費用 {string} の明細を追加する行の金額の入力欄',
    (page, element) => page.getByLabel(`${element} 明細金額`, { exact: true }),
  ],
  [
    // 月別金額は月次入力のダイアログの中。半期内の位置で指定する(月名は実行日で変わる)
    '月 {int} の金額の入力欄',
    (page, month) =>
      page
        .getByRole('dialog')
        .getByLabel(/^月次 \d+月$/)
        .nth(Number(month) - 1),
  ],
  ['計上金額の入力欄', (page) => page.getByLabel('金額(円)')],
]

/** 入れようとする文字列を、本物のクリップボード経由で貼り付ける。 */
async function 貼り付ける(page: Page, input: Locator, text: string) {
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write'])
  await page.evaluate((t) => navigator.clipboard.writeText(t), text)
  await input.focus()
  await page.keyboard.press('ControlOrMeta+V')
}

/** 入れようとする文字列を選択した別の要素から、本物のドラッグ&ドロップで入力欄に落とす。 */
async function ドロップする(page: Page, input: Locator, text: string) {
  const source = await page.evaluateHandle((t) => {
    const el = document.createElement('span')
    el.id = 'e2e-drag-source'
    el.textContent = t
    el.style.cssText =
      'position:fixed;left:8px;bottom:8px;z-index:9999;background:#fff;color:#000;padding:2px'
    document.body.appendChild(el)
    const range = document.createRange()
    range.selectNodeContents(el)
    const selection = window.getSelection()
    selection?.removeAllRanges()
    selection?.addRange(range)
    return el
  }, text)
  const from = (await source.asElement()?.boundingBox())!
  const to = (await input.boundingBox())!
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
  await page.mouse.down()
  await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 10 })
  await page.mouse.up()
  await page.evaluate(() => document.getElementById('e2e-drag-source')?.remove())
}

/** IME で文字列を変換して確定する(CDP の IME 入力)。 */
async function imeで確定する(page: Page, input: Locator, text: string) {
  await input.focus()
  const client = await page.context().newCDPSession(page)
  await client.send('Input.imeSetComposition', {
    text,
    selectionStart: text.length,
    selectionEnd: text.length,
  })
  // 確定: 変換中の文字列を同じ文字列で確定する
  await client.send('Input.insertText', { text })
  await client.detach()
}

/** ブラウザの自動入力。beforeinput を出さず、値だけを差し替えて input を出す。 */
async function 自動入力する(input: Locator, text: string) {
  await input.focus()
  await input.evaluate((el: HTMLInputElement, t) => {
    const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!
    setter.call(el, t)
    el.dispatchEvent(new Event('input', { bubbles: true }))
  }, text)
}

/** 入力欄の値のうち、指定した文字列の範囲を選ぶ。 */
async function 範囲を選ぶ(input: Locator, text: string) {
  await input.focus()
  await input.evaluate((el: HTMLInputElement, t) => {
    const start = el.value.indexOf(t)
    if (start < 0) throw new Error(`入力欄の値「${el.value}」に「${t}」がありません`)
    el.setSelectionRange(start, start + t.length)
  }, text)
}

/**
 * ステップの関数を、パターンの引数の数ちょうどの引数を持つ関数にする
 * (playwright-bdd が関数の引数の数とパターンの {string}・{int} の数を突き合わせて検査するため、残余引数は使えない)。
 */
function 手順(個数: number, body: (page: Page, args: 引数) => Promise<void>) {
  type 画面 = { page: Page }
  switch (個数) {
    case 0:
      return async ({ page }: 画面) => body(page, [])
    case 1:
      return async ({ page }: 画面, a: string | number) => body(page, [a])
    case 2:
      return async ({ page }: 画面, a: string | number, b: string | number) => body(page, [a, b])
    case 3:
      return async ({ page }: 画面, a: string | number, b: string | number, c: string | number) =>
        body(page, [a, b, c])
    case 4:
      return async (
        { page }: 画面,
        a: string | number,
        b: string | number,
        c: string | number,
        d: string | number,
      ) => body(page, [a, b, c, d])
    default:
      throw new Error(`未対応の引数の数です: ${個数}`)
  }
}

const 引数の数 = (pattern: string) => (pattern.match(/\{(string|int)\}/g) ?? []).length

/** 入力欄ごとに、同じ形のステップを登録する。 */
for (const [prefix, 取る] of 入力欄たち) {
  const 前半 = 引数の数(prefix)
  const 分ける = (args: 引数) => ({
    入力欄: (page: Page) => 取る(page, ...args.slice(0, 前半)),
    後半: args.slice(前半) as string[],
  })
  const 登録 = (
    登録先: typeof When,
    suffix: string,
    action: (page: Page, 入力欄: Locator, 後半: string[]) => Promise<void>,
  ) => {
    const pattern = `${prefix}${suffix}`
    登録先(
      pattern,
      手順(引数の数(pattern), async (page, args) => {
        const { 入力欄, 後半 } = 分ける(args)
        await action(page, 入力欄(page), 後半)
      }),
    )
  }

  const 打鍵する = async (page: Page, input: Locator, [text]: string[]) => {
    await input.focus()
    await page.keyboard.type(text)
  }
  登録(When, 'に {string} と打鍵する', 打鍵する)
  登録(Given, 'に {string} と打鍵している', 打鍵する)
  登録(When, 'の末尾に {string} と打鍵する', async (page, input, [text]) => {
    await input.focus()
    await page.keyboard.press('End')
    await page.keyboard.type(text)
  })
  登録(When, 'の {string} を選んで {string} と打鍵する', async (page, input, [range, text]) => {
    await 範囲を選ぶ(input, range)
    await page.keyboard.type(text)
  })
  登録(When, 'に {string} を貼り付ける', (page, input, [text]) => 貼り付ける(page, input, text))
  登録(When, 'に {string} をドラッグ&ドロップする', (page, input, [text]) =>
    ドロップする(page, input, text),
  )
  登録(When, 'に IME で {string} を確定する', (page, input, [text]) =>
    imeで確定する(page, input, text),
  )
  登録(When, 'にブラウザの自動入力で {string} を入れる', (_page, input, [text]) =>
    自動入力する(input, text),
  )
  登録(Then, 'の値は {string} である', async (_page, input, [value]) => {
    await expect(input).toHaveValue(value)
  })
  登録(Then, 'は空である', async (_page, input) => {
    await expect(input).toHaveValue('')
  })
}

// ---- 月次入力のダイアログ ----

Given(
  '案件 {string} の {string} の月次入力を開いている',
  async ({ page }, project: string, category: string) => {
    await page.getByRole('button', { name: `${project} ${category} を月次入力` }).click()
    const dialog = page.getByRole('dialog')
    await expect(dialog).toBeVisible()
    await expect(dialog.getByLabel(/^月次 \d+月$/)).toHaveCount(6)
  },
)
