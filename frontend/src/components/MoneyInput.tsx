import { useLayoutEffect, useRef, type ChangeEvent, type CSSProperties } from 'react'

/** 生の数値文字列(カンマなし)を 3 桁区切りで表示する。空文字はそのまま。 */
function formatWithCommas(raw: string): string {
  if (raw === '') return ''
  const dotIndex = raw.indexOf('.')
  const intPart = dotIndex === -1 ? raw : raw.slice(0, dotIndex)
  const decPart = dotIndex === -1 ? '' : raw.slice(dotIndex) // 先頭の '.' を含む
  const withCommas = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, ',')
  return withCommas + decPart
}

/** 入力文字列から数字と小数点だけを残す(小数点は最初の1つのみ)。 */
function clean(input: string): string {
  const digitsAndDots = input.replace(/[^\d.]/g, '')
  const firstDot = digitsAndDots.indexOf('.')
  if (firstDot === -1) return digitsAndDots
  return digitsAndDots.slice(0, firstDot + 1) + digitsAndDots.slice(firstDot + 1).replace(/\./g, '')
}

interface MoneyInputProps {
  /** 生の数値文字列(カンマなし)。空文字可。 */
  value: string
  /** カンマを除いた生の数値文字列を返す。 */
  onChange: (rawValue: string) => void
  onBlur?: () => void
  className?: string
  style?: CSSProperties
  placeholder?: string
  disabled?: boolean
  required?: boolean
  id?: string
  'aria-label'?: string
}

/**
 * 金額入力ボックス。編集中から 3 桁カンマ区切りで表示する制御コンポーネント。
 * 値は生の数値文字列(カンマなし)でやり取りする。
 */
export default function MoneyInput({ value, onChange, ...rest }: MoneyInputProps) {
  const ref = useRef<HTMLInputElement>(null)
  // onChange 由来の再フォーマット時にキャレット位置を復元するための、
  // 「キャレットより前にある数字・小数点の個数」。外部要因の value 変更時は null。
  const caretDigits = useRef<number | null>(null)

  const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
    const el = e.target
    const cursor = el.selectionStart ?? el.value.length
    caretDigits.current = el.value.slice(0, cursor).replace(/[^\d.]/g, '').length
    onChange(clean(el.value))
  }

  useLayoutEffect(() => {
    if (caretDigits.current === null || ref.current === null) return
    const formatted = ref.current.value
    let pos = 0
    let digits = 0
    while (pos < formatted.length && digits < caretDigits.current) {
      if (/[\d.]/.test(formatted[pos])) digits++
      pos++
    }
    ref.current.setSelectionRange(pos, pos)
    caretDigits.current = null
  })

  return (
    <input
      ref={ref}
      type="text"
      inputMode="decimal"
      value={formatWithCommas(value)}
      onChange={handleChange}
      {...rest}
    />
  )
}
