import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import {
  api,
  categoryLabel,
  currentFiscalHalf,
  formatYen,
  halfLabel,
  projectCategories,
  type BudgetCategory,
  type BudgetDetail,
  type CostElement,
  type Project,
} from '../api'

export default function BudgetEditPage() {
  const { departmentId, budgetId } = useParams<{ departmentId: string; budgetId: string }>()
  const [searchParams] = useSearchParams()
  const half = searchParams.get('half') ?? currentFiscalHalf()

  const [budget, setBudget] = useState<BudgetDetail | null>(null)
  const [projects, setProjects] = useState<Project[]>([])
  const [elements, setElements] = useState<CostElement[]>([])
  const [error, setError] = useState<string | null>(null)

  const [category, setCategory] = useState<BudgetCategory>('Revenue')
  const [projectId, setProjectId] = useState('')
  const [elementCode, setElementCode] = useState('')
  const [amount, setAmount] = useState('')
  const [saving, setSaving] = useState(false)

  const load = useCallback(() => {
    if (!budgetId) return
    api.getBudget(budgetId).then(setBudget).catch((e: Error) => setError(e.message))
  }, [budgetId])
  useEffect(load, [load])
  useEffect(() => {
    if (!departmentId) return
    api.listProjects(departmentId).then(setProjects).catch((e: Error) => setError(e.message))
    api.listCostElements().then(setElements).catch((e: Error) => setError(e.message))
  }, [departmentId])

  if (!departmentId || !budgetId) return null
  const editable = budget?.status === 'Draft'
  const isProjectCategory = category !== 'PeriodCost'
  const projectName = (id: string | null) =>
    projects.find((p) => p.id === id)?.name ?? id ?? ''
  const elementName = (code: string | null) =>
    elements.find((e) => e.code === code)?.name ?? code ?? ''

  const upsert = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      const updated = await api.upsertBudgetLine(budgetId, {
        category,
        projectId: isProjectCategory ? projectId : null,
        elementCode: isProjectCategory ? null : elementCode,
        amount: Number(amount),
      })
      setBudget(updated)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const remove = async (c: BudgetCategory, pid: string | null, code: string | null) => {
    setError(null)
    try {
      setBudget(await api.removeBudgetLine(budgetId, c, pid, code))
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const approve = async () => {
    setError(null)
    try {
      setBudget(await api.approveBudget(budgetId))
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const categoryTotals: { category: BudgetCategory; total: number }[] = budget
    ? [
        { category: 'Revenue', total: budget.revenueTotal },
        { category: 'Processing', total: budget.processingTotal },
        { category: 'Outsourcing', total: budget.outsourcingTotal },
        { category: 'PeriodCost', total: budget.periodCostTotal },
      ]
    : []

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">課一覧</Link> /{' '}
        <Link to={`/departments/${departmentId}?half=${half}`}>課詳細</Link> / 予算編集
      </div>
      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>
          v{budget?.version} {budget?.label}
          <span className="muted small" style={{ marginLeft: 12 }}>
            {budget ? halfLabel(budget.fiscalHalf) : ''}
          </span>
          <span className={`badge ${budget?.status.toLowerCase()}`} style={{ marginLeft: 12 }}>
            {budget?.status === 'Draft' ? '策定中' : budget?.status === 'Approved' ? '承認済' : '失効'}
          </span>
        </h2>
        <div className="stat-row">
          {categoryTotals.map(({ category: c, total }) => (
            <div className="stat-tile" key={c}>
              <div className="label">{categoryLabel[c]}</div>
              <div className="value">¥{formatYen(total)}</div>
            </div>
          ))}
          <div className="stat-tile">
            <div className="label">計画損益</div>
            <div className={`value ${(budget?.plannedProfit ?? 0) >= 0 ? 'favorable' : 'adverse'}`}>
              ¥{formatYen(budget?.plannedProfit ?? 0)}
            </div>
          </div>
        </div>
        {editable && (
          <button className="primary" onClick={approve} disabled={(budget?.lines.length ?? 0) === 0}>
            この予算を承認する
          </button>
        )}
        {!editable && (
          <p className="muted small">
            このバージョンは編集できません。変更するには改定版を作成してください。
          </p>
        )}
      </div>

      {editable && (
        <div className="card">
          <h2>明細の追加・更新</h2>
          <p className="muted small">
            売上高・加工費・外注費は案件ごとに、期間費用は費目ごとに半期一括の金額で計画します。
            同じ区分・案件(または費目)の明細は上書きされます。
            課の区分合計は明細の合計として自動的に算出されます。
          </p>
          <form onSubmit={upsert} className="form-row">
            <label>
              区分
              <select
                value={category}
                onChange={(e) => setCategory(e.target.value as BudgetCategory)}
              >
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
            )}
            <label>
              金額(円・半期一括)
              <input
                type="number"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                min={0}
                step="any"
                required
              />
            </label>
            <button type="submit" className="primary" disabled={saving}>
              登録
            </button>
          </form>
          {projects.length === 0 && (
            <p className="muted small">
              案件がまだ登録されていません。課詳細ページの「案件マスタ」から登録してください。
            </p>
          )}
        </div>
      )}

      <div className="card">
        <h2>予算明細</h2>
        {!budget || budget.lines.length === 0 ? (
          <p className="muted small">明細がありません。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>区分</th>
                <th>案件 / 費目</th>
                <th className="num">金額</th>
                {editable && <th></th>}
              </tr>
            </thead>
            <tbody>
              {projectCategories.concat('PeriodCost').flatMap((c) =>
                budget.lines
                  .filter((l) => l.category === c)
                  .map((l) => (
                    <tr key={l.id}>
                      <td>{categoryLabel[l.category]}</td>
                      <td>
                        {l.category === 'PeriodCost'
                          ? elementName(l.elementCode)
                          : projectName(l.projectId)}
                      </td>
                      <td className="num">¥{formatYen(l.amount)}</td>
                      {editable && (
                        <td>
                          <button onClick={() => remove(l.category, l.projectId, l.elementCode)}>
                            削除
                          </button>
                        </td>
                      )}
                    </tr>
                  )),
              )}
              <tr className="total-row">
                <td colSpan={2}>売上高合計</td>
                <td className="num">¥{formatYen(budget.revenueTotal)}</td>
                {editable && <td></td>}
              </tr>
              <tr className="total-row">
                <td colSpan={2}>総コスト(加工費 + 外注費 + 期間費用)</td>
                <td className="num">
                  ¥{formatYen(budget.processingTotal + budget.outsourcingTotal + budget.periodCostTotal)}
                </td>
                {editable && <td></td>}
              </tr>
            </tbody>
          </table>
        )}
      </div>
    </>
  )
}
