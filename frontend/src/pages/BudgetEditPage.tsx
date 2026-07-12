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
import MoneyInput from '../components/MoneyInput'

export default function BudgetEditPage() {
  const { departmentId, budgetId } = useParams<{ departmentId: string; budgetId: string }>()
  const [searchParams] = useSearchParams()
  const half = searchParams.get('half') ?? currentFiscalHalf()

  const [budget, setBudget] = useState<BudgetDetail | null>(null)
  const [projects, setProjects] = useState<Project[]>([])
  const [elements, setElements] = useState<CostElement[]>([])
  const [error, setError] = useState<string | null>(null)

  // グリッドのセル編集中の値。案件は `${category}-${projectId}`、期間費用は `PeriodCost-${code}`。
  const [edits, setEdits] = useState<Record<string, string>>({})

  // 案件追加フォーム
  const [newCode, setNewCode] = useState('')
  const [newName, setNewName] = useState('')
  const [addingProject, setAddingProject] = useState(false)

  // 費目追加フォーム

  const loadBudget = useCallback(() => {
    if (!budgetId) return
    api.getBudget(budgetId).then(setBudget).catch((e: Error) => setError(e.message))
  }, [budgetId])
  const loadProjects = useCallback(() => {
    if (!departmentId) return
    api.listProjects(departmentId).then(setProjects).catch((e: Error) => setError(e.message))
  }, [departmentId])
  const loadElements = useCallback(() => {
    api.listCostElements().then(setElements).catch((e: Error) => setError(e.message))
  }, [])

  useEffect(loadBudget, [loadBudget])
  useEffect(loadProjects, [loadProjects])
  useEffect(loadElements, [loadElements])

  if (!departmentId || !budgetId) return null
  const editable = budget?.status === 'Draft'
  const elementName = (code: string | null) =>
    elements.find((e) => e.code === code)?.name ?? code ?? ''

  const clearEdit = (key: string) =>
    setEdits((prev) => {
      const next = { ...prev }
      delete next[key]
      return next
    })

  // 編集中セルの値を、変更があったときだけ upsert / 削除する共通処理。
  const saveCell = async (
    key: string,
    current: number,
    upsert: (amount: number) => Promise<BudgetDetail>,
    remove: () => Promise<BudgetDetail>,
  ) => {
    if (!(key in edits)) return
    const raw = edits[key].trim()
    const num = raw === '' ? 0 : Number(raw)
    if (Number.isNaN(num) || num < 0 || num === current) {
      clearEdit(key)
      return
    }
    setError(null)
    try {
      setBudget(await (num > 0 ? upsert(num) : remove()))
      clearEdit(key)
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const cellValue = (key: string, amount: number): string => {
    if (key in edits) return edits[key]
    return amount === 0 ? '' : String(amount)
  }
  const onCellChange = (key: string, value: string) =>
    setEdits((prev) => ({ ...prev, [key]: value }))

  // ---- 案件別(売上高・加工費・外注費) ----
  const projectKey = (projectId: string, category: BudgetCategory) => `${category}-${projectId}`
  const projectAmount = (projectId: string, category: BudgetCategory): number =>
    budget?.lines.find((l) => l.category === category && l.projectId === projectId)?.amount ?? 0
  const onProjectBlur = (projectId: string, category: BudgetCategory) =>
    saveCell(
      projectKey(projectId, category),
      projectAmount(projectId, category),
      (amount) => api.upsertBudgetLine(budgetId, { category, projectId, amount }),
      () => api.removeBudgetLine(budgetId, category, projectId, null),
    )
  const projectProfit = (projectId: string): number =>
    projectAmount(projectId, 'Revenue') -
    projectAmount(projectId, 'Processing') -
    projectAmount(projectId, 'Outsourcing')

  // ---- 期間費用(費目別) ----
  const periodKey = (code: string) => `PeriodCost-${code}`
  const periodAmount = (code: string): number =>
    budget?.lines.find((l) => l.category === 'PeriodCost' && l.elementCode === code)?.amount ?? 0
  const onPeriodBlur = (code: string) =>
    saveCell(
      periodKey(code),
      periodAmount(code),
      (amount) => api.upsertBudgetLine(budgetId, { category: 'PeriodCost', elementCode: code, amount }),
      () => api.removeBudgetLine(budgetId, 'PeriodCost', null, code),
    )

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

  const approve = async () => {
    setError(null)
    try {
      setBudget(await api.approveBudget(budgetId))
    } catch (err) {
      setError((err as Error).message)
    }
  }

  // 期間費用の行: 編集中は全費目、閲覧時は明細のある費目のみ。
  const periodCostLines = budget?.lines.filter((l) => l.category === 'PeriodCost') ?? []
  const periodRows = editable
    ? elements.map((el) => ({ code: el.code, name: el.name }))
    : periodCostLines.map((l) => ({ code: l.elementCode!, name: elementName(l.elementCode) }))

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
          {editable && '金額を入力して次の欄へ移ると自動保存されます。終了にした案件も予算を承認するまでは編集できます。'}
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
                <tr key={p.id}>
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
                        <MoneyInput
                          className="num"
                          style={{ width: '9rem' }}
                          aria-label={`${p.name} ${categoryLabel[category]}`}
                          value={cellValue(projectKey(p.id, category), projectAmount(p.id, category))}
                          onChange={(v) => onCellChange(projectKey(p.id, category), v)}
                          onBlur={() => onProjectBlur(p.id, category)}
                        />
                      ) : (
                        <>¥{formatYen(projectAmount(p.id, category))}</>
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
          課共通の費用(人件費・ライセンス費など)を費目ごとに入力します(空欄・0 は明細なし)。
          {editable && '金額を入力して次の欄へ移ると自動保存されます。'}
          費目はシステム共通のマスタで、「費目マスタ」画面で追加します。
        </p>
        <table>
          <thead>
            <tr>
              <th>費目</th>
              <th className="num">金額</th>
            </tr>
          </thead>
          <tbody>
            {periodRows.length === 0 ? (
              <tr>
                <td colSpan={2} className="muted small">
                  {editable
                    ? '費目がありません。「費目マスタ」画面で費目を追加してください。'
                    : '明細がありません。'}
                </td>
              </tr>
            ) : (
              periodRows.map((el) => (
                <tr key={el.code}>
                  <td>
                    {el.name}
                    <span className="muted small">({el.code})</span>
                  </td>
                  <td className="num">
                    {editable ? (
                      <MoneyInput
                        className="num"
                        style={{ width: '9rem' }}
                        aria-label={`${el.name} 金額`}
                        value={cellValue(periodKey(el.code), periodAmount(el.code))}
                        onChange={(v) => onCellChange(periodKey(el.code), v)}
                        onBlur={() => onPeriodBlur(el.code)}
                      />
                    ) : (
                      <>¥{formatYen(periodAmount(el.code))}</>
                    )}
                  </td>
                </tr>
              ))
            )}
            <tr className="total-row">
              <td>合計</td>
              <td className="num">¥{formatYen(budget?.periodCostTotal ?? 0)}</td>
            </tr>
          </tbody>
        </table>

        {editable && (
          <p className="muted small">
            費目はシステム共通のマスタです。新しい費目は
            <Link to="/cost-elements">費目マスタ</Link>画面で追加してください。
          </p>
        )}
      </div>
    </>
  )
}
