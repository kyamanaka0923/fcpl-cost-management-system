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

  // 案件グリッドのセル編集中の値(キー: `${category}-${projectId}`)
  const [edits, setEdits] = useState<Record<string, string>>({})

  // 案件追加フォーム
  const [newCode, setNewCode] = useState('')
  const [newName, setNewName] = useState('')
  const [addingProject, setAddingProject] = useState(false)

  // 期間費用の追加フォーム
  const [elementCode, setElementCode] = useState('')
  const [periodAmount, setPeriodAmount] = useState('')
  const [savingPeriod, setSavingPeriod] = useState(false)

  const loadBudget = useCallback(() => {
    if (!budgetId) return
    api.getBudget(budgetId).then(setBudget).catch((e: Error) => setError(e.message))
  }, [budgetId])
  const loadProjects = useCallback(() => {
    if (!departmentId) return
    api.listProjects(departmentId).then(setProjects).catch((e: Error) => setError(e.message))
  }, [departmentId])

  useEffect(loadBudget, [loadBudget])
  useEffect(loadProjects, [loadProjects])
  useEffect(() => {
    api.listCostElements().then(setElements).catch((e: Error) => setError(e.message))
  }, [])

  if (!departmentId || !budgetId) return null
  const editable = budget?.status === 'Draft'
  const elementName = (code: string | null) =>
    elements.find((e) => e.code === code)?.name ?? code ?? ''

  // ある案件・区分の現在の予算額(明細がなければ 0)
  const lineAmount = (projectId: string, category: BudgetCategory): number =>
    budget?.lines.find((l) => l.category === category && l.projectId === projectId)?.amount ?? 0

  const cellKey = (projectId: string, category: BudgetCategory) => `${category}-${projectId}`
  const cellValue = (projectId: string, category: BudgetCategory): string => {
    const key = cellKey(projectId, category)
    if (key in edits) return edits[key]
    const amount = lineAmount(projectId, category)
    return amount === 0 ? '' : String(amount)
  }

  const onCellChange = (projectId: string, category: BudgetCategory, value: string) =>
    setEdits((prev) => ({ ...prev, [cellKey(projectId, category)]: value }))

  const clearEdit = (key: string) =>
    setEdits((prev) => {
      const next = { ...prev }
      delete next[key]
      return next
    })

  // セルからフォーカスが外れたら、変更があった区分だけ upsert / 削除する
  const onCellBlur = async (projectId: string, category: BudgetCategory) => {
    const key = cellKey(projectId, category)
    if (!(key in edits)) return
    const raw = edits[key].trim()
    const num = raw === '' ? 0 : Number(raw)
    const current = lineAmount(projectId, category)
    if (Number.isNaN(num) || num < 0 || num === current) {
      clearEdit(key)
      return
    }
    setError(null)
    try {
      const updated =
        num > 0
          ? await api.upsertBudgetLine(budgetId, { category, projectId, amount: num })
          : await api.removeBudgetLine(budgetId, category, projectId, null)
      setBudget(updated)
      clearEdit(key)
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const addProject = async (e: FormEvent) => {
    e.preventDefault()
    setAddingProject(true)
    setError(null)
    try {
      await api.createProject(departmentId, { code: newCode, name: newName })
      setNewCode('')
      setNewName('')
      loadProjects()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setAddingProject(false)
    }
  }

  const completeProject = async (projectId: string) => {
    setError(null)
    try {
      await api.completeProject(projectId)
      loadProjects()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const upsertPeriodCost = async (e: FormEvent) => {
    e.preventDefault()
    setSavingPeriod(true)
    setError(null)
    try {
      const updated = await api.upsertBudgetLine(budgetId, {
        category: 'PeriodCost',
        elementCode,
        amount: Number(periodAmount),
      })
      setBudget(updated)
      setElementCode('')
      setPeriodAmount('')
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSavingPeriod(false)
    }
  }

  const removePeriodCost = async (code: string) => {
    setError(null)
    try {
      setBudget(await api.removeBudgetLine(budgetId, 'PeriodCost', null, code))
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

  const periodCostLines = budget?.lines.filter((l) => l.category === 'PeriodCost') ?? []
  const projectProfit = (projectId: string): number =>
    lineAmount(projectId, 'Revenue') -
    lineAmount(projectId, 'Processing') -
    lineAmount(projectId, 'Outsourcing')

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
          <div className="stat-tile">
            <div className="label">{categoryLabel.Revenue}</div>
            <div className="value">¥{formatYen(budget?.revenueTotal ?? 0)}</div>
          </div>
          <div className="stat-tile">
            <div className="label">{categoryLabel.Processing}</div>
            <div className="value">¥{formatYen(budget?.processingTotal ?? 0)}</div>
          </div>
          <div className="stat-tile">
            <div className="label">{categoryLabel.Outsourcing}</div>
            <div className="value">¥{formatYen(budget?.outsourcingTotal ?? 0)}</div>
          </div>
          <div className="stat-tile">
            <div className="label">{categoryLabel.PeriodCost}</div>
            <div className="value">¥{formatYen(budget?.periodCostTotal ?? 0)}</div>
          </div>
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

      <div className="card">
        <h2>案件別の売上高・加工費・外注費</h2>
        <p className="muted small">
          案件ごとに半期一括の金額を入力します(空欄・0 は明細なし)。
          {editable && '金額を入力して次の欄へ移ると自動保存されます。'}
          課の区分合計は案件明細の合計として上部サマリに反映されます。
        </p>
        <table>
          <thead>
            <tr>
              <th>案件</th>
              <th className="num">{categoryLabel.Revenue}</th>
              <th className="num">{categoryLabel.Processing}</th>
              <th className="num">{categoryLabel.Outsourcing}</th>
              <th className="num">損益</th>
              {editable && <th></th>}
            </tr>
          </thead>
          <tbody>
            {projects.length === 0 ? (
              <tr>
                <td colSpan={editable ? 6 : 5} className="muted small">
                  案件がありません。{editable ? '下の行から案件を追加してください。' : ''}
                </td>
              </tr>
            ) : (
              projects.map((p) => (
                <tr key={p.id} className={p.status === 'Completed' ? 'muted' : ''}>
                  <td>
                    {p.name}
                    <span className="muted small">({p.code})</span>
                    {p.status === 'Completed' && (
                      <span className="badge superseded" style={{ marginLeft: 6 }}>
                        終了
                      </span>
                    )}
                  </td>
                  {projectCategories.map((category) => (
                    <td className="num" key={category}>
                      {editable ? (
                        <input
                          type="number"
                          min={0}
                          step="any"
                          className="num"
                          style={{ width: '9rem' }}
                          aria-label={`${p.name} ${categoryLabel[category]}`}
                          value={cellValue(p.id, category)}
                          onChange={(e) => onCellChange(p.id, category, e.target.value)}
                          onBlur={() => onCellBlur(p.id, category)}
                        />
                      ) : (
                        <>¥{formatYen(lineAmount(p.id, category))}</>
                      )}
                    </td>
                  ))}
                  <td className={`num ${projectProfit(p.id) >= 0 ? 'favorable' : 'adverse'}`}>
                    ¥{formatYen(projectProfit(p.id))}
                  </td>
                  {editable && (
                    <td>
                      {p.status === 'Active' && (
                        <button onClick={() => completeProject(p.id)}>終了</button>
                      )}
                    </td>
                  )}
                </tr>
              ))
            )}
            <tr className="total-row">
              <td>合計</td>
              <td className="num">¥{formatYen(budget?.revenueTotal ?? 0)}</td>
              <td className="num">¥{formatYen(budget?.processingTotal ?? 0)}</td>
              <td className="num">¥{formatYen(budget?.outsourcingTotal ?? 0)}</td>
              <td className="num">
                ¥{formatYen(
                  (budget?.revenueTotal ?? 0) -
                    (budget?.processingTotal ?? 0) -
                    (budget?.outsourcingTotal ?? 0),
                )}
              </td>
              {editable && <td></td>}
            </tr>
          </tbody>
        </table>

        {editable && (
          <>
            <h3>案件を追加</h3>
            <form onSubmit={addProject} className="form-row">
              <label>
                案件コード
                <input
                  value={newCode}
                  onChange={(e) => setNewCode(e.target.value)}
                  required
                  placeholder="PJ-001"
                />
              </label>
              <label>
                案件名
                <input
                  value={newName}
                  onChange={(e) => setNewName(e.target.value)}
                  required
                  placeholder="受託開発A"
                />
              </label>
              <button type="submit" className="primary" disabled={addingProject}>
                追加
              </button>
            </form>
          </>
        )}
      </div>

      <div className="card">
        <h2>期間費用</h2>
        <p className="muted small">
          課共通の費用(人件費・ライセンス費など)を費目ごとに計画します。
        </p>
        {periodCostLines.length === 0 ? (
          <p className="muted small">明細がありません。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>費目</th>
                <th className="num">金額</th>
                {editable && <th></th>}
              </tr>
            </thead>
            <tbody>
              {periodCostLines.map((l) => (
                <tr key={l.id}>
                  <td>{elementName(l.elementCode)}</td>
                  <td className="num">¥{formatYen(l.amount)}</td>
                  {editable && (
                    <td>
                      <button onClick={() => removePeriodCost(l.elementCode!)}>削除</button>
                    </td>
                  )}
                </tr>
              ))}
              <tr className="total-row">
                <td>合計</td>
                <td className="num">¥{formatYen(budget?.periodCostTotal ?? 0)}</td>
                {editable && <td></td>}
              </tr>
            </tbody>
          </table>
        )}

        {editable && (
          <>
            <h3>期間費用の追加・更新</h3>
            <p className="muted small">同じ費目で再登録すると上書きされます。</p>
            <form onSubmit={upsertPeriodCost} className="form-row">
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
                金額(円・半期一括)
                <input
                  type="number"
                  value={periodAmount}
                  onChange={(e) => setPeriodAmount(e.target.value)}
                  min={0}
                  step="any"
                  required
                />
              </label>
              <button type="submit" className="primary" disabled={savingPeriod}>
                登録
              </button>
            </form>
          </>
        )}
      </div>
    </>
  )
}
