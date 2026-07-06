import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  api,
  formatSignedYen,
  formatYen,
  type CostElement,
  type CostPlanSummary,
  type ProfitSummary,
  type RevenuePlanSummary,
  type RevenueVarianceReport,
  type VarianceReport,
} from '../api'
import { PlannedVsActualChart, VarianceBarChart } from '../components/charts'

type Tab = 'cost' | 'revenue' | 'profit'

function PeriodFilter({
  from,
  to,
  setFrom,
  setTo,
  children,
}: {
  from: string
  to: string
  setFrom: (v: string) => void
  setTo: (v: string) => void
  children?: React.ReactNode
}) {
  return (
    <div className="card">
      <h2>分析条件</h2>
      <div className="form-row">
        {children}
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
  )
}

function CostVarianceTab({ projectId }: { projectId: string }) {
  const [plans, setPlans] = useState<CostPlanSummary[]>([])
  const [elements, setElements] = useState<CostElement[]>([])
  const [report, setReport] = useState<VarianceReport | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [planId, setPlanId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')

  useEffect(() => {
    api.listPlans(projectId).then(setPlans).catch((e: Error) => setError(e.message))
    api.listCostElements().then(setElements).catch(() => undefined)
  }, [projectId])

  const load = useCallback(() => {
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

  return (
    <>
      {error && <div className="error-banner">{error}</div>}
      <PeriodFilter from={from} to={to} setFrom={setFrom} setTo={setTo}>
        <label>
          原価予算バージョン
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
      </PeriodFilter>

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
            <h2>月別 原価予算 vs 実績</h2>
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

function RevenueVarianceTab({ projectId }: { projectId: string }) {
  const [plans, setPlans] = useState<RevenuePlanSummary[]>([])
  const [report, setReport] = useState<RevenueVarianceReport | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [planId, setPlanId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')

  useEffect(() => {
    api.listRevenuePlans(projectId).then(setPlans).catch((e: Error) => setError(e.message))
  }, [projectId])

  const load = useCallback(() => {
    setError(null)
    api
      .getRevenueVariance(projectId, {
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

  const byItem = useMemo(() => {
    if (!report) return []
    const map = new Map<string, number>()
    for (const l of report.lines) {
      map.set(l.itemName, (map.get(l.itemName) ?? 0) + l.totalVariance)
    }
    return [...map.entries()]
      .sort(([, a], [, b]) => b - a)
      .map(([label, value]) => ({ label, value }))
  }, [report])

  return (
    <>
      {error && <div className="error-banner">{error}</div>}
      <PeriodFilter from={from} to={to} setFrom={setFrom} setTo={setTo}>
        <label>
          売上予算バージョン
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
      </PeriodFilter>

      {report && (
        <>
          <div className="stat-row">
            <div className="stat-tile">
              <div className="label">
                売上予算総額(v{report.planVersion} {report.planLabel})
              </div>
              <div className="value">¥{formatYen(report.totalPlannedAmount)}</div>
            </div>
            <div className="stat-tile">
              <div className="label">売上実績総額</div>
              <div className="value">¥{formatYen(report.totalActualAmount)}</div>
            </div>
            <div className="stat-tile">
              <div className="label">総差異(実績 − 予算)</div>
              <div className={`value ${report.totalVariance >= 0 ? 'favorable' : 'adverse'}`}>
                ¥{formatSignedYen(report.totalVariance)}
              </div>
            </div>
          </div>

          <div className="card">
            <h2>月別 売上予算 vs 実績</h2>
            <PlannedVsActualChart data={byPeriod} />
          </div>

          <div className="card">
            <h2>品目別 差異</h2>
            <VarianceBarChart
              data={byItem}
              adverseWhenPositive={false}
              legendLabels={['有利差異(売上超過)', '不利差異(売上未達)']}
            />
          </div>

          <div className="card">
            <h2>差異明細(販売価格差異・販売数量差異の分解)</h2>
            <p className="muted small">
              販売価格差異 = (実際単価 − 予定単価) × 実際数量、販売数量差異 = (実際数量 − 予定数量) ×
              予定単価。売上は原価と逆で、正の値が有利差異(売上超過)、負の値が不利差異(未達)です。
            </p>
            <table>
              <thead>
                <tr>
                  <th>年月</th>
                  <th>品目</th>
                  <th className="num">予算金額</th>
                  <th className="num">実績金額</th>
                  <th className="num">総差異</th>
                  <th className="num">販売価格差異</th>
                  <th className="num">販売数量差異</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {report.lines.map((l) => (
                  <tr key={`${l.period}-${l.itemName}`}>
                    <td>{l.period}</td>
                    <td>{l.itemName}</td>
                    <td className="num">¥{formatYen(l.plannedAmount)}</td>
                    <td className="num">¥{formatYen(l.actualAmount)}</td>
                    <td className={`num ${l.totalVariance > 0 ? 'favorable' : l.totalVariance < 0 ? 'adverse' : ''}`}>
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
                  <td className={`num ${report.totalVariance >= 0 ? 'favorable' : 'adverse'}`}>
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

function ProfitTab({ projectId }: { projectId: string }) {
  const [summary, setSummary] = useState<ProfitSummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')

  const load = useCallback(() => {
    setError(null)
    api
      .getProfit(projectId, { from: from || undefined, to: to || undefined })
      .then(setSummary)
      .catch((e: Error) => {
        setSummary(null)
        setError(e.message)
      })
  }, [projectId, from, to])
  useEffect(load, [load])

  const profitByPeriod = useMemo(
    () =>
      summary?.periodLines.map((l) => ({
        label: l.period,
        planned: l.plannedProfit,
        actual: l.actualProfit,
      })) ?? [],
    [summary],
  )

  const pct = (v: number | null) => (v === null ? '—' : `${(v * 100).toFixed(1)}%`)

  return (
    <>
      {error && <div className="error-banner">{error}</div>}
      <PeriodFilter from={from} to={to} setFrom={setFrom} setTo={setTo}>
        <span className="muted small" style={{ alignSelf: 'center' }}>
          最新の承認済み売上予算・原価予算を突き合わせます。
        </span>
      </PeriodFilter>

      {summary && (
        <>
          <div className="stat-row">
            <div className="stat-tile">
              <div className="label">
                粗利予算(売上 v{summary.revenuePlanVersion} − 原価 v{summary.costPlanVersion})
              </div>
              <div className="value">¥{formatYen(summary.plannedProfit)}</div>
              <div className="label">粗利率 {pct(summary.plannedMarginRate)}</div>
            </div>
            <div className="stat-tile">
              <div className="label">粗利実績</div>
              <div className="value">¥{formatYen(summary.actualProfit)}</div>
              <div className="label">粗利率 {pct(summary.actualMarginRate)}</div>
            </div>
            <div className="stat-tile">
              <div className="label">粗利差異(実績 − 予算)</div>
              <div className={`value ${summary.profitVariance >= 0 ? 'favorable' : 'adverse'}`}>
                ¥{formatSignedYen(summary.profitVariance)}
              </div>
            </div>
          </div>

          <div className="card">
            <h2>月別 粗利 予算 vs 実績</h2>
            <PlannedVsActualChart data={profitByPeriod} seriesLabels={['粗利予算', '粗利実績']} />
          </div>

          <div className="card">
            <h2>損益内訳</h2>
            <table>
              <thead>
                <tr>
                  <th></th>
                  <th className="num">予算</th>
                  <th className="num">実績</th>
                  <th className="num">差異</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>売上高</td>
                  <td className="num">¥{formatYen(summary.plannedRevenue)}</td>
                  <td className="num">¥{formatYen(summary.actualRevenue)}</td>
                  <td className={`num ${summary.revenueVariance >= 0 ? 'favorable' : 'adverse'}`}>
                    ¥{formatSignedYen(summary.revenueVariance)}
                  </td>
                </tr>
                <tr>
                  <td>総原価</td>
                  <td className="num">¥{formatYen(summary.plannedCost)}</td>
                  <td className="num">¥{formatYen(summary.actualCost)}</td>
                  <td className={`num ${summary.costVariance > 0 ? 'adverse' : 'favorable'}`}>
                    ¥{formatSignedYen(summary.costVariance)}
                  </td>
                </tr>
                <tr className="total-row">
                  <td>粗利</td>
                  <td className="num">¥{formatYen(summary.plannedProfit)}</td>
                  <td className="num">¥{formatYen(summary.actualProfit)}</td>
                  <td className={`num ${summary.profitVariance >= 0 ? 'favorable' : 'adverse'}`}>
                    ¥{formatSignedYen(summary.profitVariance)}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div className="card">
            <h2>月別内訳</h2>
            <table>
              <thead>
                <tr>
                  <th>年月</th>
                  <th className="num">売上予算</th>
                  <th className="num">売上実績</th>
                  <th className="num">原価予算</th>
                  <th className="num">原価実績</th>
                  <th className="num">粗利予算</th>
                  <th className="num">粗利実績</th>
                  <th className="num">粗利差異</th>
                </tr>
              </thead>
              <tbody>
                {summary.periodLines.map((l) => (
                  <tr key={l.period}>
                    <td>{l.period}</td>
                    <td className="num">¥{formatYen(l.plannedRevenue)}</td>
                    <td className="num">¥{formatYen(l.actualRevenue)}</td>
                    <td className="num">¥{formatYen(l.plannedCost)}</td>
                    <td className="num">¥{formatYen(l.actualCost)}</td>
                    <td className="num">¥{formatYen(l.plannedProfit)}</td>
                    <td className="num">¥{formatYen(l.actualProfit)}</td>
                    <td className={`num ${l.profitVariance >= 0 ? 'favorable' : 'adverse'}`}>
                      ¥{formatSignedYen(l.profitVariance)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </>
  )
}

export default function VariancePage() {
  const { projectId } = useParams<{ projectId: string }>()
  const [tab, setTab] = useState<Tab>('cost')

  if (!projectId) return null

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">プロジェクト一覧</Link> /{' '}
        <Link to={`/projects/${projectId}`}>プロジェクト</Link> / 予実差異分析・損益
      </div>

      <div className="tab-row">
        <button className={tab === 'cost' ? 'active' : ''} onClick={() => setTab('cost')}>
          原価差異
        </button>
        <button className={tab === 'revenue' ? 'active' : ''} onClick={() => setTab('revenue')}>
          売上差異
        </button>
        <button className={tab === 'profit' ? 'active' : ''} onClick={() => setTab('profit')}>
          損益(粗利)
        </button>
      </div>

      {tab === 'cost' && <CostVarianceTab projectId={projectId} />}
      {tab === 'revenue' && <RevenueVarianceTab projectId={projectId} />}
      {tab === 'profit' && <ProfitTab projectId={projectId} />}
    </>
  )
}
