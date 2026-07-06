import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, formatYen, type CostPlanSummary, type Project } from '../api'

const statusLabel: Record<CostPlanSummary['status'], string> = {
  Draft: '策定中',
  Approved: '承認済',
  Superseded: '失効',
}

export default function ProjectDetailPage() {
  const { projectId } = useParams<{ projectId: string }>()
  const navigate = useNavigate()
  const [project, setProject] = useState<Project | null>(null)
  const [plans, setPlans] = useState<CostPlanSummary[]>([])
  const [error, setError] = useState<string | null>(null)
  const [label, setLabel] = useState('')
  const [basePlanId, setBasePlanId] = useState('')
  const [saving, setSaving] = useState(false)

  const load = useCallback(() => {
    if (!projectId) return
    api.getProject(projectId).then(setProject).catch((e: Error) => setError(e.message))
    api.listPlans(projectId).then(setPlans).catch((e: Error) => setError(e.message))
  }, [projectId])
  useEffect(load, [load])

  if (!projectId) return null

  const hasDraft = plans.some((p) => p.status === 'Draft')
  const nonDrafts = plans.filter((p) => p.status !== 'Draft')

  const createPlan = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      const plan = await api.createPlan(projectId, {
        label,
        basePlanId: basePlanId || null,
      })
      navigate(`/projects/${projectId}/plans/${plan.id}`)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const approve = async (planId: string) => {
    setError(null)
    try {
      await api.approvePlan(planId)
      load()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">プロジェクト一覧</Link> / {project?.name ?? '…'}
      </div>
      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>
          {project?.code} {project?.name}
          <span className="muted small" style={{ marginLeft: 12 }}>
            {project?.fiscalYear}年度
          </span>
        </h2>
        <div className="form-row">
          <Link to={`/projects/${projectId}/actuals`}>
            <button>実績入力</button>
          </Link>
          <Link to={`/projects/${projectId}/variance`}>
            <button>予実差異分析</button>
          </Link>
          <Link to={`/projects/${projectId}/comparison`}>
            <button>予算バージョン比較</button>
          </Link>
        </div>
      </div>

      <div className="card">
        <h2>予算バージョン</h2>
        <p className="muted small">
          予算は四半期などの節目で何度でも改定できます。改定版は既存バージョンの明細を引き継いだ
          ドラフトとして作成され、承認すると旧バージョンは失効(履歴として保持)します。
        </p>
        {plans.length === 0 ? (
          <p className="muted small">予算がまだありません。下のフォームから当初予算を作成してください。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th className="num">Ver</th>
                <th>名称</th>
                <th>状態</th>
                <th className="num">総額</th>
                <th>承認日時</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {plans.map((p) => (
                <tr key={p.id}>
                  <td className="num">v{p.version}</td>
                  <td>
                    <Link to={`/projects/${projectId}/plans/${p.id}`}>{p.label}</Link>
                  </td>
                  <td>
                    <span className={`badge ${p.status.toLowerCase()}`}>{statusLabel[p.status]}</span>
                  </td>
                  <td className="num">¥{formatYen(p.totalAmount)}</td>
                  <td className="small muted">
                    {p.approvedAt ? new Date(p.approvedAt).toLocaleString('ja-JP') : '—'}
                  </td>
                  <td>
                    {p.status === 'Draft' && (
                      <button onClick={() => approve(p.id)} className="primary">
                        承認
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        <h3>{plans.length === 0 ? '当初予算の作成' : '改定版の作成'}</h3>
        <form onSubmit={createPlan} className="form-row">
          <label>
            予算名
            <input
              value={label}
              onChange={(e) => setLabel(e.target.value)}
              required
              placeholder={plans.length === 0 ? '当初予算' : '第2四半期改定'}
            />
          </label>
          {nonDrafts.length > 0 && (
            <label>
              引継ぎ元バージョン
              <select value={basePlanId} onChange={(e) => setBasePlanId(e.target.value)}>
                <option value="">最新承認版(既定)</option>
                {nonDrafts.map((p) => (
                  <option key={p.id} value={p.id}>
                    v{p.version} {p.label}
                  </option>
                ))}
              </select>
            </label>
          )}
          <button type="submit" className="primary" disabled={saving || hasDraft}>
            ドラフト作成
          </button>
          {hasDraft && <span className="muted small">策定中のドラフトを先に承認してください。</span>}
        </form>
      </div>
    </>
  )
}
