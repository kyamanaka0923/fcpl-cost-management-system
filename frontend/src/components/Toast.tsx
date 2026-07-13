import { useEffect, useRef } from 'react'

/**
 * エラー等を画面隅に固定表示するトースト通知。
 * ページ最上部のバナーと違い、スクロール位置に関わらず必ず視界に入る(Issue #4)。
 * message が非 null の間だけ表示し、一定時間後に自動で閉じる(手動でも閉じられる)。
 */
export default function Toast({
  message,
  onClose,
  duration = 6000,
}: {
  message: string | null
  onClose: () => void
  duration?: number
}) {
  // onClose の参照だけを最新に保ち、message が変わったときだけタイマーを張り直す。
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose

  useEffect(() => {
    if (!message) return
    const timer = setTimeout(() => onCloseRef.current(), duration)
    return () => clearTimeout(timer)
  }, [message, duration])

  if (!message) return null

  return (
    <div className="toast-region" role="alert" aria-live="assertive">
      <div className="toast toast-error">
        <span className="toast-message">{message}</span>
        <button type="button" className="toast-close" aria-label="エラーを閉じる" onClick={onClose}>
          ×
        </button>
      </div>
    </div>
  )
}
