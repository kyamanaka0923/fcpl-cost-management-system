import MoneyInput from '@app/components/MoneyInput'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, test, vi } from 'vitest'

/** 実際の画面と同じく、親が生の値を持つ制御コンポーネントとして使う。 */
function 金額欄({ onRaw, initial = '' }: { onRaw?: (raw: string) => void; initial?: string }) {
  const [value, setValue] = useState(initial)
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

/** 入力欄(テキストボックス)。 */
const 入力欄 = () => screen.getByLabelText('金額') as HTMLInputElement

/**
 * ブラウザが文字列を入れる入り方のうち、user-event では起こせないものを再現する。
 * 本物のブラウザと同じく、先に取り消せる beforeinput を出し、取り消されなければ選択範囲を置き換えて input を出す。
 */
function 入れる(inputType: string, text: string) {
  const el = 入力欄()
  const event = new InputEvent('beforeinput', {
    inputType,
    data: text,
    bubbles: true,
    cancelable: true,
  })
  el.dispatchEvent(event)
  if (event.defaultPrevented) return
  const start = el.selectionStart ?? el.value.length
  const end = el.selectionEnd ?? el.value.length
  fireEvent.change(el, { target: { value: el.value.slice(0, start) + text + el.value.slice(end) } })
}

/** ブラウザの自動入力。beforeinput を出さず、値だけが差し替わる。 */
function 自動入力(text: string) {
  fireEvent.change(入力欄(), { target: { value: text } })
}

describe('金額入力', () => {
  test.each([
    ['1000', '1,000'],
    ['1234567', '1,234,567'],
    ['999', '999'],
  ])('"%s" と打つと入力中から "%s" と3桁区切りで表示する', async (typed, shown) => {
    const user = userEvent.setup({ delay: null })
    render(<金額欄 />)
    await user.type(screen.getByLabelText('金額'), typed)
    expect(screen.getByLabelText('金額')).toHaveValue(shown)
  })

  test.each([
    ['1a2b3', '123'], // 数字以外は捨てる
    ['¥1,000', '1000'], // 記号や貼り付けたカンマも捨てる
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

  describe('小数点は入らない(1 文字ずつの打鍵)', () => {
    test.each([
      ['100.5', '1,005'], // 小数点だけが入らない
      ['100．5', '1,005'], // 全角の小数点も同じ
      ['100.', '100'], // 末尾
      ['.5', '5'], // 先頭
      ['.', ''], // 小数点だけ
      ['0', '0'], // 下限の値は入る
      ['2000000', '2,000,000'],
    ])('空の欄に %s と打つと %s になる', async (typed, shown) => {
      const user = userEvent.setup({ delay: null })
      const onRaw = vi.fn()
      render(<金額欄 onRaw={onRaw} />)
      await user.type(入力欄(), typed)
      expect(入力欄()).toHaveValue(shown)
      // 欄が親に渡す値にも小数点は現れない
      for (const [raw] of onRaw.mock.calls) expect(raw).toMatch(/^\d*$/)
    })

    test.each(['.', '．'])(
      '範囲「000,000」を選んで %s を打っても、値も選んだ範囲も変わらない',
      async (point) => {
        const user = userEvent.setup({ delay: null })
        render(<金額欄 initial="3000000" />)
        await user.type(入力欄(), point, { initialSelectionStart: 2, initialSelectionEnd: 9 })
        expect(入力欄()).toHaveValue('3,000,000')
        expect([入力欄().selectionStart, 入力欄().selectionEnd]).toEqual([2, 9])
      },
    )

    test('値の全体を選んで小数点を打っても、値も選んだ範囲も変わらない', async () => {
      const user = userEvent.setup({ delay: null })
      render(<金額欄 initial="3000000" />)
      await user.type(入力欄(), '.', { initialSelectionStart: 0, initialSelectionEnd: 9 })
      expect(入力欄()).toHaveValue('3,000,000')
      expect([入力欄().selectionStart, 入力欄().selectionEnd]).toEqual([0, 9])
    })

    test('範囲「000,000」を選んで 5 を打つと、選んだ範囲は数字で置き換わる', async () => {
      const user = userEvent.setup({ delay: null })
      render(<金額欄 initial="3000000" />)
      await user.type(入力欄(), '5', { initialSelectionStart: 2, initialSelectionEnd: 9 })
      expect(入力欄()).toHaveValue('35')
    })
  })

  describe('文字列がまとめて入る入り方では、小数点が 1 つでもあれば全体が入らない', () => {
    /** 入り方ごとの入れ方。どの入り方でも結果は同じになる(入り方を見分けない)。 */
    const 入り方: [string, (text: string) => Promise<void> | void][] = [
      [
        '貼り付け',
        async (text) => {
          const user = userEvent.setup({ delay: null })
          入力欄().focus()
          await user.paste(text)
        },
      ],
      ['テキストのドラッグ&ドロップ', (text) => 入れる('insertFromDrop', text)],
      ['IME の確定', (text) => 入れる('insertCompositionText', text)],
      ['ブラウザの自動入力', (text) => 自動入力(text)],
    ]

    describe.each(入り方)('%s', (_name, 入れる方) => {
      test.each([
        ['', '100.5', ''], // 文字列の一部も入らない
        ['2000', '100.5', '2,000'], // 値があるときも前のまま
        ['2000', '0.5', '2,000'], // 1 円未満だけの端数
        ['2000', '100.0', '2,000'], // 小数点以下が 0 でも、文字に小数点があれば入らない
        ['', '100.', ''], // 末尾
        ['', '.5', ''], // 先頭
        ['3000000', '100．5', '3,000,000'], // 全角
      ])(
        '値 "%s" の欄に "%s" を入れようとしても、欄は "%s" のまま',
        async (initial, text, shown) => {
          const onRaw = vi.fn()
          render(<金額欄 initial={initial} onRaw={onRaw} />)
          await 入れる方(text)
          expect(入力欄()).toHaveValue(shown)
          expect(onRaw).not.toHaveBeenCalled()
        },
      )

      test.each([
        ['0', '0'], // 下限の値は入る
        ['1,000', '1,000'], // カンマつきは今までどおり入る
        ['2000000', '2,000,000'],
        ['-1', '1'], // マイナスは捨てる(いまのまま)
      ])('空の欄に "%s" を入れると "%s" になる', async (text, shown) => {
        render(<金額欄 />)
        await 入れる方(text)
        expect(入力欄()).toHaveValue(shown)
      })
    })

    test('小数点を含む自動入力のあと、選んでいた範囲は入力の前のままである', async () => {
      const user = userEvent.setup({ delay: null })
      render(<金額欄 initial="3000000" />)
      await user.type(入力欄(), '{Escape}', { initialSelectionStart: 2, initialSelectionEnd: 9 })
      自動入力('3,000,100.5')
      await Promise.resolve()
      expect([入力欄().selectionStart, 入力欄().selectionEnd]).toEqual([2, 9])
    })
  })
})
