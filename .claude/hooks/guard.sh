#!/usr/bin/env bash
# 工場のゲートを破る操作を止める PreToolUse フック(ADR-0015)。
# 入力: Claude Code が渡すツール呼び出しの JSON(標準入力)。
# 止めるときは理由を標準エラーに出して終了コード 2 を返す(Claude Code は呼び出しを取り消し、理由をエージェントに返す)。
# 判定の正本は tools/factory/guard/rules.sh(Codex・Copilot でも効く gh・git の包みと共有する)。
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../tools/factory/guard/rules.sh
. "$here/../../tools/factory/guard/rules.sh"

input="$(cat)"
tool="$(jq -r '.tool_name // ""' <<<"$input")"

case "$tool" in
  Bash)
    cmd="$(jq -r '.tool_input.command // ""' <<<"$input")"
    if ! reason="$(factory_guard_command "$cmd")"; then
      factory_guard_explain "$reason"
      exit 2
    fi
    ;;
  Edit | Write | MultiEdit | NotebookEdit)
    path="$(jq -r '.tool_input.file_path // .tool_input.notebook_path // ""' <<<"$input")"
    if ! reason="$(factory_guard_path "$path")"; then
      factory_guard_explain "$reason"
      exit 2
    fi
    ;;
esac
exit 0
