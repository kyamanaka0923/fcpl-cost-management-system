import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, formatYen, revenueItemLabel, type CostElement, type CostPlanDetail } from '../api'

export default function PlanEditPage() {
  const { projectId, planId } = useParams<{ projectId: string; planId: string }>()
  const [plan, setPlan] = useState<CostPlanDetail | null>(null)
  const [elements, setElements] = useState<CostElement[]>([])
  const [revenueItems, setRevenueItems] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)

  const [elementCode, setElementCode] = useState('')
  const [revenueItem, setRevenueItem] = useState('')
  const [period, setPeriod] = useState('')
  const [amount, setAmount] = useState('')
  const [saving, setSaving] = useState(false)

  const load = useCallback(() => {
    if (!planId) return
    api.getPlan(planId).then(setPlan).catch((e: Error) => setError(e.message))
  }, [planId])
  useEffect(load, [load])
  useEffect(() => {
    api.listCostElements().then(setElements).catch((e: Error) => setError(e.message))
    if (projectId) api.listRevenueItems(projectId).then(setRevenueItems).catch(() => undefined)
  }, [projectId])

  if (!projectId || !planId) return null
  const editable = plan?.status === 'Draft'
  const elementName = (code: string) => elements.find((e) => e.code === code)?.name ?? code

  const upsert = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      const updated = await api.upsertPlanLine(planId, {
        elementCode,
        revenueItem: revenueItem || null,
        period,
        amount: Number(amount),
      })
      setPlan(updated)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const remove = async (code: string, item: string | null, p: string) => {
    setError(null)
    try {
      setPlan(await api.removePlanLine(planId, code, item, p))
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const approve = async () => {
    setError(null)
    try {
      setPlan(await api.approvePlan(planId))
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">プロジェクト一覧</Link> /{' '}
        <Link to={`/projects/${projectId}`}>プロジェクト</Link> / 原価予算編集
      </div>
      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>
          v{plan?.version} {plan?.label}
          <span className={`badge ${plan?.status.toLowerCase()}`} style={{ marginLeft: 12 }}>
            {plan?.status === 'Draft' ? '策定中' : plan?.status === 'Approved' ? '承認済' : '失効'}
          </span>
        </h2>
        <p>
          原価予算総額: <strong>¥{formatYen(plan?.totalAmount ?? 0)}</strong>
        </p>
        {editable && (
          <button className="primary" onClick={approve} disabled={(plan?.lines.length ?? 0) === 0}>
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
            同じ費目・売上対応品目・年月の明細は上書きされます。売上対応品目を空欄にすると
            共通費(特定の売上に対応しない原価)として扱われます。
          </p>
          <form onSubmit={upsert} className="form-row">
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
              売上対応品目(空欄 = 共通費)
              <input
                value={revenueItem}
                onChange={(e) => setRevenueItem(e.target.value)}
                placeholder="案件A"
                list="revenue-items"
              />
              <datalist id="revenue-items">
                {revenueItems.map((item) => (
                  <option key={item} value={item} />
                ))}
              </datalist>
            </label>
            <label>
              年月
              <input type="month" value={period} onChange={(e) => setPeriod(e.target.value)} required />
            </label>
            <label>
              金額(円)
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
        </div>
      )}

      <div className="card">
        <h2>原価予算明細</h2>
        {!plan || plan.lines.length === 0 ? (
          <p className="muted small">明細がありません。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>年月</th>
                <th>費目</th>
                <th>売上対応品目</th>
                <th className="num">金額</th>
                {editable && <th></th>}
              </tr>
            </thead>
            <tbody>
              {plan.lines.map((l) => (
                <tr key={l.id}>
                  <td>{l.period}</td>
                  <td>{elementName(l.elementCode)}</td>
                  <td className={l.revenueItem ? '' : 'muted'}>{revenueItemLabel(l.revenueItem)}</td>
                  <td className="num">¥{formatYen(l.amount)}</td>
                  {editable && (
                    <td>
                      <button onClick={() => remove(l.elementCode, l.revenueItem, l.period)}>
                        削除
                      </button>
                    </td>
                  )}
                </tr>
              ))}
              <tr className="total-row">
                <td colSpan={3}>合計</td>
                <td className="num">¥{formatYen(plan.totalAmount)}</td>
                {editable && <td></td>}
              </tr>
            </tbody>
          </table>
        )}
      </div>
    </>
  )
}
