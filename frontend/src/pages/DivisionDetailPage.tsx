import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import {
  api,
  categoryLabel,
  currentFiscalHalf,
  fiscalHalfOptions,
  formatPercent,
  formatSignedYen,
  formatYen,
  halfLabel,
  marginRate,
  type BudgetCategory,
  type DepartmentSummaryLine,
  type Division,
  type DivisionBudgetSummary,
} from '../api'

type Tab = 'plan' | 'actual'

// 区分別の差異の色: 売上高は正が有利(緑)、コスト系は正が不利(赤)。
const categoryVarianceClass = (category: BudgetCategory, variance: number) => {
  if (variance === 0) return ''
  const favorable = category === 'Revenue' ? variance > 0 : variance < 0
  return favorable ? 'favorable' : 'adverse'
}

/** 課の区分別予算(未策定なら0)。 */
const plannedByCategory = (line: DepartmentSummaryLine, category: BudgetCategory): number =>
  line.categories.find((c) => c.category === category)?.plannedAmount ?? 0

/** 予算(計画)タブ: 予算のみの区分別内訳・計画損益・粗利率と、課ごとの予算比較グリッド。 */
function PlanView({ summary, half }: { summary: DivisionBudgetSummary; half: string }) {
  const plannedByCat = (category: BudgetCategory) =>
    summary.categories.find((c) => c.category === category)?.plannedAmount ?? 0
  const approvedDepartments = summary.departmentLines.filter((d) => d.hasApprovedBudget)

  return (
    <>
      <div className="stat-row">
        <div className="stat-tile">
          <div className="label">売上高予算</div>
          <div className="value">¥{formatYen(plannedByCat('Revenue'))}</div>
        </div>
        <div className="stat-tile">
          <div className="label">加工費予算</div>
          <div className="value">¥{formatYen(plannedByCat('Processing'))}</div>
        </div>
        <div className="stat-tile">
          <div className="label">外注費予算</div>
          <div className="value">¥{formatYen(plannedByCat('Outsourcing'))}</div>
        </div>
        <div className="stat-tile">
          <div className="label">期間費用予算</div>
          <div className="value">¥{formatYen(plannedByCat('PeriodCost'))}</div>
        </div>
        <div className="stat-tile">
          <div className="label">計画損益</div>
          <div className={`value ${summary.plannedProfit >= 0 ? 'favorable' : 'adverse'}`}>
            ¥{formatYen(summary.plannedProfit)}
          </div>
          <div className="label">
            粗利率 {formatPercent(marginRate(summary.plannedProfit, summary.plannedRevenue))}
          </div>
        </div>
      </div>

      <div className="card">
        <h2>課ごとの予算比較</h2>
        <p className="muted small">
          課ごとの計画(予算のみ)を並べて比較します。計画損益・粗利率で色分けし、
          損益が弱い課を把握できます(予算未策定の課は「未策定」表示)。
        </p>
        {summary.departmentLines.length === 0 ? (
          <p className="muted small">課がありません。下のフォームから課を登録してください。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>課</th>
                <th className="num">売上高</th>
                <th className="num">加工費</th>
                <th className="num">外注費</th>
                <th className="num">期間費用</th>
                <th className="num">計画損益</th>
                <th className="num">粗利率</th>
              </tr>
            </thead>
            <tbody>
              {summary.departmentLines.map((d) => {
                const margin = marginRate(d.plannedProfit, d.plannedRevenue)
                const profitClass = d.plannedProfit >= 0 ? 'favorable' : 'adverse'
                return (
                  <tr key={d.departmentId}>
                    <td>
                      <Link to={`/departments/${d.departmentId}?half=${half}`}>
                        {d.departmentName}
                      </Link>
                      <span className="muted small">({d.departmentCode})</span>
                      {!d.hasApprovedBudget && (
                        <span className="badge draft" style={{ marginLeft: 6 }}>
                          未策定
                        </span>
                      )}
                    </td>
                    {d.hasApprovedBudget ? (
                      <>
                        <td className="num">¥{formatYen(plannedByCategory(d, 'Revenue'))}</td>
                        <td className="num">¥{formatYen(plannedByCategory(d, 'Processing'))}</td>
                        <td className="num">¥{formatYen(plannedByCategory(d, 'Outsourcing'))}</td>
                        <td className="num">¥{formatYen(plannedByCategory(d, 'PeriodCost'))}</td>
                        <td className={`num ${profitClass}`}>¥{formatYen(d.plannedProfit)}</td>
                        <td className={`num ${profitClass}`}>{formatPercent(margin)}</td>
                      </>
                    ) : (
                      <td className="num muted" colSpan={6}>
                        —
                      </td>
                    )}
                  </tr>
                )
              })}
              <tr className="total-row">
                <td>部合計</td>
                <td className="num">¥{formatYen(plannedByCat('Revenue'))}</td>
                <td className="num">¥{formatYen(plannedByCat('Processing'))}</td>
                <td className="num">¥{formatYen(plannedByCat('Outsourcing'))}</td>
                <td className="num">¥{formatYen(plannedByCat('PeriodCost'))}</td>
                <td className={`num ${summary.plannedProfit >= 0 ? 'favorable' : 'adverse'}`}>
                  ¥{formatYen(summary.plannedProfit)}
                </td>
                <td className={`num ${summary.plannedProfit >= 0 ? 'favorable' : 'adverse'}`}>
                  {formatPercent(
                    marginRate(
                      approvedDepartments.reduce((s, d) => s + d.plannedProfit, 0),
                      approvedDepartments.reduce((s, d) => s + d.plannedRevenue, 0),
                    ),
                  )}
                </td>
              </tr>
            </tbody>
          </table>
        )}
      </div>
    </>
  )
}

