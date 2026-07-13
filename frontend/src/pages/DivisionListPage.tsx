import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, type Division } from '../api'
import Toast from '../components/Toast'

export default function DivisionListPage() {
  const [divisions, setDivisions] = useState<Division[]>([])
  const [error, setError] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [saving, setSaving] = useState(false)

  const load = () => {
    api.listDivisions().then(setDivisions).catch((e: Error) => setError(e.message))
  }
  useEffect(load, [])

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await api.createDivision({ code, name })
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
      <Toast message={error} onClose={() => setError(null)} />

      <div className="card">
        <h2>部の新規登録</h2>
        <form onSubmit={onSubmit} className="form-row">
          <label>
            部コード
            <input value={code} onChange={(e) => setCode(e.target.value)} required placeholder="SALES" />
          </label>
          <label>
            部名
            <input value={name} onChange={(e) => setName(e.target.value)} required placeholder="営業本部" />
          </label>
          <button type="submit" className="primary" disabled={saving}>
            登録
          </button>
        </form>
      </div>

      <div className="card">
        <h2>部一覧</h2>
        {divisions.length === 0 ? (
          <p className="muted small">部がありません。上のフォームから登録してください。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>部コード</th>
                <th>部名</th>
              </tr>
            </thead>
            <tbody>
              {divisions.map((d) => (
                <tr key={d.id}>
                  <td>{d.code}</td>
                  <td>
                    <Link to={`/divisions/${d.id}`}>{d.name}</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  )
}
