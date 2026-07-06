import { useState, type ReactNode } from 'react'
import { formatSignedYen, formatYen } from '../api'

// dataviz スキル準拠: 細いマーク、データ端 4px 角丸、隣接バー間 2px、
// ホバーツールチップ、凡例、グリッドは控えめ、文言はインクトークン。

interface TooltipState {
  x: number
  y: number
  content: ReactNode
}

function useTooltip() {
  const [tooltip, setTooltip] = useState<TooltipState | null>(null)
  const show = (e: { clientX: number; clientY: number }, content: ReactNode) =>
    setTooltip({ x: e.clientX + 12, y: e.clientY + 12, content })
  const hide = () => setTooltip(null)
  const node = tooltip ? (
    <div className="chart-tooltip" style={{ left: tooltip.x, top: tooltip.y }}>
      {tooltip.content}
    </div>
  ) : null
  return { show, hide, node }
}

function niceMax(value: number): number {
  if (value <= 0) return 1
  const exp = Math.pow(10, Math.floor(Math.log10(value)))
  const f = value / exp
  const nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10
  return nice * exp
}

/** 上端のみ 4px 角丸の縦バーのパス(データ端が丸く、基線側は直角)。 */
function barPath(x: number, y: number, w: number, h: number, up: boolean): string {
  const r = Math.min(4, w / 2, h)
  if (h <= 0) return ''
  if (up) {
    // y が上端(値側)、基線は y + h
    return `M${x},${y + h} L${x},${y + r} Q${x},${y} ${x + r},${y} L${x + w - r},${y} Q${x + w},${y} ${x + w},${y + r} L${x + w},${y + h} Z`
  }
  // 下向き(基線が y、データ端が y + h)
  return `M${x},${y} L${x + w},${y} L${x + w},${y + h - r} Q${x + w},${y + h} ${x + w - r},${y + h} L${x + r},${y + h} Q${x},${y + h} ${x},${y + h - r} Z`
}

export interface GroupedBarDatum {
  label: string
  planned: number
  actual: number
}

/** 予算 vs 実績のグループ棒グラフ。負値(損失など)は基線の下に描画する。 */
export function PlannedVsActualChart({
  data,
  seriesLabels = ['予算', '実績'],
}: {
  data: GroupedBarDatum[]
  seriesLabels?: [string, string]
}) {
  const tooltip = useTooltip()
  if (data.length === 0) return <p className="muted small">表示するデータがありません。</p>

  const width = 720
  const height = 260
  const margin = { top: 12, right: 8, bottom: 28, left: 64 }
  const plotW = width - margin.left - margin.right
  const plotH = height - margin.top - margin.bottom

  const rawMax = Math.max(...data.map((d) => Math.max(d.planned, d.actual, 0)))
  const rawMin = Math.min(...data.map((d) => Math.min(d.planned, d.actual, 0)))
  const max = rawMax > 0 ? niceMax(rawMax) : 0
  const min = rawMin < 0 ? -niceMax(-rawMin) : 0
  const span = max - min || 1
  const yScale = (v: number) => plotH - ((v - min) / span) * plotH
  const groupW = plotW / data.length
  const barW = Math.min(28, (groupW - 12) / 2 - 1)
  const ticks = [...new Set([0, 0.25, 0.5, 0.75, 1].map((t) => min + t * span).concat([0]))]

  return (
    <div>
      <div className="chart-legend">
        <span className="item">
          <span className="swatch" style={{ background: 'var(--series-1)' }} />
          {seriesLabels[0]}
        </span>
        <span className="item">
          <span className="swatch" style={{ background: 'var(--series-2)' }} />
          {seriesLabels[1]}
        </span>
      </div>
      <svg
        viewBox={`0 0 ${width} ${height}`}
        style={{ width: '100%', height: 'auto' }}
        role="img"
        aria-label="予算と実績の比較グラフ"
      >
        <g transform={`translate(${margin.left},${margin.top})`}>
          {ticks.map((t) => (
            <g key={t}>
              <line
                x1={0}
                x2={plotW}
                y1={yScale(t)}
                y2={yScale(t)}
                stroke={t === 0 ? 'var(--baseline)' : 'var(--gridline)'}
                strokeWidth={1}
              />
              <text
                x={-8}
                y={yScale(t)}
                textAnchor="end"
                dominantBaseline="middle"
                fontSize={11}
                fill="var(--text-muted)"
              >
                {formatYen(t)}
              </text>
            </g>
          ))}
          {data.map((d, i) => {
            const cx = i * groupW + groupW / 2
            // 隣接バー間は 2px のサーフェス空白
            const xPlanned = cx - barW - 1
            const xActual = cx + 1
            const show = (e: React.MouseEvent) =>
              tooltip.show(e, (
                <>
                  <div className="tt-title">{d.label}</div>
                  <div className="tt-line">
                    <span>{seriesLabels[0]}</span>
                    <span className="v">¥{formatYen(d.planned)}</span>
                  </div>
                  <div className="tt-line">
                    <span>{seriesLabels[1]}</span>
                    <span className="v">¥{formatYen(d.actual)}</span>
                  </div>
                  <div className="tt-line">
                    <span>差異</span>
                    <span className="v">¥{formatSignedYen(d.actual - d.planned)}</span>
                  </div>
                </>
              ))
            const zeroY = yScale(0)
            const bar = (x: number, v: number, color: string) => {
              const vy = yScale(v)
              return v >= 0 ? (
                <path d={barPath(x, vy, barW, zeroY - vy, true)} fill={color} />
              ) : (
                <path d={barPath(x, zeroY, barW, vy - zeroY, false)} fill={color} />
              )
            }
            return (
              <g key={d.label} onMouseMove={show} onMouseLeave={tooltip.hide}>
                {/* ヒットターゲットはマークより大きく */}
                <rect x={i * groupW} y={0} width={groupW} height={plotH} fill="transparent" />
                {bar(xPlanned, d.planned, 'var(--series-1)')}
                {bar(xActual, d.actual, 'var(--series-2)')}
                <text
                  x={cx}
                  y={plotH + 18}
                  textAnchor="middle"
                  fontSize={11}
                  fill="var(--text-secondary)"
                >
                  {d.label}
                </text>
              </g>
            )
          })}
        </g>
      </svg>
      {tooltip.node}
    </div>
  )
}

