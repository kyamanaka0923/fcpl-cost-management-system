import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import {
  api,
  currentFiscalHalf,
  fiscalHalfOptions,
  formatYen,
  halfLabel,
  type BudgetStatus,
  type BudgetSummary,
  type Department,
  type Division,
} from '../api'

const statusLabel: Record<BudgetStatus, string> = {
  Draft: '策定中',
  Approved: '承認済',
  Superseded: '失効',
}

function HalfSelector({ half, setHalf }: { half: string; setHalf: (v: string) => void }) {
  return (
    <label>
      対象半期
      <select value={half} onChange={(e) => setHalf(e.target.value)}>
        {fiscalHalfOptions().map((h) => (
          <option key={h} value={h}>
            {halfLabel(h)}
          </option>
        ))}
      </select>
    </label>
  )
}

function BudgetVersionsCard({
  departmentId,
  half,
  budgets,
  onChanged,
  onError,
}: {
  departmentId: string
  half: string
  budgets: BudgetSummary[]
  onChanged: () => void
  onError: (message: string | null) => void
}) {
  const navigate = useNavigate()
  const [label, setLabel] = useState('')
  const [baseBudgetId, setBaseBudgetId] = useState('')
  const [saving, setSaving] = useState(false)

  const hasDraft = budgets.some((b) => b.status === 'Draft')
  const nonDrafts = budgets.filter((b) => b.status !== 'Draft')

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    onError(null)
    try {
      const budget = await api.createBudget(departmentId, {
        fiscalHalf: half,
        label,
        baseBudgetId: baseBudgetId || null,
      })
      setLabel('')
      navigate(`/departments/${departmentId}/budgets/${budget.id}?half=${half}`)
    } catch (err) {
      onError((err as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const approve = async (budgetId: string) => {
    onError(null)
    try {
      await api.approveBudget(budgetId)
      onChanged()
    } catch (err) {
      onError((err as Error).message)
    }
  }

  return (
    <div className="card">
      <h2>予算バージョン({halfLabel(half)})</h2>
      <p className="muted small">
        予算は課 × 半期ごとにバージョン管理され、半期の途中でも改定できます。
        改定版は既存バージョンの明細を引き継いだドラフトとして作成され、
        承認すると旧バージョンは失効(履歴として保持)します。
      </p>
      {budgets.length === 0 ? (
        <p className="muted small">予算がまだありません。下のフォームから当初予算を作成してください。</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th className="num">Ver</th>
              <th>名称</th>
              <th>状態</th>
              <th className="num">売上高</th>
              <th className="num">総コスト</th>
              <th className="num">損益</th>
              <th>承認日時</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {budgets.map((b) => (
              <tr key={b.id}>
                <td className="num">v{b.version}</td>
                <td>
                  <Link to={`/departments/${departmentId}/budgets/${b.id}?half=${half}`}>
                    {b.label}
                  </Link>
                </td>
                <td>
                  <span className={`badge ${b.status.toLowerCase()}`}>{statusLabel[b.status]}</span>
                </td>
                <td className="num">¥{formatYen(b.revenueTotal)}</td>
                <td className="num">
                  ¥{formatYen(b.processingTotal + b.outsourcingTotal + b.periodCostTotal)}
                </td>
                <td className="num">¥{formatYen(b.plannedProfit)}</td>
                <td className="small muted">
                  {b.approvedAt ? new Date(b.approvedAt).toLocaleString('ja-JP') : '—'}
                </td>
                <td>
                  {b.status === 'Draft' && (
                    <button onClick={() => approve(b.id)} className="primary">
                      承認
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <h3>{budgets.length === 0 ? '当初予算の作成' : '改定版の作成'}</h3>
      <form onSubmit={submit} className="form-row">
        <label>
          予算名
          <input
            value={label}
            onChange={(e) => setLabel(e.target.value)}
            required
            placeholder={budgets.length === 0 ? '当初予算' : '上期見直し'}
          />
        </label>
        {nonDrafts.length > 0 && (
          <label>
            引継ぎ元バージョン
            <select value={baseBudgetId} onChange={(e) => setBaseBudgetId(e.target.value)}>
              <option value="">最新承認版(既定)</option>
              {nonDrafts.map((b) => (
                <option key={b.id} value={b.id}>
                  v{b.version} {b.label}
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
  )
}

export default function DepartmentDetailPage() {
  const { departmentId } = useParams<{ departmentId: string }>()
  const [searchParams, setSearchParams] = useSearchParams()
  const half = searchParams.get('half') ?? currentFiscalHalf()
  const setHalf = (value: string) => setSearchParams({ half: value })

  const [department, setDepartment] = useState<Department | null>(null)
  const [division, setDivision] = useState<Division | null>(null)
  const [budgets, setBudgets] = useState<BudgetSummary[]>([])
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    if (!departmentId) return
    api
      .getDepartment(departmentId)
      .then((d) => {
        setDepartment(d)
        api.getDivision(d.divisionId).then(setDivision).catch(() => undefined)
      })
      .catch((e: Error) => setError(e.message))
    api.listBudgets(departmentId, half).then(setBudgets).catch((e: Error) => setError(e.message))
  }, [departmentId, half])
  useEffect(load, [load])

  if (!departmentId) return null

  const approved = budgets.find((b) => b.status === 'Approved')

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">部一覧</Link> /{' '}
        <Link to={`/divisions/${department?.divisionId}?half=${half}`}>{division?.name ?? '…'}</Link>{' '}
        / {department?.name ?? '…'}
      </div>
      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>
          {department?.code} {department?.name}
        </h2>
        <div className="form-row">
          <HalfSelector half={half} setHalf={setHalf} />
          <Link to={`/departments/${departmentId}/actuals?half=${half}`}>
            <button>実績入力</button>
          </Link>
          <Link to={`/departments/${departmentId}/variance?half=${half}`}>
            <button>予実差異分析・損益</button>
          </Link>
          <Link to={`/departments/${departmentId}/comparison?half=${half}`}>
            <button>予算バージョン比較</button>
          </Link>
        </div>
      </div>

      {approved && (
        <div className="stat-row">
          <div className="stat-tile">
            <div className="label">売上高予算</div>
            <div className="value">¥{formatYen(approved.revenueTotal)}</div>
          </div>
          <div className="stat-tile">
            <div className="label">加工費予算</div>
            <div className="value">¥{formatYen(approved.processingTotal)}</div>
          </div>
          <div className="stat-tile">
            <div className="label">外注費予算</div>
            <div className="value">¥{formatYen(approved.outsourcingTotal)}</div>
          </div>
          <div className="stat-tile">
            <div className="label">期間費用予算</div>
            <div className="value">¥{formatYen(approved.periodCostTotal)}</div>
          </div>
          <div className="stat-tile">
            <div className="label">計画損益</div>
            <div className={`value ${approved.plannedProfit >= 0 ? 'favorable' : 'adverse'}`}>
              ¥{formatYen(approved.plannedProfit)}
            </div>
          </div>
        </div>
      )}

      <BudgetVersionsCard
        departmentId={departmentId}
        half={half}
        budgets={budgets}
        onChanged={load}
        onError={setError}
      />
    </>
  )
}
