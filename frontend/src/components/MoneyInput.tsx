import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type ChangeEvent,
  type CompositionEvent,
  type CSSProperties,
} from 'react'

/** 生の数値文字列(カンマなし)を 3 桁区切りで表示する。空文字はそのまま。 */
function formatWithCommas(raw: string): string {
  return raw.replace(/\B(?=(\d{3})+(?!\d))/g, ',')
}

/** 小数点(半角「.」U+002E と全角「．」U+FF0E)。入力欄には入れない(BR-BUD-007、SPEC-002)。 */
const DECIMAL_POINT = /[.．]/

/** 入力文字列から数字だけを残す。 */
function clean(input: string): string {
  return input.replace(/\D/g, '')
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
 *
 * 1 円未満の端数を入れさせないため、小数点(「.」「．」)を含む文字列は入り方によらず受け付けない
 * (SPEC-002)。入ろうとする文字列に小数点が 1 つでもあれば、その入力では値も選択範囲も変えない。
 * 打鍵・貼り付け・ドロップは beforeinput で止める。
 * 入る前に止められない入り方は、change で入力前の状態に戻す。
 * IME は変換中の beforeinput が取り消せず、確定までに変換中の input が何回も出るので、
 * 変換中は親に値を渡さず(途中の数字を合計の表示や保存に流さない)、確定のときに、確定した文字列だけで判定する。
 * 小数点があれば変換の前の値と選択範囲に戻し、なければ確定後の値を 1 回だけ親に渡す。
 * ブラウザの自動入力は beforeinput を伴わず、change で戻す。
 */
export default function MoneyInput({ value, onChange, className, ...rest }: MoneyInputProps) {
  const ref = useRef<HTMLInputElement>(null)
  // フォーカス時に日本語入力(IME)を無効化して数字を直接打てるようにする。
  // ime-mode は Firefox/旧 Edge で有効。inputMode="numeric" はモバイルの数字キーボード指定(小数点キーを出さない)。
  const mergedClassName = ['money-input', className].filter(Boolean).join(' ')
  // onChange 由来の再フォーマット時にキャレット位置を復元するための、
  // 「キャレットより前にある数字・小数点の個数」。外部要因の value 変更時は null。
  const caretDigits = useRef<number | null>(null)
  // 直近の入力の前の選択範囲。小数点で入力を取り消すときに、選んでいた範囲ごと元に戻すために持つ。
  const lastSelection = useRef<[number, number]>([0, 0])

  // IME の変換中の表示と、変換の始まりの値・選択範囲。変換中(compositionstart〜compositionend)は
  // 親の onChange を呼ばず、表示だけをここで持つ(変換中の途中の文字列を親に知らせない)。
  const [composing, setComposing] = useState<string | null>(null)
  const compositionStart = useRef<{ raw: string; selection: [number, number] } | null>(null)

  const rememberSelection = () => {
    const el = ref.current
    if (el === null) return
    lastSelection.current = [
      el.selectionStart ?? el.value.length,
      el.selectionEnd ?? el.value.length,
    ]
  }

  // 入る前に止められる入り方(打鍵・貼り付け・ドロップ・IME)は、小数点を含むならその入力を取り消す。
  useEffect(() => {
    const el = ref.current
    if (el === null) return
    const onBeforeInput = (e: InputEvent) => {
      rememberSelection()
      const text = e.data ?? e.dataTransfer?.getData('text/plain') ?? ''
      if (DECIMAL_POINT.test(text) && e.cancelable) e.preventDefault()
    }
    el.addEventListener('beforeinput', onBeforeInput)
    return () => el.removeEventListener('beforeinput', onBeforeInput)
  }, [])

  const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
    const el = e.target
    if (compositionStart.current !== null) {
      // 変換中は途中の文字列を表示するだけで、親には渡さない。判定は確定のときに確定した文字列でする
      setComposing(el.value)
      return
    }
    if (DECIMAL_POINT.test(el.value)) {
      // 入力の前の値には小数点がないので、小数点は入ろうとした文字列にある。値を戻し、範囲を復元する。
      // (React が制御コンポーネントの値を戻したあとに選択範囲を戻すため、マイクロタスクで行う)
      const [start, end] = lastSelection.current
      el.value = formatWithCommas(value)
      queueMicrotask(() => ref.current?.setSelectionRange(start, end))
      return
    }
    const cursor = el.selectionStart ?? el.value.length
    caretDigits.current = el.value.slice(0, cursor).replace(/\D/g, '').length
    onChange(clean(el.value))
  }

  const handleCompositionStart = () => {
    const el = ref.current
    compositionStart.current = {
      raw: value,
      selection: [el?.selectionStart ?? 0, el?.selectionEnd ?? 0],
    }
    setComposing(formatWithCommas(value))
  }

  const handleCompositionEnd = (e: CompositionEvent<HTMLInputElement>) => {
    const started = compositionStart.current
    compositionStart.current = null
    if (started === null) return
    const el = e.currentTarget
    setComposing(null)
    if (DECIMAL_POINT.test(e.data)) {
      // 確定した文字列に小数点がある: 変換の前の値と選択範囲に戻す。親は呼ばない
      const [start, end] = started.selection
      el.value = formatWithCommas(started.raw)
      queueMicrotask(() => ref.current?.setSelectionRange(start, end))
      return
    }
    // 確定した文字列に小数点がない: 確定後の値で 1 回だけ親に渡す
    const cursor = el.selectionStart ?? el.value.length
    caretDigits.current = el.value.slice(0, cursor).replace(/\D/g, '').length
    onChange(clean(el.value))
  }

  useLayoutEffect(() => {
    if (caretDigits.current === null || ref.current === null) return
    const formatted = ref.current.value
    let pos = 0
    let digits = 0
    while (pos < formatted.length && digits < caretDigits.current) {
      if (/\d/.test(formatted[pos])) digits++
      pos++
    }
    ref.current.setSelectionRange(pos, pos)
    lastSelection.current = [pos, pos]
    caretDigits.current = null
  })

  return (
    <input
      ref={ref}
      type="text"
      inputMode="numeric"
      className={mergedClassName}
      value={composing ?? formatWithCommas(value)}
      onChange={handleChange}
      onSelect={rememberSelection}
      onCompositionStart={handleCompositionStart}
      onCompositionEnd={handleCompositionEnd}
      {...rest}
    />
  )
}
