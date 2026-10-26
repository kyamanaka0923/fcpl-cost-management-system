#!/bin/bash
# devcontainer の named volume を vscode ユーザーの持ち物にする(postCreateCommand が sudo で実行する)。
# vscode ユーザーの sudo はこのスクリプトと init-firewall.sh だけなので、ここで扱う場所は固定する。
set -euo pipefail
for p in /home/vscode/.nuget/packages /workspace/frontend/node_modules /home/vscode/.claude; do
  [ -e "$p" ] && chown -R vscode:vscode "$p"
done
exit 0
