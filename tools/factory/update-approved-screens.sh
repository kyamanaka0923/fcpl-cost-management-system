#!/usr/bin/env bash
# 承認する画面の画像(requirements/screens/approved/)を、CI と同じコンテナで撮り直す。
# **人が実行する。** エージェントからは実行できない(.claude/hooks/guard.sh が止める)。
#   bash tools/factory/update-approved-screens.sh               すべての画面
#   bash tools/factory/update-approved-screens.sh -g SCR-001    1 つの画面だけ
# 撮ったら、画像を開いて screens.yaml の shows どおりに見えるかを確かめてからコミットする(requirements/screens/README.md)。
set -euo pipefail
root="$(git rev-parse --show-toplevel)"
mkdir -p "$root/requirements/screens/approved"
bash "$root/tools/factory/screens-docker.sh" --update-snapshots=all "$@"
echo "撮り直しました。git status requirements/screens/approved/ で変わった画像を確かめてください。"
