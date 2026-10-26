import { defineConfig } from '@playwright/test'
import path from 'node:path'

// 承認した画面の置き場。手元で試すときだけ SCREENS_APPROVED_DIR で別の場所に向けられる
const approvedDir = process.env.SCREENS_APPROVED_DIR ?? path.resolve(import.meta.dirname, '../../../requirements/screens/approved')

export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.ts',
  timeout: 30_000,
  // 承認した画面がないときに、画像を勝手に作らない(作るのは人。tools/factory/update-approved-screens.sh)
  updateSnapshots: 'none',
  snapshotPathTemplate: `${approvedDir}/{arg}{ext}`,
  expect: {
    // 違ってよい画素の数は 0。画素ごとの色の差は Playwright の既定(threshold 0.2)までを同じとみなす。
    // 文言を 2 文字足しただけでも落ちるようにする(比べるのは CI と同じコンテナで撮った画像どうしなので、描画の揺れはない)
    toHaveScreenshot: { maxDiffPixels: 0, animations: 'disabled', caret: 'hide' },
  },
  use: {
    baseURL: 'http://localhost:5174',
    viewport: { width: 1280, height: 800 },
    deviceScaleFactor: 1,
    locale: 'ja-JP',
    timezoneId: 'Asia/Tokyo',
    colorScheme: 'light',
    trace: 'retain-on-failure',
  },
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  webServer: {
    // 画面だけを起動する。API は各テストが fixtures で返すので、バックエンドは起動しない
    command: 'npm run dev -- --port 5174 --strictPort',
    cwd: '../../../frontend',
    url: 'http://localhost:5174',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
})
