import { useCallback, useEffect, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import {
  api,
  categoryLabel,
  currentFiscalHalf,
  formatSignedYen,
  formatYen,
  halfLabel,
  type BudgetComparison,
  type BudgetComparisonLine,
  type BudgetSummary,
} from '../api'

const lineName = (l: BudgetComparisonLine): string =>
  l.category === 'PeriodCost'
    ? (l.elementName ?? l.elementCode ?? '')
    : (l.projectName ?? l.projectId ?? '')

export default function ComparisonPage() {
  const { departmentId } = useParams<{ departmentId: string }>()
  const [searchParams] = useSearchParams()
  const half = searchParams.get('half') ?? currentFiscalHalf()

  const [budgets, setBudgets] = useState<BudgetSummary[]>([])
  const [report, setReport] = useState<BudgetComparison | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [baseVersion, setBaseVersion] = useState<number | null>(null)
  const [targetVersion, setTargetVersion] = useState<number | null>(null)

  useEffect(() => {
    if (!departmentId) return
    api
      .listBudgets(departmentId, half)
      .then((list) => {
        setBudgets(list)
        // 既定は 最旧バージョン vs 最新バージョン
        if (list.length >= 2) {
          setBaseVersion(Math.min(...list.map((b) => b.version)))
          setTargetVersion(Math.max(...list.map((b) => b.version)))
        }
      })
      .catch((e: Error) => setError(e.message))
  }, [departmentId, half])

  const load = useCallback(() => {
    if (!departmentId || baseVersion === null || targetVersion === null) return
    setError(null)
    api
      .compareBudgets(departmentId, half, baseVersion, targetVersion)
      .then(setReport)
      .catch((e: Error) => {
        setReport(null)
        setError(e.message)
      })
  }, [departmentId, half, baseVersion, targetVersion])
  useEffect(load, [load])

  if (!departmentId) return null

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">課一覧</Link> /{' '}
        <Link to={`/departments/${departmentId}?half=${half}`}>課詳細</Link> / 予算バージョン比較(
        {halfLabel(half)})
      </div>
      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>比較条件</h2>
        {budgets.length < 2 ? (
          <p className="muted small">
            比較にはバージョンが2つ以上必要です。課詳細ページから改定版を作成してください。
          </p>
        ) : (
          <div className="form-row">
            <label>
              基準バージョン
              <select
                value={baseVersion ?? ''}
                onChange={(e) => setBaseVersion(Number(e.target.value))}
              >
                {budgets.map((b) => (
                  <option key={b.id} value={b.version}>
                    v{b.version} {b.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              比較バージョン
              <select
                value={targetVersion ?? ''}
                onChange={(e) => setTargetVersion(Number(e.target.value))}
              >
                {budgets.map((b) => (
                  <option key={b.id} value={b.version}>
                    v{b.version} {b.label}
                  </option>
                ))}
              </select>
            </label>
          </div>
        )}
      </div>

      {report && (
        <>
          <div className="stat-row">
            {report.categories.map((c) => (
              <div className="stat-tile" key={c.category}>
                <div className="label">{categoryLabel[c.category]} 増減</div>
                <div
                  className={`value ${c.difference === 0 ? '' : c.difference > 0 ? 'adverse' : 'favorable'}`}
                >
                  ¥{formatSignedYen(c.difference)}
                </div>
              </div>
            ))}
          </div>

          {report.categories.map((c) => (
            <div className="card" key={c.category}>
              <h2>{categoryLabel[c.category]} の増減明細</h2>
              {c.lines.length === 0 ? (
                <p className="muted small">明細がありません。</p>
              ) : (
                <table>
                  <thead>
                    <tr>
                      <th>{c.category === 'PeriodCost' ? '費目' : '案件'}</th>
                      <th className="num">
                        v{report.baseVersion} {report.baseLabel}
                      </th>
                      <th className="num">
                        v{report.targetVersion} {report.targetLabel}
                      </th>
                      <th className="num">増減</th>
                    </tr>
                  </thead>
                  <tbody>
                    {c.lines.map((l) => (
                      <tr key={`${l.category}-${l.projectId ?? ''}-${l.elementCode ?? ''}`}>
                        <td>{lineName(l)}</td>
                        <td className="num">¥{formatYen(l.baseAmount)}</td>
                        <td className="num">¥{formatYen(l.targetAmount)}</td>
                        <td className={`num ${l.difference === 0 ? '' : l.difference > 0 ? 'adverse' : 'favorable'}`}>
                          ¥{formatSignedYen(l.difference)}
                        </td>
                      </tr>
                    ))}
                    <tr className="total-row">
                      <td>合計</td>
                      <td className="num">¥{formatYen(c.baseAmount)}</td>
                      <td className="num">¥{formatYen(c.targetAmount)}</td>
                      <td className={`num ${c.difference === 0 ? '' : c.difference > 0 ? 'adverse' : 'favorable'}`}>
                        ¥{formatSignedYen(c.difference)}
                      </td>
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
