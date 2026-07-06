import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, formatYen, type ActualRevenue } from '../api'

export default function RevenueActualsPage() {
  const { projectId } = useParams<{ projectId: string }>()
  const [actuals, setActuals] = useState<ActualRevenue[]>([])
  const [revenueItems, setRevenueItems] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)

  const [itemName, setItemName] = useState('')
  const [period, setPeriod] = useState('')
  const [amount, setAmount] = useState('')
  const [note, setNote] = useState('')
  const [saving, setSaving] = useState(false)

  const load = useCallback(() => {
    if (!projectId) return
    api.listActualRevenues(projectId).then(setActuals).catch((e: Error) => setError(e.message))
    api.listRevenueItems(projectId).then(setRevenueItems).catch(() => undefined)
  }, [projectId])
  useEffect(load, [load])

  if (!projectId) return null
  const total = actuals.reduce((sum, a) => sum + a.amount, 0)

  const record = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await api.recordActualRevenue(projectId, {
        itemName,
        period,
        amount: Number(amount),
        note: note || null,
      })
      setNote('')
      load()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const remove = async (id: string) => {
    setError(null)
    try {
      await api.deleteActualRevenue(id)
      load()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">プロジェクト一覧</Link> /{' '}
        <Link to={`/projects/${projectId}`}>プロジェクト</Link> / 売上実績入力
      </div>
      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>売上実績の計上</h2>
        <p className="muted small">
          同じ品目・年月に複数回計上でき、分析時には合算されます(検収単位の計上などを想定)。
        </p>
        <form onSubmit={record} className="form-row">
          <label>
            品目(案件名など)
            <input
              value={itemName}
              onChange={(e) => setItemName(e.target.value)}
              required
              placeholder="案件A"
              list="revenue-items-rev-actual"
            />
            <datalist id="revenue-items-rev-actual">
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
          <label>
            摘要
            <input value={note} onChange={(e) => setNote(e.target.value)} placeholder="任意" />
          </label>
          <button type="submit" className="primary" disabled={saving}>
            計上
          </button>
        </form>
      </div>

      <div className="card">
        <h2>売上実績一覧</h2>
        {actuals.length === 0 ? (
          <p className="muted small">実績がありません。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>年月</th>
                <th>品目</th>
                <th className="num">金額</th>
                <th>摘要</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {actuals.map((a) => (
                <tr key={a.id}>
                  <td>{a.period}</td>
                  <td>{a.itemName}</td>
                  <td className="num">¥{formatYen(a.amount)}</td>
                  <td className="small muted">{a.note ?? ''}</td>
                  <td>
                    <button onClick={() => remove(a.id)}>削除</button>
                  </td>
                </tr>
              ))}
              <tr className="total-row">
                <td colSpan={2}>合計</td>
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
