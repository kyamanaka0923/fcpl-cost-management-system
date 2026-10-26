import { setupServer } from 'msw/node'

/**
 * API のモックサーバ(MSW)。テストごとに `server.use(http.get('/api/...', ...))` で応答を決める。
 * 決めていない API が呼ばれたらテストを失敗させる(setup.ts の onUnhandledRequest: 'error')。
 */
export const server = setupServer()
