// 介入の記録(/intervention <種類> <原因> [メモ])を読む。依存なし(.github/workflows/factory-intervention.yml からも使う)。

/** 介入の種類。人が手を出した理由。 */
export const KINDS = ['回答', '差し戻し', '手直し', 'CI承認'];
/** 介入が要った原因の場所。 */
export const CAUSES = ['ナレッジ', '要件', '仕様', '工場'];

export const USAGE = `/intervention <種類> <原因> [メモ]
- 種類: ${KINDS.join(' / ')}
- 原因: ${CAUSES.join(' / ')}
例: /intervention 差し戻し 仕様 端数の扱いが処理ルールに書いてなかった`;

/**
 * コメントの 1 行目が介入の記録なら読む。
 * @returns {null | {ok: true, kind: string, cause: string, note: string} | {ok: false, error: string}}
 *   null は介入の記録ではないコメント
 */
export function parseIntervention(body) {
  const first = String(body ?? '').trimStart().split(/\r?\n/)[0].trim();
  if (!/^\/intervention(\s|$)/.test(first)) return null;
  const [, kind, cause, ...rest] = first.split(/\s+/);
  if (!KINDS.includes(kind)) return { ok: false, error: `種類「${kind ?? ''}」は ${KINDS.join(' / ')} のどれか` };
  if (!CAUSES.includes(cause)) return { ok: false, error: `原因「${cause ?? ''}」は ${CAUSES.join(' / ')} のどれか` };
  return { ok: true, kind, cause, note: rest.join(' ') };
}
