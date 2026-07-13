import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import {
  api,
  categoryLabel,
  currentFiscalHalf,
  formatSignedYen,
  formatYen,
  halfLabel,
  type BudgetSummary,
  type ProfitSummary,
  type VarianceLine,
  type VarianceReport,
} from '../api'
import { PlannedVsActualChart, VarianceBarChart } from '../components/charts'
import Toast from '../components/Toast'

type Tab = 'variance' | 'profit'

const lineName = (l: VarianceLine): string =>
  l.category === 'PeriodCost'
    ? (l.elementName ?? l.elementCode ?? '')
    : (l.projectName ?? l.projectId ?? '')

function VarianceTab({ departmentId, half }: { departmentId: string; half: string }) {
  const [budgets, setBudgets] = useState<BudgetSummary[]>([])
  const [report, setReport] = useState<VarianceReport | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [budgetId, setBudgetId] = useState('')

  useEffect(() => {
    api.listBudgets(departmentId, half).then(setBudgets).catch((e: Error) => setError(e.message))
  }, [departmentId, half])

  const load = useCallback(() => {
    setError(null)
    api
      .getVariance(departmentId, half, { budgetId: budgetId || undefined })
      .then(setReport)
      .catch((e: Error) => {
        setReport(null)
        setError(e.message)
      })
  }, [departmentId, half, budgetId])
  useEffect(load, [load])

  const byCategory = useMemo(
    () =>
      report?.categories.map((c) => ({
        label: categoryLabel[c.category],
        planned: c.plannedAmount,
        actual: c.actualAmount,
      })) ?? [],
    [report],
  )

  const costVarianceLines = useMemo(
    () =>
      report?.categories
        .filter((c) => c.category !== 'Revenue')
        .flatMap((c) =>
          c.lines.map((l) => ({
            label: `${categoryLabel[l.category]}: ${lineName(l)}`,
            value: l.variance,
          })),
        )
        .sort((a, b) => b.value - a.value) ?? [],
    [report],
  )

  return (
    <>
      <Toast message={error} onClose={() => setError(null)} />
      <div className="card">
        <h2>分析条件</h2>
        <div className="form-row">
          <label>
            予算バージョン
            <select value={budgetId} onChange={(e) => setBudgetId(e.target.value)}>
              <option value="">最新承認版(既定)</option>
              {budgets
                .filter((b) => b.status !== 'Draft')
                .map((b) => (
                  <option key={b.id} value={b.id}>
                    v{b.version} {b.label}
                  </option>
                ))}
            </select>
          </label>
        </div>
      </div>

      {report && (
        <>
          <div className="stat-row">
            <div className="stat-tile">
              <div className="label">
                売上高差異(v{report.budgetVersion} {report.budgetLabel})
              </div>
              <div className={`value ${report.revenueVariance >= 0 ? 'favorable' : 'adverse'}`}>
                ¥{formatSignedYen(report.revenueVariance)}
              </div>
            </div>
            <div className="stat-tile">
              <div className="label">コスト予算(加工費 + 外注費 + 期間費用)</div>
              <div className="value">¥{formatYen(report.plannedCost)}</div>
            </div>
            <div className="stat-tile">
              <div className="label">コスト実績</div>
              <div className="value">¥{formatYen(report.actualCost)}</div>
            </div>
            <div className="stat-tile">
              <div className="label">コスト差異(実績 − 予算)</div>
              <div className={`value ${report.costVariance > 0 ? 'adverse' : 'favorable'}`}>
                ¥{formatSignedYen(report.costVariance)}
              </div>
            </div>
          </div>

          <div className="card">
            <h2>区分別 予算 vs 実績</h2>
            <PlannedVsActualChart data={byCategory} />
          </div>

          <div className="card">
            <h2>案件・費目別 コスト差異</h2>
            <VarianceBarChart data={costVarianceLines} />
          </div>

          {report.categories.map((c) => (
            <div className="card" key={c.category}>
              <h2>{categoryLabel[c.category]} 差異明細</h2>
              {c.lines.length === 0 ? (
                <p className="muted small">明細がありません。</p>
              ) : (
                <table>
                  <thead>
                    <tr>
                      <th>{c.category === 'PeriodCost' ? '費目' : '案件'}</th>
                      <th className="num">予算金額</th>
                      <th className="num">実績金額</th>
                      <th className="num">差異</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {c.lines.map((l) => (
                      <tr key={`${l.category}-${l.projectId ?? ''}-${l.elementCode ?? ''}`}>
                        <td>{lineName(l)}</td>
                        <td className="num">¥{formatYen(l.plannedAmount)}</td>
                        <td className="num">¥{formatYen(l.actualAmount)}</td>
                        <td
                          className={`num ${l.isAdverse ? 'adverse' : l.isFavorable ? 'favorable' : ''}`}
                        >
                          ¥{formatSignedYen(l.variance)}
                        </td>
                        <td className="small muted">{l.isUnplanned ? '予定外' : ''}</td>
                      </tr>
                    ))}
                    <tr className="total-row">
                      <td>合計</td>
                      <td className="num">¥{formatYen(c.plannedAmount)}</td>
                      <td className="num">¥{formatYen(c.actualAmount)}</td>
                      <td
                        className={`num ${
                          c.category === 'Revenue'
                            ? c.variance >= 0
                              ? 'favorable'
                              : 'adverse'
                            : c.variance > 0
                              ? 'adverse'
                              : 'favorable'
                        }`}
                      >
                        ¥{formatSignedYen(c.variance)}
                      </td>
                      <td></td>
                    </tr>
                  </tbody>
                </table>
              )}
            </div>
          ))}
        </>
      )}
    </>
  )
}

