import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, formatYen, type RevenuePlanDetail } from '../api'

export default function RevenuePlanEditPage() {
  const { projectId, planId } = useParams<{ projectId: string; planId: string }>()
  const [plan, setPlan] = useState<RevenuePlanDetail | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [itemName, setItemName] = useState('')
  const [period, setPeriod] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [unitPrice, setUnitPrice] = useState('')
  const [saving, setSaving] = useState(false)

  const load = useCallback(() => {
    if (!planId) return
    api.getRevenuePlan(planId).then(setPlan).catch((e: Error) => setError(e.message))
  }, [planId])
  useEffect(load, [load])

  if (!projectId || !planId) return null
  const editable = plan?.status === 'Draft'

  const upsert = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      const updated = await api.upsertRevenuePlanLine(planId, {
        itemName,
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

  const remove = async (item: string, p: string) => {
    setError(null)
    try {
      setPlan(await api.removeRevenuePlanLine(planId, item, p))
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const approve = async () => {
    setError(null)
    try {
      setPlan(await api.approveRevenuePlan(planId))
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">プロジェクト一覧</Link> /{' '}
        <Link to={`/projects/${projectId}`}>プロジェクト</Link> / 売上予算編集
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
          売上予算総額: <strong>¥{formatYen(plan?.totalAmount ?? 0)}</strong>
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
          <p className="muted small">同じ品目・年月の明細は上書きされます。</p>
          <form onSubmit={upsert} className="form-row">
            <label>
              品目
              <input
                value={itemName}
                onChange={(e) => setItemName(e.target.value)}
                required
                placeholder="製品A"
              />
            </label>
            <label>
              年月
              <input type="month" value={period} onChange={(e) => setPeriod(e.target.value)} required />
            </label>
            <label>
              販売数量
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
              販売単価(円)
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
        <h2>売上予算明細</h2>
        {!plan || plan.lines.length === 0 ? (
          <p className="muted small">明細がありません。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>年月</th>
                <th>品目</th>
                <th className="num">販売数量</th>
                <th className="num">販売単価</th>
                <th className="num">金額</th>
                {editable && <th></th>}
              </tr>
            </thead>
            <tbody>
              {plan.lines.map((l) => (
                <tr key={l.id}>
                  <td>{l.period}</td>
                  <td>{l.itemName}</td>
                  <td className="num">{formatYen(l.quantity)}</td>
                  <td className="num">¥{formatYen(l.unitPrice)}</td>
                  <td className="num">¥{formatYen(l.amount)}</td>
                  {editable && (
                    <td>
                      <button onClick={() => remove(l.itemName, l.period)}>削除</button>
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
