import { defineConfig } from '@playwright/test'
import os from 'node:os'
import path from 'node:path'

// 実行ごとに独立した SQLite データベースを使う
const dbPath = path.join(os.tmpdir(), `cm-browser-e2e-${Date.now()}.db`)

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  // CI では失敗時のトレースを残す
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
    launchOptions: {
      // サンドボックス環境などで同梱ブラウザを使う場合に上書きできる
      executablePath: process.env.PW_CHROMIUM || undefined,
    },
  },
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  webServer: [
    {
      command: 'dotnet run --project ../backend/src/CostManagement.WebApi',
      url: 'http://localhost:5100/api/cost-elements',
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
      env: {
        ASPNETCORE_URLS: 'http://localhost:5100',
        ConnectionStrings__Default: `Data Source=${dbPath}`,
      },
    },
    {
      command: 'npm run dev',
      url: 'http://localhost:5173',
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
  ],
})
