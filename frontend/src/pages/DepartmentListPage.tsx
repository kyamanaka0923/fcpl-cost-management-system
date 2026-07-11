import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, type Department } from '../api'

export default function DepartmentListPage() {
  const [departments, setDepartments] = useState<Department[]>([])
  const [error, setError] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [saving, setSaving] = useState(false)

  const load = () => {
    api.listDepartments().then(setDepartments).catch((e: Error) => setError(e.message))
  }
  useEffect(load, [])

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await api.createDepartment({ code, name })
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
      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>課の新規登録</h2>
        <form onSubmit={onSubmit} className="form-row">
          <label>
            課コード
            <input value={code} onChange={(e) => setCode(e.target.value)} required placeholder="DEV-1" />
          </label>
          <label>
            課名
            <input value={name} onChange={(e) => setName(e.target.value)} required placeholder="開発1課" />
          </label>
          <button type="submit" className="primary" disabled={saving}>
            登録
          </button>
        </form>
      </div>

      <div className="card">
        <h2>課一覧</h2>
        {departments.length === 0 ? (
          <p className="muted small">課がありません。上のフォームから登録してください。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>課コード</th>
                <th>課名</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {departments.map((d) => (
                <tr key={d.id}>
                  <td>{d.code}</td>
                  <td>
                    <Link to={`/departments/${d.id}`}>{d.name}</Link>
                  </td>
                  <td className="small">
                    <Link to={`/departments/${d.id}/variance`}>差異分析</Link>
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
