#!/usr/bin/env bash
# bin/gh・bin/git の中身。$1 = コマンド名(gh か git)、残り = その引数。
# 止める操作なら理由を標準エラーに出して終了コード 2 で終える。通すなら PATH の次の本物を exec する。
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
bin_dir="$here/bin"
name="$1"
shift

# shellcheck source=rules.sh
. "$here/rules.sh"
if ! reason="$(factory_guard_command "$name $*")"; then
  factory_guard_explain "$reason"
  exit 2
fi

# PATH からこのフォルダを除いて、本物を探す(自分を呼び直さない)
real=""
IFS=: read -r -a dirs <<<"${PATH:-}"
for d in "${dirs[@]}"; do
  [ -z "$d" ] && continue
  [ "$(cd "$d" 2>/dev/null && pwd -P)" = "$(cd "$bin_dir" && pwd -P)" ] && continue
  if [ -x "$d/$name" ] && [ ! -d "$d/$name" ]; then
    real="$d/$name"
    break
  fi
done
if [ -z "$real" ]; then
  echo "工場のガード: 本物の $name が PATH に見つかりません" >&2
  exit 127
fi
exec "$real" "$@"
