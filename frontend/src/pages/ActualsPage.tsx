import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import {
  api,
  categoryLabel,
  currentFiscalHalf,
  formatYen,
  halfLabel,
  halfMonths,
  type ActualEntry,
  type BudgetCategory,
  type CostElement,
  type Project,
} from '../api'
import MoneyInput from '../components/MoneyInput'
import Toast from '../components/Toast'

export default function ActualsPage() {
  const { departmentId } = useParams<{ departmentId: string }>()
  const [searchParams] = useSearchParams()
  const half = searchParams.get('half') ?? currentFiscalHalf()

  const [entries, setEntries] = useState<ActualEntry[]>([])
  const [projects, setProjects] = useState<Project[]>([])
  const [elements, setElements] = useState<CostElement[]>([])
  const [error, setError] = useState<string | null>(null)

  const [category, setCategory] = useState<BudgetCategory>('Revenue')
  const [projectId, setProjectId] = useState('')
  const [elementCode, setElementCode] = useState('')
  const [periodDetail, setPeriodDetail] = useState('') // 期間費用の明細名(任意。空 = 費目一括)
  const [month, setMonth] = useState('') // '' = 半期一括
  const [amount, setAmount] = useState('')
  const [note, setNote] = useState('')
  const [saving, setSaving] = useState(false)
  // 計画済みの期間費用明細名(費目コード → 明細名の一覧)。実績入力の候補に使う。
  const [plannedDetails, setPlannedDetails] = useState<Record<string, string[]>>({})

  const months = halfMonths(half)
  const monthLabel = (m: number | null) =>
    m === null ? '半期一括' : (months.find((x) => x.index === m)?.label ?? `${m}月`)

  const load = useCallback(() => {
    if (!departmentId) return
    api.listActuals(departmentId, half).then(setEntries).catch((e: Error) => setError(e.message))
  }, [departmentId, half])
  useEffect(load, [load])
  useEffect(() => {
    if (!departmentId) return
    api.listProjects(departmentId).then(setProjects).catch((e: Error) => setError(e.message))
    api.listCostElements().then(setElements).catch((e: Error) => setError(e.message))
  }, [departmentId])
  // 計画済みの期間費用明細名を集める(承認済みがあれば優先、なければ最新版)。実績入力時の候補にする。
  useEffect(() => {
    if (!departmentId) return
    api
      .listBudgets(departmentId, half)
      .then(async (list) => {
        if (list.length === 0) {
          setPlannedDetails({})
          return
        }
        const chosen = list.find((b) => b.status === 'Approved') ?? list[0]
        const detail = await api.getBudget(chosen.id)
        const map: Record<string, string[]> = {}
        for (const l of detail.lines) {
          if (l.category === 'PeriodCost' && l.periodDetail && l.elementCode) {
            ;(map[l.elementCode] ??= []).push(l.periodDetail)
          }
        }
        setPlannedDetails(map)
      })
      .catch(() => setPlannedDetails({}))
  }, [departmentId, half])

  if (!departmentId) return null
  const isProjectCategory = category !== 'PeriodCost'
  const projectName = (id: string | null) => projects.find((p) => p.id === id)?.name ?? id ?? ''
  const elementName = (code: string | null) =>
    elements.find((e) => e.code === code)?.name ?? code ?? ''

  const record = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await api.recordActual(departmentId, {
        fiscalHalf: half,
        category,
        projectId: isProjectCategory ? projectId : null,
        elementCode: isProjectCategory ? null : elementCode,
        periodDetail: isProjectCategory ? null : periodDetail || null,
        month: month ? Number(month) : null,
        amount: Number(amount),
        note: note || null,
      })
      setAmount('')
      setNote('')
      load()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const remove = async (actualId: string) => {
    setError(null)
    try {
      await api.deleteActual(actualId)
      load()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const total = entries.reduce((sum, e) => sum + e.amount, 0)

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">課一覧</Link> /{' '}
        <Link to={`/departments/${departmentId}?half=${half}`}>課詳細</Link> / 実績入力
      </div>
      <Toast message={error} onClose={() => setError(null)} />

      <div className="card">
        <h2>実績の計上({halfLabel(half)})</h2>
        <p className="muted small">
          売上高・加工費・外注費は案件ごとに、期間費用は費目ごとに計上します。
          「月」を選ぶと特定月の計上、「半期一括」なら半期まとめての計上になります。
          同じ案件(または費目)に複数回計上でき、分析時に合算されます。
        </p>
        <form onSubmit={record} className="form-row">
          <label>
            区分
            <select value={category} onChange={(e) => setCategory(e.target.value as BudgetCategory)}>
              {(Object.keys(categoryLabel) as BudgetCategory[]).map((c) => (
                <option key={c} value={c}>
                  {categoryLabel[c]}
                </option>
              ))}
            </select>
          </label>
          {isProjectCategory ? (
            <label>
              案件
              <select value={projectId} onChange={(e) => setProjectId(e.target.value)} required>
                <option value="">選択してください</option>
                {projects.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}({p.code})
                  </option>
                ))}
              </select>
            </label>
          ) : (
            <>
              <label>
                費目
                <select value={elementCode} onChange={(e) => setElementCode(e.target.value)} required>
                  <option value="">選択してください</option>
                  {elements.map((el) => (
                    <option key={el.code} value={el.code}>
                      {el.name}({el.code})
                    </option>
                  ))}
                </select>
              </label>
              <label>
                明細(任意)
                <input
                  aria-label="明細名"
                  list={`planned-details-${elementCode}`}
                  value={periodDetail}
                  onChange={(e) => setPeriodDetail(e.target.value)}
                  placeholder="費目一括はそのまま"
                />
                <datalist id={`planned-details-${elementCode}`}>
                  {(plannedDetails[elementCode] ?? []).map((d) => (
                    <option key={d} value={d} />
                  ))}
                </datalist>
              </label>
            </>
          )}
          <label>
            月
            <select value={month} onChange={(e) => setMonth(e.target.value)}>
              <option value="">半期一括</option>
              {months.map((m) => (
                <option key={m.index} value={m.index}>
                  {m.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            金額(円)
            <MoneyInput value={amount} onChange={setAmount} required />
          </label>
          <label>
            備考
            <input value={note} onChange={(e) => setNote(e.target.value)} placeholder="4月分 など" />
          </label>
          <button type="submit" className="primary" disabled={saving}>
            計上
          </button>
        </form>
      </div>

      <div className="card">
        <h2>実績一覧</h2>
        {entries.length === 0 ? (
          <p className="muted small">実績がありません。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>計上日時</th>
                <th>区分</th>
                <th>案件 / 費目</th>
                <th>月</th>
                <th className="num">金額</th>
                <th>備考</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {entries.map((e) => (
                <tr key={e.id}>
                  <td className="small muted">{new Date(e.recordedAt).toLocaleString('ja-JP')}</td>
                  <td>{categoryLabel[e.category]}</td>
                  <td>
                    {e.category === 'PeriodCost'
                      ? elementName(e.elementCode) + (e.periodDetail ? ` / ${e.periodDetail}` : '')
                      : projectName(e.projectId)}
                  </td>
                  <td className="small muted">{monthLabel(e.month)}</td>
                  <td className="num">¥{formatYen(e.amount)}</td>
                  <td className="small muted">{e.note ?? ''}</td>
                  <td>
                    <button onClick={() => remove(e.id)}>削除</button>
                  </td>
                </tr>
              ))}
              <tr className="total-row">
                <td colSpan={4}>合計(全区分)</td>
                <td className="num">¥{formatYen(total)}</td>
                <td colSpan={2}></td>
              </tr>
            </tbody>
          </table>
        )}
      </div>
    </>
  )
}
