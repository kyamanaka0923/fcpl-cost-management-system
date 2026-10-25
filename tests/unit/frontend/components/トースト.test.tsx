import Toast from '@app/components/Toast'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, test, vi } from 'vitest'

describe('エラーのトースト', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  test('メッセージがあるときだけ表示する', () => {
    const { rerender } = render(<Toast message={null} onClose={() => {}} />)
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    rerender(<Toast message="案件コードが重複しています" onClose={() => {}} />)
    expect(screen.getByRole('alert')).toHaveTextContent('案件コードが重複しています')
  })

  test('×ボタンで閉じられる', async () => {
    const user = userEvent.setup({ delay: null })
    const onClose = vi.fn()
    render(<Toast message="失敗しました" onClose={onClose} />)
    await user.click(screen.getByRole('button', { name: 'エラーを閉じる' }))
    expect(onClose).toHaveBeenCalledOnce()
  })

  test('指定した時間が過ぎると自動で閉じる', () => {
    vi.useFakeTimers()
    const onClose = vi.fn()
    render(<Toast message="失敗しました" onClose={onClose} duration={6000} />)
    act(() => vi.advanceTimersByTime(5999))
    expect(onClose).not.toHaveBeenCalled()
    act(() => vi.advanceTimersByTime(1))
    expect(onClose).toHaveBeenCalledOnce()
  })

  test('別のメッセージに変わると自動で閉じるまでの時間を数え直す', () => {
    vi.useFakeTimers()
    const onClose = vi.fn()
    const { rerender } = render(<Toast message="1件目" onClose={onClose} duration={6000} />)
    act(() => vi.advanceTimersByTime(4000))
    rerender(<Toast message="2件目" onClose={onClose} duration={6000} />)
    act(() => vi.advanceTimersByTime(4000)) // 1件目からは8秒経つが、2件目からは4秒
    expect(onClose).not.toHaveBeenCalled()
    act(() => vi.advanceTimersByTime(2000))
    expect(onClose).toHaveBeenCalledOnce()
  })

  test('親が再描画されて閉じる処理が作り直されても、時間は数え直さない', () => {
    vi.useFakeTimers()
    const first = vi.fn()
    const second = vi.fn()
    const { rerender } = render(<Toast message="失敗しました" onClose={first} duration={6000} />)
    act(() => vi.advanceTimersByTime(4000))
    rerender(<Toast message="失敗しました" onClose={second} duration={6000} />)
    act(() => vi.advanceTimersByTime(2000))
    expect(first).not.toHaveBeenCalled()
    expect(second).toHaveBeenCalledOnce() // 最新の閉じる処理を呼ぶ
  })
})
