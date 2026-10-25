import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from './server'

// 画面のコードは fetch('/api/...') と相対 URL で呼ぶ。Node の fetch は相対 URL を解釈できないため、
// jsdom のページの URL(http://localhost:3000/)を基準に絶対 URL へ直してから渡す。
const nodeFetch = globalThis.fetch
globalThis.fetch = (input: RequestInfo | URL, init?: RequestInit) =>
  nodeFetch(typeof input === 'string' ? new URL(input, window.location.href) : input, init)

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterEach(() => {
  cleanup()
  server.resetHandlers()
})
afterAll(() => server.close())
