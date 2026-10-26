import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

/** テスト対象の frontend/src。テストからは `@app/...` で参照する。 */
const appSrc = fileURLToPath(new URL('../../../frontend/src', import.meta.url))

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@app': appSrc },
    // frontend/src の画面とテスト側の Testing Library が別々の React を読み込むと Hooks が壊れる。
    // このパッケージの node_modules にある1つの React に必ずそろえる。
    dedupe: ['react', 'react-dom', 'react-router-dom'],
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./support/setup.ts'],
    include: ['**/*.test.{ts,tsx}'],
    exclude: ['node_modules/**'],
    // 画面の初期半期など、実行日で結果が変わる処理があるため日本時間で動かす
    env: { TZ: 'Asia/Tokyo' },
  },
})
