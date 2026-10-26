import MoneyInput from '@app/components/MoneyInput'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, test, vi } from 'vitest'

/** 実際の画面と同じく、親が生の値を持つ制御コンポーネントとして使う。 */
function 金額欄({ onRaw }: { onRaw?: (raw: string) => void }) {
  const [value, setValue] = useState('')
  return (
    <MoneyInput
      aria-label="金額"
      value={value}
      onChange={(raw) => {
        setValue(raw)
        onRaw?.(raw)
      }}
    />
  )
}

describe('金額入力', () => {
  test.each([
    ['1000', '1,000'],
    ['1234567', '1,234,567'],
    ['999', '999'],
    ['1234.5', '1,234.5'], // 小数部には区切りを入れない
  ])('"%s" と打つと入力中から "%s" と3桁区切りで表示する', async (typed, shown) => {
    const user = userEvent.setup({ delay: null })
    render(<金額欄 />)
    await user.type(screen.getByLabelText('金額'), typed)
    expect(screen.getByLabelText('金額')).toHaveValue(shown)
  })

  test.each([
    ['1a2b3', '123'], // 数字以外は捨てる
    ['¥1,000', '1000'], // 記号や貼り付けたカンマも捨てる
    ['1.2.3', '1.23'], // 小数点は最初の1つだけ残す
    ['-500', '500'], // マイナスは入力できない
  ])('"%s" と打つと親には生の値 "%s" を渡す', async (typed, raw) => {
    const user = userEvent.setup({ delay: null })
    const onRaw = vi.fn()
    render(<金額欄 onRaw={onRaw} />)
    await user.type(screen.getByLabelText('金額'), typed)
    expect(onRaw).toHaveBeenLastCalledWith(raw)
  })

  test('外から渡された値も3桁区切りで表示する', () => {
    render(<MoneyInput aria-label="金額" value="3000000" onChange={() => {}} />)
    expect(screen.getByLabelText('金額')).toHaveValue('3,000,000')
  })

  test('空の値は空欄のまま表示する', () => {
    render(<MoneyInput aria-label="金額" value="" onChange={() => {}} />)
    expect(screen.getByLabelText('金額')).toHaveValue('')
  })
})
