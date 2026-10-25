import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactElement } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

/**
 * 画面をルーティングつきで描画する。route は画面のルート定義(例: '/departments/:departmentId')、
 * url は開く URL(例: '/departments/dept-1?half=2026-H1')。
 */
export function 画面を開く(page: ReactElement, route: string, url: string) {
  const user = userEvent.setup({ delay: null })
  const result = render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path={route} element={page} />
      </Routes>
    </MemoryRouter>,
  )
  return { user, ...result }
}