/** 予実サマリタブ: 予算と実績を突き合わせた差異・損益。 */
function ActualSummaryView({
  summary,
  half,
}: {
  summary: DivisionBudgetSummary
  half: string
}) {
  return (
    <>
      <div className="stat-row">
        <div className="stat-tile">
          <div className="label">売上高(予算 / 実績)</div>
          <div className="value">¥{formatYen(summary.actualRevenue)}</div>
          <div className="label">予算 ¥{formatYen(summary.plannedRevenue)}</div>
        </div>
        <div className="stat-tile">
          <div className="label">総コスト(予算 / 実績)</div>
          <div className="value">¥{formatYen(summary.actualCost)}</div>
          <div className="label">予算 ¥{formatYen(summary.plannedCost)}</div>
        </div>
        <div className="stat-tile">
          <div className="label">損益予算</div>
          <div className={`value ${summary.plannedProfit >= 0 ? 'favorable' : 'adverse'}`}>
            ¥{formatYen(summary.plannedProfit)}
          </div>
        </div>
        <div className="stat-tile">
          <div className="label">損益実績</div>
          <div className={`value ${summary.actualProfit >= 0 ? 'favorable' : 'adverse'}`}>
            ¥{formatYen(summary.actualProfit)}
          </div>
        </div>
        <div className="stat-tile">
          <div className="label">損益差異(実績 − 予算)</div>
          <div className={`value ${summary.profitVariance >= 0 ? 'favorable' : 'adverse'}`}>
            ¥{formatSignedYen(summary.profitVariance)}
          </div>
        </div>
      </div>

      <div className="card">
        <h2>区分別の予実(部合計)</h2>
        <table>
          <thead>
            <tr>
              <th>区分</th>
              <th className="num">予算</th>
              <th className="num">実績</th>
              <th className="num">差異</th>
            </tr>
          </thead>
          <tbody>
            {summary.categories.map((c) => (
              <tr key={c.category}>
                <td>{categoryLabel[c.category]}</td>
                <td className="num">¥{formatYen(c.plannedAmount)}</td>
                <td className="num">¥{formatYen(c.actualAmount)}</td>
                <td className={`num ${categoryVarianceClass(c.category, c.variance)}`}>
                  ¥{formatSignedYen(c.variance)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card">
        <h2>課別の内訳</h2>
        {summary.departmentLines.length === 0 ? (
          <p className="muted small">課がありません。下のフォームから課を登録してください。</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>課</th>
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
              {summary.departmentLines.map((d) => (
                <tr key={d.departmentId}>
                  <td>
                    <Link to={`/departments/${d.departmentId}?half=${half}`}>
                      {d.departmentName}
                    </Link>
                    <span className="muted small">({d.departmentCode})</span>
                    {!d.hasApprovedBudget && (
                      <span className="badge draft" style={{ marginLeft: 6 }}>
                        未策定
                      </span>
                    )}
                  </td>
                  <td className="num">¥{formatYen(d.plannedRevenue)}</td>
                  <td className="num">¥{formatYen(d.actualRevenue)}</td>
                  <td className="num">¥{formatYen(d.plannedCost)}</td>
                  <td className="num">¥{formatYen(d.actualCost)}</td>
                  <td className="num">¥{formatYen(d.plannedProfit)}</td>
                  <td className="num">¥{formatYen(d.actualProfit)}</td>
                  <td className={`num ${d.profitVariance >= 0 ? 'favorable' : 'adverse'}`}>
                    ¥{formatSignedYen(d.profitVariance)}
                  </td>
                </tr>
              ))}
              <tr className="total-row">
                <td>部合計</td>
                <td className="num">¥{formatYen(summary.plannedRevenue)}</td>
                <td className="num">¥{formatYen(summary.actualRevenue)}</td>
                <td className="num">¥{formatYen(summary.plannedCost)}</td>
                <td className="num">¥{formatYen(summary.actualCost)}</td>
                <td className="num">¥{formatYen(summary.plannedProfit)}</td>
                <td className="num">¥{formatYen(summary.actualProfit)}</td>
                <td className={`num ${summary.profitVariance >= 0 ? 'favorable' : 'adverse'}`}>
                  ¥{formatSignedYen(summary.profitVariance)}
                </td>
              </tr>
            </tbody>
          </table>
        )}
      </div>
    </>
  )
}

export default function DivisionDetailPage() {
  const { divisionId } = useParams<{ divisionId: string }>()
  const [searchParams, setSearchParams] = useSearchParams()
  const half = searchParams.get('half') ?? currentFiscalHalf()
  const setHalf = (value: string) => setSearchParams({ half: value })

  const [division, setDivision] = useState<Division | null>(null)
  const [summary, setSummary] = useState<DivisionBudgetSummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('plan')

  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [saving, setSaving] = useState(false)

  const load = useCallback(() => {
    if (!divisionId) return
    api.getDivision(divisionId).then(setDivision).catch((e: Error) => setError(e.message))
    api
      .getDivisionBudgetSummary(divisionId, half)
      .then(setSummary)
      .catch((e: Error) => setError(e.message))
  }, [divisionId, half])
  useEffect(load, [load])

  if (!divisionId) return null

  const addDepartment = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await api.createDepartment(divisionId, { code, name })
      setCode('')
      setName('')
      load()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const approve = async () => {
    setError(null)
    try {
      await api.approveDivisionBudget(divisionId, half)
      load()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const revoke = async () => {
    setError(null)
    try {
      await api.revokeDivisionBudget(divisionId, half)
      load()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">部一覧</Link> / {division?.name ?? '…'}
      </div>
      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>
          {division?.code} {division?.name}
        </h2>
        <div className="form-row">
          <label>
            対象半期
            <select value={half} onChange={(e) => setHalf(e.target.value)}>
              {fiscalHalfOptions().map((h) => (
                <option key={h} value={h}>
                  {halfLabel(h)}
                </option>
              ))}
            </select>
          </label>
        </div>
        <p className="muted small">
          配下の課の承認済み予算を集計しています(予算未策定の課は合計に含まれません)。
        </p>
      </div>

      {summary && (
        <>
          <div className="card">
            <h2>
              部予算の承認
              <span
                className={`badge ${summary.isApproved ? 'approved' : 'draft'}`}
                style={{ marginLeft: 12 }}
              >
                {summary.isApproved ? '承認済' : '未承認'}
              </span>
            </h2>
            {summary.isApproved ? (
              <>
                <p className="muted small">
                  {summary.approvedAt
                    ? `${new Date(summary.approvedAt).toLocaleString('ja-JP')} に承認`
                    : ''}
                  。課の承認・改定は部承認とは独立しています(改定しても部承認は残ります)。
                </p>
                <button onClick={revoke}>部承認を取り消す</button>
              </>
            ) : (
              <>
                <p className="muted small">
                  配下課の予算がすべて承認されると、部として承認できます。
                </p>
                <button className="primary" onClick={approve} disabled={!summary.canApprove}>
                  部予算を承認する
                </button>
                {!summary.canApprove && (
                  <span className="muted small" style={{ marginLeft: 8 }}>
                    未承認の課があります。
                  </span>
                )}
              </>
            )}
          </div>

          <div className="tab-row">
            <button className={tab === 'plan' ? 'active' : ''} onClick={() => setTab('plan')}>
              予算(計画)
            </button>
            <button className={tab === 'actual' ? 'active' : ''} onClick={() => setTab('actual')}>
              予実サマリ
            </button>
          </div>

          {tab === 'plan' && <PlanView summary={summary} half={half} />}
          {tab === 'actual' && <ActualSummaryView summary={summary} half={half} />}
        </>
      )}

      <div className="card">
        <h2>課の登録</h2>
        <p className="muted small">課はこの部に属します。予算・実績は各課の画面で管理します。</p>
        <form onSubmit={addDepartment} className="form-row">
          <label>
            課コード
            <input value={code} onChange={(e) => setCode(e.target.value)} required placeholder="DEV-1" />
          </label>
          <label>
            課名
            <input value={name} onChange={(e) => setName(e.target.value)} required placeholder="開発1課" />
          </label>
          <button type="submit" className="primary" disabled={saving}>
            登録
          </button>
        </form>
      </div>
    </>
  )
}
