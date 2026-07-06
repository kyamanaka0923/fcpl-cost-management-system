import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, formatYen, type ActualCost, type CostElement } from '../api'

export default function ActualsPage() {
  const { projectId } = useParams<{ projectId: string }>()
  const [actuals, setActuals] = useState<ActualCost[]>([])
  const [elements, setElements] = useState<CostElement[]>([])
  const [error, setError] = useState<string | null>(null)

  const [elementCode, setElementCode] = useState('')
  const [period, setPeriod] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [unitPrice, setUnitPrice] = useState('')
  const [note, setNote] = useState('')
  const [saving, setSaving] = useState(false)

  const load = useCallback(() => {
    if (!projectId) return
    api.listActuals(projectId).then(setActuals).catch((e: Error) => setError(e.message))
  }, [projectId])
  useEffect(load, [load])
  useEffect(() => {
    api.listCostElements().then(setElements).catch((e: Error) => setError(e.message))
  }, [])

  if (!projectId) return null
  const elementName = (code: string) => elements.find((e) => e.code === code)?.name ?? code
  const total = actuals.reduce((sum, a) => sum + a.amount, 0)

  const record = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await api.recordActual(projectId, {
        elementCode,
        period,
        quantity: Number(quantity),
        unitPrice: Number(unitPrice),
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
      await api.deleteActual(id)
      load()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">プロジェクト一覧</Link> /{' '}
        <Link to={`/projects/${projectId}`}>プロジェクト</Link> / 実績入力
      </div>
      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>実績の計上</h2>
        <p className="muted small">
          同じ費目・年月に複数回計上できます。分析時には合算され、単価は加重平均で扱われます。
        </p>
        <form onSubmit={record} className="form-row">
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
        <h2>実績一覧</h2>
        {actuals.length === 0 ? (
          <p className="muted small">実績がありません。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>年月</th>
                <th>費目</th>
                <th className="num">数量</th>
                <th className="num">単価</th>
                <th className="num">金額</th>
                <th>摘要</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {actuals.map((a) => (
                <tr key={a.id}>
                  <td>{a.period}</td>
                  <td>{elementName(a.elementCode)}</td>
                  <td className="num">{formatYen(a.quantity)}</td>
                  <td className="num">¥{formatYen(a.unitPrice)}</td>
                  <td className="num">¥{formatYen(a.amount)}</td>
                  <td className="small muted">{a.note ?? ''}</td>
                  <td>
                    <button onClick={() => remove(a.id)}>削除</button>
                  </td>
                </tr>
              ))}
              <tr className="total-row">
                <td colSpan={4}>合計</td>
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
