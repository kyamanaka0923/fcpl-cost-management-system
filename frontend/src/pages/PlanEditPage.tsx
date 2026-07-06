import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, formatYen, type CostElement, type CostPlanDetail } from '../api'

export default function PlanEditPage() {
  const { projectId, planId } = useParams<{ projectId: string; planId: string }>()
  const [plan, setPlan] = useState<CostPlanDetail | null>(null)
  const [elements, setElements] = useState<CostElement[]>([])
  const [error, setError] = useState<string | null>(null)

  const [elementCode, setElementCode] = useState('')
  const [period, setPeriod] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [unitPrice, setUnitPrice] = useState('')
  const [saving, setSaving] = useState(false)

  const load = useCallback(() => {
    if (!planId) return
    api.getPlan(planId).then(setPlan).catch((e: Error) => setError(e.message))
  }, [planId])
  useEffect(load, [load])
  useEffect(() => {
    api.listCostElements().then(setElements).catch((e: Error) => setError(e.message))
  }, [])

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
        period,
        quantity: Number(quantity),
        unitPrice: Number(unitPrice),
      })
      setPlan(updated)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const remove = async (code: string, p: string) => {
    setError(null)
    try {
      setPlan(await api.removePlanLine(planId, code, p))
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
        <Link to={`/projects/${projectId}`}>プロジェクト</Link> / 予算編集
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
          総額: <strong>¥{formatYen(plan?.totalAmount ?? 0)}</strong>
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
          <p className="muted small">同じ費目・年月の明細は上書きされます。</p>
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
              年月
              <input type="month" value={period} onChange={(e) => setPeriod(e.target.value)} required />
            </label>
            <label>
              数量
              <input
                type="number"
                value={quantity}
                onChange={(e) => setQuantity(e.target.value)}
                min={0}
                step="any"
                required
              />
            </label>
            <label>
              単価(円)
              <input
                type="number"
                value={unitPrice}
                onChange={(e) => setUnitPrice(e.target.value)}
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
        <h2>予算明細</h2>
        {!plan || plan.lines.length === 0 ? (
          <p className="muted small">明細がありません。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>年月</th>
                <th>費目</th>
                <th className="num">数量</th>
                <th className="num">単価</th>
                <th className="num">金額</th>
                {editable && <th></th>}
              </tr>
            </thead>
            <tbody>
              {plan.lines.map((l) => (
                <tr key={l.id}>
                  <td>{l.period}</td>
                  <td>{elementName(l.elementCode)}</td>
                  <td className="num">{formatYen(l.quantity)}</td>
                  <td className="num">¥{formatYen(l.unitPrice)}</td>
                  <td className="num">¥{formatYen(l.amount)}</td>
                  {editable && (
                    <td>
                      <button onClick={() => remove(l.elementCode, l.period)}>削除</button>
                    </td>
                  )}
                </tr>
              ))}
              <tr className="total-row">
                <td colSpan={4}>合計</td>
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
