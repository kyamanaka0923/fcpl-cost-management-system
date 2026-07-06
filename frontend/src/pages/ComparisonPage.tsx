import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, formatSignedYen, formatYen, type CostElement } from '../api'

type PlanKind = 'cost' | 'revenue'

interface PlanOption {
  id: string
  version: number
  label: string
}

interface ComparisonView {
  baseVersion: number
  baseLabel: string
  targetVersion: number
  targetLabel: string
  lines: {
    key: string
    period: string
    name: string
    baseAmount: number
    targetAmount: number
    difference: number
  }[]
  baseTotalAmount: number
  targetTotalAmount: number
  totalDifference: number
}

export default function ComparisonPage() {
  const { projectId } = useParams<{ projectId: string }>()
  const [kind, setKind] = useState<PlanKind>('cost')
  const [plans, setPlans] = useState<PlanOption[]>([])
  const [elements, setElements] = useState<CostElement[]>([])
  const [comparison, setComparison] = useState<ComparisonView | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [baseVersion, setBaseVersion] = useState<number | ''>('')
  const [targetVersion, setTargetVersion] = useState<number | ''>('')

  useEffect(() => {
    api.listCostElements().then(setElements).catch(() => undefined)
  }, [])

  useEffect(() => {
    if (!projectId) return
    setComparison(null)
    setError(null)
    const list =
      kind === 'cost' ? api.listPlans(projectId) : api.listRevenuePlans(projectId)
    list
      .then((ps) => {
        setPlans(ps.map((p) => ({ id: p.id, version: p.version, label: p.label })))
        if (ps.length >= 2) {
          const versions = ps.map((p) => p.version).sort((a, b) => a - b)
          setBaseVersion(versions[0])
          setTargetVersion(versions[versions.length - 1])
        } else {
          setBaseVersion('')
          setTargetVersion('')
        }
      })
      .catch((e: Error) => setError(e.message))
  }, [projectId, kind])

  const elementName = useCallback(
    (code: string) => elements.find((e) => e.code === code)?.name ?? code,
    [elements],
  )

  const load = useCallback(() => {
    if (!projectId || baseVersion === '' || targetVersion === '') return
    setError(null)
    const promise =
      kind === 'cost'
        ? api.comparePlans(projectId, baseVersion, targetVersion).then(
            (c): ComparisonView => ({
              ...c,
              lines: c.lines.map((l) => ({
                key: `${l.period}-${l.elementCode}`,
                period: l.period,
                name: elementName(l.elementCode),
                baseAmount: l.baseAmount,
                targetAmount: l.targetAmount,
                difference: l.difference,
              })),
            }),
          )
        : api.compareRevenuePlans(projectId, baseVersion, targetVersion).then(
            (c): ComparisonView => ({
              ...c,
              lines: c.lines.map((l) => ({
                key: `${l.period}-${l.itemName}`,
                period: l.period,
                name: l.itemName,
                baseAmount: l.baseAmount,
                targetAmount: l.targetAmount,
                difference: l.difference,
              })),
            }),
          )
    promise.then(setComparison).catch((e: Error) => {
      setComparison(null)
      setError(e.message)
    })
  }, [projectId, kind, baseVersion, targetVersion, elementName])
  useEffect(load, [load])

  if (!projectId) return null

  return (
    <>
      <div className="breadcrumbs">
        <Link to="/">プロジェクト一覧</Link> /{' '}
        <Link to={`/projects/${projectId}`}>プロジェクト</Link> / 予算バージョン比較
      </div>
      {error && <div className="error-banner">{error}</div>}

      <div className="tab-row">
        <button className={kind === 'cost' ? 'active' : ''} onClick={() => setKind('cost')}>
          原価予算
        </button>
        <button className={kind === 'revenue' ? 'active' : ''} onClick={() => setKind('revenue')}>
          売上予算
        </button>
      </div>

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
              <div className="value">¥{formatSignedYen(comparison.totalDifference)}</div>
            </div>
          </div>

          <div className="card">
            <h2>明細比較</h2>
            <table>
              <thead>
                <tr>
                  <th>年月</th>
                  <th>{kind === 'cost' ? '費目' : '品目'}</th>
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
                  <tr key={l.key}>
                    <td>{l.period}</td>
                    <td>{l.name}</td>
                    <td className="num">¥{formatYen(l.baseAmount)}</td>
                    <td className="num">¥{formatYen(l.targetAmount)}</td>
                    <td className={`num ${l.difference !== 0 ? 'muted' : ''}`}>
                      <strong>¥{formatSignedYen(l.difference)}</strong>
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
