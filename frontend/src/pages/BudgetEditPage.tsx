import { Fragment, useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import {
  api,
  categoryLabel,
  currentFiscalHalf,
  formatPercent,
  formatYen,
  halfLabel,
  halfMonths,
  marginRate,
  projectCategories,
  type BudgetCategory,
  type BudgetDetail,
  type BudgetLine,
  type CostElement,
  type Project,
} from '../api'
import MoneyInput from '../components/MoneyInput'
import Modal from '../components/Modal'
import Toast from '../components/Toast'

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

  // 案件のインライン編集(コード・名称)
  const [editingProjectId, setEditingProjectId] = useState<string | null>(null)
  const [editProjectCode, setEditProjectCode] = useState('')
  const [editProjectName, setEditProjectName] = useState('')
  const [savingProject, setSavingProject] = useState(false)

  // 月次入力ダイアログ(明細ごとに半期一括↔月次を切り替える)
  const [monthlyTarget, setMonthlyTarget] = useState<{
    category: BudgetCategory
    projectId: string | null
    elementCode: string | null
    label: string
  } | null>(null)
  const [monthlyEdits, setMonthlyEdits] = useState<Record<number, string>>({})
  const [savingMonthly, setSavingMonthly] = useState(false)

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
  // 案件粗利率 = 損益 ÷ 売上高。売上高が0なら「—」。
  const projectMargin = (projectId: string): number | null =>
    marginRate(projectProfit(projectId), projectAmount(projectId, 'Revenue'))

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

  // ---- 月次入力 ----
  const findLine = (
    category: BudgetCategory,
    projectId: string | null,
    elementCode: string | null,
  ): BudgetLine | undefined =>
    budget?.lines.find(
      (l) =>
        l.category === category &&
        (l.projectId ?? null) === projectId &&
        (l.elementCode ?? null) === elementCode,
    )

  const openMonthly = (
    category: BudgetCategory,
    projectId: string | null,
    elementCode: string | null,
    label: string,
  ) => {
    const line = findLine(category, projectId, elementCode)
    const init: Record<number, string> = {}
    if (line?.isMonthly) {
      for (const [m, amt] of Object.entries(line.monthlyAmounts)) init[Number(m)] = String(amt)
    }
    setMonthlyEdits(init)
    setMonthlyTarget({ category, projectId, elementCode, label })
    setError(null)
  }

  const monthlyDialogTotal = halfMonths(half).reduce((sum, { index }) => {
    const raw = (monthlyEdits[index] ?? '').trim()
    const n = raw === '' ? 0 : Number(raw)
    return sum + (Number.isNaN(n) ? 0 : n)
  }, 0)

  const saveMonthly = async () => {
    if (!monthlyTarget) return
    setSavingMonthly(true)
    setError(null)
    try {
      const monthlyAmounts: Record<number, number> = {}
      for (const { index } of halfMonths(half)) {
        const raw = (monthlyEdits[index] ?? '').trim()
        const n = raw === '' ? 0 : Number(raw)
        if (!Number.isNaN(n) && n > 0) monthlyAmounts[index] = n
      }
      const { category, projectId, elementCode } = monthlyTarget
      const updated =
        Object.keys(monthlyAmounts).length === 0
          ? await api.removeBudgetLine(budgetId, category, projectId, elementCode)
          : await api.upsertBudgetLine(budgetId, {
              category,
              projectId,
              elementCode,
              amount: 0,
              monthlyAmounts,
            })
      setBudget(updated)
      setMonthlyTarget(null)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSavingMonthly(false)
    }
  }

  const revertMonthlyToHalf = async () => {
    if (!monthlyTarget) return
    setSavingMonthly(true)
    setError(null)
    try {
      const { category, projectId, elementCode } = monthlyTarget
      const total = findLine(category, projectId, elementCode)?.amount ?? 0
      const updated =
        total > 0
          ? await api.upsertBudgetLine(budgetId, { category, projectId, elementCode, amount: total })
          : await api.removeBudgetLine(budgetId, category, projectId, elementCode)
      setBudget(updated)
      setMonthlyTarget(null)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSavingMonthly(false)
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

  const startEditProject = (p: Project) => {
    setEditingProjectId(p.id)
    setEditProjectCode(p.code)
    setEditProjectName(p.name)
    setError(null)
  }

  const cancelEditProject = () => setEditingProjectId(null)

  const saveEditProject = async (projectId: string) => {
    setSavingProject(true)
    setError(null)
    try {
      await api.updateProject(projectId, { code: editProjectCode, name: editProjectName })
      setEditingProjectId(null)
      loadProjects()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSavingProject(false)
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
      <Toast message={error} onClose={() => setError(null)} />

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
            <div className="label">
              粗利率 {formatPercent(marginRate(budget?.plannedProfit ?? 0, budget?.revenueTotal ?? 0))}
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
          {editable && '金額を入力して次の欄へ移ると自動保存されます。各セルの「月次入力」で月ごとの金額も入力でき、その明細は半期合計が自動算出されます。'}
          案件名の横の「編集」からコード・名称を後から変更できます(参照はGUIDのため予算・実績は保持されます)。
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
              <th className="num">粗利率</th>
            </tr>
          </thead>
          <tbody>
            {projects.length === 0 ? (
              <tr>
                <td colSpan={6} className="muted small">
                  案件がありません。{editable ? '下の行から案件を追加してください。' : ''}
                </td>
              </tr>
            ) : (
              projects.map((p) => (
                <tr key={p.id}>
                  <td>
                    {editingProjectId === p.id ? (
                      <span className="project-edit">
                        <input
                          aria-label="案件コード編集"
                          value={editProjectCode}
                          onChange={(e) => setEditProjectCode(e.target.value)}
                          style={{ width: '7rem' }}
                        />
                        <input
                          aria-label="案件名編集"
                          value={editProjectName}
                          onChange={(e) => setEditProjectName(e.target.value)}
                          style={{ width: '10rem' }}
                        />
                        <button
                          className="primary"
                          onClick={() => saveEditProject(p.id)}
                          disabled={savingProject}
                        >
                          保存
                        </button>
                        <button onClick={cancelEditProject} disabled={savingProject}>
                          取消
                        </button>
                      </span>
                    ) : (
                      <>
                        {p.name}
                        <span className="muted small">({p.code})</span>
                        <button
                          className="row-edit-link"
                          onClick={() => startEditProject(p)}
                          aria-label={`${p.name} を編集`}
                        >
                          編集
                        </button>
                      </>
                    )}
                  </td>
                  {projectCategories.map((category) => {
                    const monthly = findLine(category, p.id, null)?.isMonthly ?? false
                    const label = `${p.name} ${categoryLabel[category]}`
                    return (
                      <td className="num" key={category}>
                        {editable && !monthly && (
                          <MoneyInput
                            className="num"
                            style={{ width: '9rem' }}
                            aria-label={label}
                            value={cellValue(projectKey(p.id, category), projectAmount(p.id, category))}
                            onChange={(v) => onCellChange(projectKey(p.id, category), v)}
                            onBlur={() => onProjectBlur(p.id, category)}
                          />
                        )}
                        {(!editable || monthly) && (
                          <span>
                            ¥{formatYen(projectAmount(p.id, category))}
                            {monthly && <span className="badge-monthly">月次</span>}
                          </span>
                        )}
                        {editable && (
                          <button
                            className="cell-monthly-btn"
                            aria-label={`${label} を月次入力`}
                            onClick={() => openMonthly(category, p.id, null, label)}
                          >
                            {monthly ? '月次編集' : '月次入力'}
                          </button>
                        )}
                      </td>
                    )
                  })}
                  <td className={`num ${projectProfit(p.id) >= 0 ? 'favorable' : 'adverse'}`}>
                    ¥{formatYen(projectProfit(p.id))}
                  </td>
                  <td className="num muted">{formatPercent(projectMargin(p.id))}</td>
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
              <td className="num muted">
                {formatPercent(
                  marginRate(
                    (budget?.revenueTotal ?? 0) -
                      (budget?.processingTotal ?? 0) -
                      (budget?.outsourcingTotal ?? 0),
                    budget?.revenueTotal ?? 0,
                  ),
                )}
              </td>
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
                    {(() => {
                      const monthly = findLine('PeriodCost', null, el.code)?.isMonthly ?? false
                      const label = `${el.name} 金額`
                      return (
                        <>
                          {editable && !monthly && (
                            <MoneyInput
                              className="num"
                              style={{ width: '9rem' }}
                              aria-label={label}
                              value={cellValue(periodKey(el.code), periodAmount(el.code))}
                              onChange={(v) => onCellChange(periodKey(el.code), v)}
                              onBlur={() => onPeriodBlur(el.code)}
                            />
                          )}
                          {(!editable || monthly) && (
                            <span>
                              ¥{formatYen(periodAmount(el.code))}
                              {monthly && <span className="badge-monthly">月次</span>}
                            </span>
                          )}
                          {editable && (
                            <button
                              className="cell-monthly-btn"
                              aria-label={`${el.name} を月次入力`}
                              onClick={() => openMonthly('PeriodCost', null, el.code, label)}
                            >
                              {monthly ? '月次編集' : '月次入力'}
                            </button>
                          )}
                        </>
                      )
                    })()}
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

      {monthlyTarget && (
        <Modal title={`月次入力 — ${monthlyTarget.label}`} onClose={() => setMonthlyTarget(null)}>
          <p className="muted small">
            {halfLabel(half)}の各月の金額を入力します。半期合計は自動計算され、月次入力にすると
            この明細の半期一括入力は無効になります。
          </p>
          <div className="month-grid">
            {halfMonths(half).map(({ index, label }) => (
              <Fragment key={index}>
                <label>{label}</label>
                <MoneyInput
                  aria-label={`月次 ${label}`}
                  value={monthlyEdits[index] ?? ''}
                  onChange={(v) => setMonthlyEdits((prev) => ({ ...prev, [index]: v }))}
                />
              </Fragment>
            ))}
          </div>
          <div className="month-total">
            <span>半期合計</span>
            <span>¥{formatYen(monthlyDialogTotal)}</span>
          </div>
          <div className="modal-actions">
            <button className="spacer" onClick={revertMonthlyToHalf} disabled={savingMonthly}>
              半期一括に戻す
            </button>
            <button onClick={() => setMonthlyTarget(null)} disabled={savingMonthly}>
              取消
            </button>
            <button className="primary" onClick={saveMonthly} disabled={savingMonthly}>
              保存
            </button>
          </div>
        </Modal>
      )}
    </>
  )
}