function ProfitTab({ departmentId, half }: { departmentId: string; half: string }) {
  const [summary, setSummary] = useState<ProfitSummary | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    setError(null)
    api
      .getProfit(departmentId, half)
      .then(setSummary)
      .catch((e: Error) => {
        setSummary(null)
        setError(e.message)
      })
  }, [departmentId, half])
  useEffect(load, [load])

  const profitVarianceByProject = useMemo(
    () =>
      summary?.projectLines.map((l) => ({
        label: l.projectName,
        value: l.profitVariance,
      })) ?? [],
    [summary],
  )

  const pct = (v: number | null) => (v === null ? '—' : `${(v * 100).toFixed(1)}%`)

  return (
    <>
      <Toast message={error} onClose={() => setError(null)} />

      {summary && (
        <>
          <div className="stat-row">
            <div className="stat-tile">
              <div className="label">
                損益予算(v{summary.budgetVersion} {summary.budgetLabel})
              </div>
              <div className="value">¥{formatYen(summary.plannedProfit)}</div>
              <div className="label">利益率 {pct(summary.plannedMarginRate)}</div>
            </div>
            <div className="stat-tile">
              <div className="label">損益実績</div>
              <div className="value">¥{formatYen(summary.actualProfit)}</div>
              <div className="label">利益率 {pct(summary.actualMarginRate)}</div>
            </div>
            <div className="stat-tile">
              <div className="label">損益差異(実績 − 予算)</div>
              <div className={`value ${summary.profitVariance >= 0 ? 'favorable' : 'adverse'}`}>
                ¥{formatSignedYen(summary.profitVariance)}
              </div>
            </div>
          </div>

          <div className="card">
            <h2>案件別 損益</h2>
            <p className="muted small">
              案件損益 = 売上高 − 加工費 − 外注費。期間費用は課共通のため案件には配賦せず、
              「期間費用(課共通)」行として全体の損益にのみ反映します。
            </p>
            <table>
              <thead>
                <tr>
                  <th>案件</th>
                  <th className="num">売上予算</th>
                  <th className="num">売上実績</th>
                  <th className="num">コスト予算</th>
                  <th className="num">コスト実績</th>
                  <th className="num">損益予算</th>
                  <th className="num">損益実績</th>
                  <th className="num">損益差異</th>
                </tr>
              </thead>
              <tbody>
                {summary.projectLines.map((l) => (
                  <tr key={l.projectId}>
                    <td>
                      {l.projectName}
                      <span className="muted small">({l.projectCode})</span>
                    </td>
                    <td className="num">¥{formatYen(l.plannedRevenue)}</td>
                    <td className="num">¥{formatYen(l.actualRevenue)}</td>
                    <td className="num">
                      ¥{formatYen(l.plannedProcessing + l.plannedOutsourcing)}
                    </td>
                    <td className="num">¥{formatYen(l.actualProcessing + l.actualOutsourcing)}</td>
                    <td className="num">¥{formatYen(l.plannedProfit)}</td>
                    <td className="num">¥{formatYen(l.actualProfit)}</td>
                    <td className={`num ${l.profitVariance >= 0 ? 'favorable' : 'adverse'}`}>
                      ¥{formatSignedYen(l.profitVariance)}
                    </td>
                  </tr>
                ))}
                <tr>
                  <td className="muted">期間費用(課共通)</td>
                  <td className="num">—</td>
                  <td className="num">—</td>
                  <td className="num">¥{formatYen(summary.plannedPeriodCost)}</td>
                  <td className="num">¥{formatYen(summary.actualPeriodCost)}</td>
                  <td className="num">−¥{formatYen(summary.plannedPeriodCost)}</td>
                  <td className="num">−¥{formatYen(summary.actualPeriodCost)}</td>
                  <td className="num"></td>
                </tr>
                <tr className="total-row">
                  <td>合計(課全体)</td>
                  <td className="num">¥{formatYen(summary.plannedRevenue)}</td>
                  <td className="num">¥{formatYen(summary.actualRevenue)}</td>
                  <td className="num">¥{formatYen(summary.plannedTotalCost)}</td>
                  <td className="num">¥{formatYen(summary.actualTotalCost)}</td>
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
            <h2>案件別 損益差異</h2>
            <VarianceBarChart
              data={profitVarianceByProject}
              adverseWhenPositive={false}
              legendLabels={['有利差異(損益改善)', '不利差異(損益悪化)']}
            />
          </div>
        </>
      )}
    </>
  )
}

export default function VariancePage() {
  const { departmentId } = useParams<{ departmentId: string }>()
  const [searchParams] = useSearchParams()
  const half = searchParams.get('half') ?? currentFiscalHalf()
  const [tab, setTab] = useState<Tab>('variance')

  if (!departmentId) return null

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">課一覧</Link> /{' '}
        <Link to={`/departments/${departmentId}?half=${half}`}>課詳細</Link> / 予実差異分析・損益(
        {halfLabel(half)})
      </div>

      <div className="tab-row">
        <button className={tab === 'variance' ? 'active' : ''} onClick={() => setTab('variance')}>
          予実差異
        </button>
        <button className={tab === 'profit' ? 'active' : ''} onClick={() => setTab('profit')}>
          損益
        </button>
      </div>

      {tab === 'variance' && <VarianceTab departmentId={departmentId} half={half} />}
      {tab === 'profit' && <ProfitTab departmentId={departmentId} half={half} />}
    </>
  )
}
