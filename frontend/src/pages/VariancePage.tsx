import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  api,
  formatSignedYen,
  formatYen,
  type CostElement,
  type CostPlanSummary,
  type VarianceReport,
} from '../api'
import { PlannedVsActualChart, VarianceBarChart } from '../components/charts'

export default function VariancePage() {
  const { projectId } = useParams<{ projectId: string }>()
  const [plans, setPlans] = useState<CostPlanSummary[]>([])
  const [elements, setElements] = useState<CostElement[]>([])
  const [report, setReport] = useState<VarianceReport | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [planId, setPlanId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')

  useEffect(() => {
    if (!projectId) return
    api.listPlans(projectId).then(setPlans).catch((e: Error) => setError(e.message))
    api.listCostElements().then(setElements).catch(() => undefined)
  }, [projectId])

  const load = useCallback(() => {
    if (!projectId) return
    setError(null)
    api
      .getVariance(projectId, {
        planId: planId || undefined,
        from: from || undefined,
        to: to || undefined,
      })
      .then(setReport)
      .catch((e: Error) => {
        setReport(null)
        setError(e.message)
      })
  }, [projectId, planId, from, to])
  useEffect(load, [load])

  const elementName = useCallback(
    (code: string) => elements.find((e) => e.code === code)?.name ?? code,
    [elements],
  )

  const byPeriod = useMemo(() => {
    if (!report) return []
    const map = new Map<string, { planned: number; actual: number }>()
    for (const l of report.lines) {
      const entry = map.get(l.period) ?? { planned: 0, actual: 0 }
      entry.planned += l.plannedAmount
      entry.actual += l.actualAmount
      map.set(l.period, entry)
    }
    return [...map.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([label, v]) => ({ label, ...v }))
  }, [report])

  const byElement = useMemo(() => {
    if (!report) return []
    const map = new Map<string, number>()
    for (const l of report.lines) {
      map.set(l.elementCode, (map.get(l.elementCode) ?? 0) + l.totalVariance)
    }
    return [...map.entries()]
      .sort(([, a], [, b]) => b - a)
      .map(([code, value]) => ({ label: elementName(code), value }))
  }, [report, elementName])

  if (!projectId) return null

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">プロジェクト一覧</Link> /{' '}
        <Link to={`/projects/${projectId}`}>プロジェクト</Link> / 予実差異分析
      </div>
      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>分析条件</h2>
        <div className="form-row">
          <label>
            予算バージョン
            <select value={planId} onChange={(e) => setPlanId(e.target.value)}>
              <option value="">最新承認版(既定)</option>
              {plans
                .filter((p) => p.status !== 'Draft')
                .map((p) => (
                  <option key={p.id} value={p.id}>
                    v{p.version} {p.label}
                  </option>
                ))}
            </select>
          </label>
          <label>
            期間(自)
            <input type="month" value={from} onChange={(e) => setFrom(e.target.value)} />
          </label>
          <label>
            期間(至)
            <input type="month" value={to} onChange={(e) => setTo(e.target.value)} />
          </label>
        </div>
      </div>

      {report && (
        <>
          <div className="stat-row">
            <div className="stat-tile">
              <div className="label">
                予算総額(v{report.planVersion} {report.planLabel})
              </div>
              <div className="value">¥{formatYen(report.totalPlannedAmount)}</div>
            </div>
            <div className="stat-tile">
              <div className="label">実績総額</div>
              <div className="value">¥{formatYen(report.totalActualAmount)}</div>
            </div>
            <div className="stat-tile">
              <div className="label">総差異(実績 − 予算)</div>
              <div className={`value ${report.totalVariance > 0 ? 'adverse' : 'favorable'}`}>
                ¥{formatSignedYen(report.totalVariance)}
              </div>
            </div>
          </div>

          <div className="card">
            <h2>月別 予算 vs 実績</h2>
            <PlannedVsActualChart data={byPeriod} />
          </div>

          <div className="card">
            <h2>費目別 差異</h2>
            <VarianceBarChart data={byElement} />
          </div>

          <div className="card">
            <h2>差異明細(価格差異・数量差異の分解)</h2>
            <p className="muted small">
              価格差異 = (実際単価 − 予定単価) × 実際数量、数量差異 = (実際数量 − 予定数量) × 予定単価。
              正の値は不利差異(予算超過)、負の値は有利差異です。
            </p>
            <table>
              <thead>
                <tr>
                  <th>年月</th>
                  <th>費目</th>
                  <th className="num">予算金額</th>
                  <th className="num">実績金額</th>
                  <th className="num">総差異</th>
                  <th className="num">価格差異</th>
                  <th className="num">数量差異</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {report.lines.map((l) => (
                  <tr key={`${l.period}-${l.elementCode}`}>
                    <td>{l.period}</td>
                    <td>{elementName(l.elementCode)}</td>
                    <td className="num">¥{formatYen(l.plannedAmount)}</td>
                    <td className="num">¥{formatYen(l.actualAmount)}</td>
                    <td className={`num ${l.totalVariance > 0 ? 'adverse' : l.totalVariance < 0 ? 'favorable' : ''}`}>
                      ¥{formatSignedYen(l.totalVariance)}
                    </td>
                    <td className="num">
                      {l.priceVariance !== null ? `¥${formatSignedYen(l.priceVariance)}` : '—'}
                    </td>
                    <td className="num">
                      {l.quantityVariance !== null ? `¥${formatSignedYen(l.quantityVariance)}` : '—'}
                    </td>
                    <td className="small muted">{l.isUnplanned ? '予定外' : ''}</td>
                  </tr>
                ))}
                <tr className="total-row">
                  <td colSpan={2}>合計</td>
                  <td className="num">¥{formatYen(report.totalPlannedAmount)}</td>
                  <td className="num">¥{formatYen(report.totalActualAmount)}</td>
                  <td className={`num ${report.totalVariance > 0 ? 'adverse' : 'favorable'}`}>
                    ¥{formatSignedYen(report.totalVariance)}
                  </td>
                  <td colSpan={3}></td>
                </tr>
              </tbody>
            </table>
          </div>
        </>
      )}
    </>
  )
}
