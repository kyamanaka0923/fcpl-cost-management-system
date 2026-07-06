import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, type Project } from '../api'

export default function ProjectListPage() {
  const [projects, setProjects] = useState<Project[]>([])
  const [error, setError] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [fiscalYear, setFiscalYear] = useState(new Date().getFullYear())
  const [saving, setSaving] = useState(false)

  const load = () => {
    api.listProjects().then(setProjects).catch((e: Error) => setError(e.message))
  }
  useEffect(load, [])

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await api.createProject({ code, name, fiscalYear })
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
        <h2>プロジェクト新規作成</h2>
        <form onSubmit={onSubmit} className="form-row">
          <label>
            コード
            <input value={code} onChange={(e) => setCode(e.target.value)} required placeholder="PJ-001" />
          </label>
          <label>
            名称
            <input value={name} onChange={(e) => setName(e.target.value)} required placeholder="新製品ライン構築" />
          </label>
          <label>
            会計年度
            <input
              type="number"
              value={fiscalYear}
              onChange={(e) => setFiscalYear(Number(e.target.value))}
              min={2000}
              max={2100}
              required
            />
          </label>
          <button type="submit" className="primary" disabled={saving}>
            作成
          </button>
        </form>
      </div>

      <div className="card">
        <h2>プロジェクト一覧</h2>
        {projects.length === 0 ? (
          <p className="muted small">プロジェクトがありません。上のフォームから作成してください。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>コード</th>
                <th>名称</th>
                <th className="num">会計年度</th>
                <th>状態</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {projects.map((p) => (
                <tr key={p.id}>
                  <td>{p.code}</td>
                  <td>
                    <Link to={`/projects/${p.id}`}>{p.name}</Link>
                  </td>
                  <td className="num">{p.fiscalYear}</td>
                  <td>
                    <span className={`badge ${p.status === 'Active' ? 'approved' : 'superseded'}`}>
                      {p.status === 'Active' ? '進行中' : '完了'}
                    </span>
                  </td>
                  <td className="small">
                    <Link to={`/projects/${p.id}/variance`}>差異分析</Link>
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
