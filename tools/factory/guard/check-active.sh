#!/usr/bin/env bash
# このシェルの gh・git に、工場のガード(bin/ の包み)が効いているかを確かめる。
# Software-Factory の start step が最初に実行する。効いていなければ終了コード 1 で、理由を出す。
# ログインシェル(bash -lc)は ~/.profile で PATH の先頭を書き換えることがあるので、エージェントのシェルの中で確かめる。
set -uo pipefail

bin_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/bin" && pwd -P)"
ok=0
for name in gh git; do
  found="$(command -v "$name" || true)"
  resolved="$( [ -n "$found" ] && cd "$(dirname "$found")" && pwd -P)/$name"
  if [ "$resolved" = "$bin_dir/$name" ]; then
    echo "ok   $name → $found"
  else
    echo "NG   $name → ${found:-見つからない}(期待: $bin_dir/$name)"
    ok=1
  fi
done
if [ $ok -ne 0 ]; then
  echo "工場のガードが効いていません。tools/factory/takt.sh から実行してください。"
  echo "それでも NG なら、エージェントのシェルが PATH を書き換えています(例: ログインシェルの ~/.profile)。"
fi
exit $ok
