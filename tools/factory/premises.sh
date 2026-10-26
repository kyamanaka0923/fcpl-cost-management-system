#!/usr/bin/env bash
# 前提の記録 .factory/premises/ISSUE-<n>.yml を作る(.factory/premises/README.md)。
# 使い方: bash tools/factory/premises.sh <Issue番号> <spec|impl> <ファイル>...
# 版は origin/main にあるファイルの blob SHA。main にないファイル(まだ承認されていないもの)は前提にできない。
set -euo pipefail

if [ $# -lt 3 ]; then
  echo "使い方: $0 <Issue番号> <spec|impl> <ファイル>..." >&2
  exit 1
fi
issue="$1"; stage="$2"; shift 2
case "$issue" in ''|*[!0-9]*) echo "Issue 番号は数字で指定してください: $issue" >&2; exit 1 ;; esac
case "$stage" in spec|impl) ;; *) echo "工程は spec か impl で指定してください: $stage" >&2; exit 1 ;; esac

root="$(git rev-parse --show-toplevel)"
git -C "$root" fetch -q origin main

out="$root/.factory/premises/ISSUE-$issue.yml"
trap 'rm -f "$out.tmp"' EXIT
{
  echo "issue: $issue"
  echo "stage: $stage"
  echo "factory_version: $(tr -d '[:space:]' < "$root/.factory/VERSION")"
  echo "recorded_at: $(TZ=Asia/Tokyo date '+%Y-%m-%dT%H:%M:%S+09:00')"
  echo "premises:"
  for path in "$@"; do
    if ! blob="$(git -C "$root" rev-parse -q --verify "origin/main:$path" 2>/dev/null)"; then
      echo "main にないファイルは前提にできません(まだ承認されていない): $path" >&2
      exit 1
    fi
    echo "  - path: $path"
    echo "    blob: $blob"
  done
} > "$out.tmp"
mv "$out.tmp" "$out"
echo "前提を書きました: ${out#"$root"/}"
