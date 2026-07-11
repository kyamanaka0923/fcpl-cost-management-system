import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import {
  api,
  categoryLabel,
  currentFiscalHalf,
  fiscalHalfOptions,
  formatSignedYen,
  formatYen,
  halfLabel,
  type BudgetCategory,
  type Division,
  type DivisionBudgetSummary,
} from '../api'

export default function DivisionDetailPage() {
  const { divisionId } = useParams<{ divisionId: string }>()
  const [searchParams, setSearchParams] = useSearchParams()
  const half = searchParams.get('half') ?? currentFiscalHalf()
  const setHalf = (value: string) => setSearchParams({ half: value })

  const [division, setDivision] = useState<Division | null>(null)
  const [summary, setSummary] = useState<DivisionBudgetSummary | null>(null)
  const [error, setError] = useState<string | null>(null)

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

  // 区分別の差異の色: 売上高は正が有利(緑)、コスト系は正が不利(赤)。
  const categoryVarianceClass = (category: BudgetCategory, variance: number) => {
    if (variance === 0) return ''
    const favorable = category === 'Revenue' ? variance > 0 : variance < 0
    return favorable ? 'favorable' : 'adverse'
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
          配下の課の承認済み予算と実績を合計しています(予算未策定の課は合計に含まれません)。
        </p>
      </div>

      {summary && (
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
