#!/usr/bin/env bash
# 工場のライン(takt の workflow Software-Factory)を、ゲートのガードを効かせて1回動かす。
# 使い方: tools/factory/takt.sh <Issue番号> [claude|codex|copilot] [takt に渡す引数…]
#   エージェントを省くと、環境変数 FACTORY_AGENT、それもなければ claude。
#   例: tools/factory/takt.sh 45
#       tools/factory/takt.sh 45 codex
#       FACTORY_AGENT=copilot tools/factory/takt.sh 45
# ガード: tools/factory/guard/bin(gh・git の包み)を PATH の先頭に置く。workflow の start step が、
# エージェントのシェルでも効いているかを確かめ、効いていなければ止める。
set -euo pipefail

usage() {
  sed -n '2,7p' "$0" | sed 's/^# \{0,1\}//' >&2
  exit 2
}

[ $# -ge 1 ] || usage
issue="${1#\#}"
shift
[[ "$issue" =~ ^[0-9]+$ ]] || usage

agent="${FACTORY_AGENT:-claude}"
if [ $# -ge 1 ] && [[ "$1" != -* ]]; then
  agent="$1"
  shift
fi
case "$agent" in
  claude | codex | copilot) ;;
  *) echo "エージェントは claude・codex・copilot のどれか: $agent" >&2; exit 2 ;;
esac

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"

export PATH="$root/tools/factory/guard/bin:$PATH"
export FACTORY_AGENT="$agent"

bash tools/factory/guard/check-active.sh >/dev/null || {
  bash tools/factory/guard/check-active.sh >&2
  exit 1
}

# --skip-git: ブランチ・コミット・PR は workflow が自分で扱う。takt の worktree は origin を外すので使わない。
exec takt --pipeline --skip-git -w Software-Factory --runtime-assignment "$agent" -t "#$issue" "$@"
