import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  api,
  formatSignedYen,
  formatYen,
  type CostElement,
  type CostPlanSummary,
  type PlanComparison,
} from '../api'

export default function ComparisonPage() {
  const { projectId } = useParams<{ projectId: string }>()
  const [plans, setPlans] = useState<CostPlanSummary[]>([])
  const [elements, setElements] = useState<CostElement[]>([])
  const [comparison, setComparison] = useState<PlanComparison | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [baseVersion, setBaseVersion] = useState<number | ''>('')
  const [targetVersion, setTargetVersion] = useState<number | ''>('')

  useEffect(() => {
    if (!projectId) return
    api
      .listPlans(projectId)
      .then((ps) => {
        setPlans(ps)
        // 既定では最古 vs 最新を比較対象にする
        if (ps.length >= 2) {
          const versions = ps.map((p) => p.version).sort((a, b) => a - b)
          setBaseVersion(versions[0])
          setTargetVersion(versions[versions.length - 1])
        }
      })
      .catch((e: Error) => setError(e.message))
    api.listCostElements().then(setElements).catch(() => undefined)
  }, [projectId])

  const load = useCallback(() => {
    if (!projectId || baseVersion === '' || targetVersion === '') return
    setError(null)
    api
      .comparePlans(projectId, baseVersion, targetVersion)
      .then(setComparison)
      .catch((e: Error) => {
        setComparison(null)
        setError(e.message)
      })
  }, [projectId, baseVersion, targetVersion])
  useEffect(load, [load])

  if (!projectId) return null
  const elementName = (code: string) => elements.find((e) => e.code === code)?.name ?? code

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">プロジェクト一覧</Link> /{' '}
        <Link to={`/projects/${projectId}`}>プロジェクト</Link> / 予算バージョン比較
      </div>
      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>比較対象</h2>
        {plans.length < 2 ? (
          <p className="muted small">比較には2つ以上の予算バージョンが必要です。</p>
        ) : (
          <div className="form-row">
            <label>
              基準バージョン
              <select
                value={baseVersion}
                onChange={(e) => setBaseVersion(Number(e.target.value))}
              >
                {plans.map((p) => (
                  <option key={p.id} value={p.version}>
                    v{p.version} {p.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              比較バージョン
              <select
                value={targetVersion}
                onChange={(e) => setTargetVersion(Number(e.target.value))}
              >
                {plans.map((p) => (
                  <option key={p.id} value={p.version}>
                    v{p.version} {p.label}
                  </option>
                ))}
              </select>
            </label>
          </div>
        )}
      </div>

      {comparison && (
        <>
          <div className="stat-row">
            <div className="stat-tile">
              <div className="label">
                v{comparison.baseVersion} {comparison.baseLabel}
              </div>
              <div className="value">¥{formatYen(comparison.baseTotalAmount)}</div>
            </div>
            <div className="stat-tile">
              <div className="label">
                v{comparison.targetVersion} {comparison.targetLabel}
              </div>
              <div className="value">¥{formatYen(comparison.targetTotalAmount)}</div>
            </div>
            <div className="stat-tile">
              <div className="label">増減</div>
              <div
                className={`value ${
                  comparison.totalDifference > 0
                    ? 'adverse'
                    : comparison.totalDifference < 0
                      ? 'favorable'
                      : ''
                }`}
              >
                ¥{formatSignedYen(comparison.totalDifference)}
              </div>
            </div>
          </div>

          <div className="card">
            <h2>明細比較</h2>
            <table>
              <thead>
                <tr>
                  <th>年月</th>
                  <th>費目</th>
                  <th className="num">
                    v{comparison.baseVersion} {comparison.baseLabel}
                  </th>
                  <th className="num">
                    v{comparison.targetVersion} {comparison.targetLabel}
                  </th>
                  <th className="num">増減</th>
                </tr>
              </thead>
              <tbody>
                {comparison.lines.map((l) => (
                  <tr key={`${l.period}-${l.elementCode}`}>
                    <td>{l.period}</td>
                    <td>{elementName(l.elementCode)}</td>
                    <td className="num">¥{formatYen(l.baseAmount)}</td>
                    <td className="num">¥{formatYen(l.targetAmount)}</td>
                    <td className={`num ${l.difference > 0 ? 'adverse' : l.difference < 0 ? 'favorable' : ''}`}>
                      ¥{formatSignedYen(l.difference)}
                    </td>
                  </tr>
                ))}
                <tr className="total-row">
                  <td colSpan={2}>合計</td>
                  <td className="num">¥{formatYen(comparison.baseTotalAmount)}</td>
                  <td className="num">¥{formatYen(comparison.targetTotalAmount)}</td>
                  <td className="num">¥{formatSignedYen(comparison.totalDifference)}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </>
      )}
    </>
  )
}
