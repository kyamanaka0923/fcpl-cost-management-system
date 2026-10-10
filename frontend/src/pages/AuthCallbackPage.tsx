import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { completeLogin } from '../auth'

/**
 * Cognito Hosted UI からのコールバック。認可コードをトークンに交換し、
 * ログイン前に見ていた画面へ戻る。
 */
export default function AuthCallbackPage() {
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  // StrictMode の二重実行で同じ認可コードを2回交換しないようにする
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    started.current = true
    completeLogin(window.location.search)
      .then((returnTo) => navigate(returnTo, { replace: true }))
      .catch((e: Error) => setError(e.message))
  }, [navigate])

  return (
    <div className="card">
      {error ? (
        <>
          <p>{error}</p>
          <Link to="/">トップへ戻る</Link>
        </>
      ) : (
        <p>ログインしています…</p>
      )}
    </div>
  )
}
