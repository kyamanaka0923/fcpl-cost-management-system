import { expect, test } from '@playwright/test'
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { parse } from 'yaml'

/** 画面要件(requirements/screens/README.md)の、この検査で使う部分。 */
interface Screen {
  id: string
  title: string
  route: string
  states: { name: string; shows: string }[]
}

/** fixtures の 1 応答。 */
interface FixtureResponse {
  status: number
  body?: unknown
}

const screensDir = path.resolve(import.meta.dirname, '../../../requirements/screens')
const { screens } = parse(readFileSync(path.join(screensDir, 'screens.yaml'), 'utf8')) as { screens: Screen[] }

/** 画面の日時を固定する(半期の既定値などが日付で変わるため)。2026-10-26 12:00 JST */
const FIXED_TIME = new Date('2026-10-26T12:00:00+09:00')

/** "GET /api/projects/{id}" が、このリクエストに当たるか。 */
function matches(key: string, method: string, pathname: string): boolean {
  const [m, template] = key.split(' ')
  const pattern = new RegExp(`^${template.replace(/[.*+?^$()|[\]\\]/g, '\\$&').replace(/\{[^}]+\}/g, '[^/]+')}$`)
  return m === method && pattern.test(pathname)
}

for (const screen of screens) {
  for (const state of screen.states) {
    test(`${screen.id} ${screen.title}「${state.name}」: ${state.shows}`, async ({ page }) => {
      const fixtures = JSON.parse(
        readFileSync(path.join(screensDir, 'fixtures', screen.id, `${state.name}.json`), 'utf8'),
      ) as Record<string, FixtureResponse>
      const unexpected: string[] = []

      await page.route('**/api/**', (route) => {
        const req = route.request()
        const { pathname } = new URL(req.url())
        const hit = Object.entries(fixtures).find(([key]) => matches(key, req.method(), pathname))
        if (!hit) {
          unexpected.push(`${req.method()} ${pathname}`)
          return route.fulfill({ status: 501, json: { error: 'fixtures にない API' } })
        }
        const [, res] = hit
        return route.fulfill({
          status: res.status,
          contentType: 'application/json',
          body: res.body === undefined ? '' : JSON.stringify(res.body),
        })
      })
      await page.clock.setFixedTime(FIXED_TIME)

      await page.goto(screen.route)
      await page.waitForLoadState('networkidle')

      expect(unexpected, '画面が fixtures にない API を呼んだ(fixtures に足す)').toEqual([])
      await expect(page).toHaveScreenshot(`${screen.id}-${state.name}.png`, { fullPage: true })
    })
  }
}