export interface DivergingBarDatum {
  label: string
  value: number
  detail?: ReactNode
}

/**
 * 差異の分岐棒グラフ。
 * 原価(既定): 正 = 不利差異 = 赤、負 = 有利差異 = 青。
 * 売上(adverseWhenPositive=false): 正 = 有利差異 = 青、負 = 不利差異 = 赤。
 */
export function VarianceBarChart({
  data,
  adverseWhenPositive = true,
  legendLabels = ['不利差異(予算超過)', '有利差異(予算内)'],
}: {
  data: DivergingBarDatum[]
  adverseWhenPositive?: boolean
  legendLabels?: [string, string]
}) {
  const tooltip = useTooltip()
  if (data.length === 0) return <p className="muted small">表示するデータがありません。</p>

  const positiveColor = adverseWhenPositive ? 'var(--diverge-adverse)' : 'var(--diverge-favorable)'
  const negativeColor = adverseWhenPositive ? 'var(--diverge-favorable)' : 'var(--diverge-adverse)'

  const width = 720
  const rowH = 30
  const margin = { top: 8, right: 72, bottom: 8, left: 120 }
  const height = margin.top + margin.bottom + rowH * data.length
  const plotW = width - margin.left - margin.right

  const maxAbs = niceMax(Math.max(...data.map((d) => Math.abs(d.value)), 1))
  const zeroX = plotW / 2
  // 値ラベル分の余白をバーの外側に確保する
  const armW = plotW / 2 - 64
  const xScale = (v: number) => zeroX + (v / maxAbs) * armW

  return (
    <div>
      <div className="chart-legend">
        <span className="item">
          <span className="swatch" style={{ background: positiveColor }} />
          {legendLabels[0]}
        </span>
        <span className="item">
          <span className="swatch" style={{ background: negativeColor }} />
          {legendLabels[1]}
        </span>
      </div>
      <svg
        viewBox={`0 0 ${width} ${height}`}
        style={{ width: '100%', height: 'auto' }}
        role="img"
        aria-label="費目別差異グラフ"
      >
        <g transform={`translate(${margin.left},${margin.top})`}>
          <line
            x1={zeroX}
            x2={zeroX}
            y1={0}
            y2={rowH * data.length}
            stroke="var(--baseline)"
            strokeWidth={1}
          />
          {data.map((d, i) => {
            const y = i * rowH + rowH / 2
            const barH = 14
            const x0 = Math.min(xScale(0), xScale(d.value))
            const w = Math.abs(xScale(d.value) - xScale(0))
            const show = (e: React.MouseEvent) =>
              tooltip.show(e, (
                <>
                  <div className="tt-title">{d.label}</div>
                  <div className="tt-line">
                    <span>差異</span>
                    <span className="v">¥{formatSignedYen(d.value)}</span>
                  </div>
                  {d.detail}
                </>
              ))
            return (
              <g key={d.label} onMouseMove={show} onMouseLeave={tooltip.hide}>
                <rect x={-margin.left} y={i * rowH} width={width} height={rowH} fill="transparent" />
                <text
                  x={-10}
                  y={y}
                  textAnchor="end"
                  dominantBaseline="middle"
                  fontSize={11}
                  fill="var(--text-secondary)"
                >
                  {d.label}
                </text>
                {w > 0 && (
                  <rect
                    x={x0}
                    y={y - barH / 2}
                    width={w}
                    height={barH}
                    rx={4}
                    fill={d.value > 0 ? positiveColor : negativeColor}
                  />
                )}
                <text
                  x={d.value >= 0 ? xScale(d.value) + 6 : xScale(d.value) - 6}
                  y={y}
                  textAnchor={d.value >= 0 ? 'start' : 'end'}
                  dominantBaseline="middle"
                  fontSize={11}
                  fill="var(--text-secondary)"
                >
                  {formatSignedYen(d.value)}
                </text>
              </g>
            )
          })}
        </g>
      </svg>
      {tooltip.node}
    </div>
  )
}
