import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, type CostElement } from '../api'

/**
 * 費目マスタ管理(システム共通)。
 * 期間費用に使う費目はシステム全体で定義し、どの部・課の予算でも共通で使う。
 */
export default function CostElementsPage() {
  const [elements, setElements] = useState<CostElement[]>([])
  const [error, setError] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [saving, setSaving] = useState(false)

  const load = () => {
    api.listCostElements().then(setElements).catch((e: Error) => setError(e.message))
  }
  useEffect(load, [])

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await api.createCostElement({ code, name })
      setCode('')
      setName('')
      load()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">部一覧</Link> / 費目マスタ
      </div>
      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>費目マスタ(期間費用)</h2>
        <p className="muted small">
          期間費用の費目はシステム全体で定義する共通マスタです。ここで追加した費目は、
          すべての部・課の予算編集・実績入力で共通して使えます。
        </p>
        {elements.length === 0 ? (
          <p className="muted small">費目がありません。下のフォームから追加してください。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>費目コード</th>
                <th>費目名</th>
              </tr>
            </thead>
            <tbody>
              {elements.map((el) => (
                <tr key={el.code}>
                  <td>{el.code}</td>
                  <td>{el.name}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        <h3>費目を追加</h3>
        <form onSubmit={onSubmit} className="form-row">
          <label>
            費目コード
            <input value={code} onChange={(e) => setCode(e.target.value)} required placeholder="TRAVEL" />
          </label>
          <label>
            費目名
            <input value={name} onChange={(e) => setName(e.target.value)} required placeholder="旅費交通費" />
          </label>
          <button type="submit" className="primary" disabled={saving}>
            追加
          </button>
        </form>
      </div>
    </>
  )
}
