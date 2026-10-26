#!/usr/bin/env bash
# 画面の差分の検査(tests/e2e/screens)を、CI と同じ Playwright のコンテナで実行する。
# フォントと描画がコンテナで決まるので、承認した画面との比較はこのコンテナでだけ意味がある。
#   bash tools/factory/screens-docker.sh                 承認した画面と比べる
#   bash tools/factory/screens-docker.sh <playwright の引数>...
# 承認した画面を作り直すのは人だけ(tools/factory/update-approved-screens.sh)。
set -euo pipefail

# tests/e2e/screens/package.json の @playwright/test と同じ版にする(.github/workflows/factory.yml も同じ)
image="mcr.microsoft.com/playwright:v1.64.1-noble"
root="$(git rev-parse --show-toplevel)"
owner="$(id -u):$(id -g)"

# node_modules はコンテナの中だけのボリュームに入れ、手元の node_modules(devcontainer のものなど)に触らない。
# コンテナは root で動くので、書いたファイル(画像・結果)は最後に実行した人の持ち物に戻す。
docker run --rm --ipc=host -e CI=1 -e OWNER="$owner" \
  ${SCREENS_APPROVED_DIR:+-e SCREENS_APPROVED_DIR="$SCREENS_APPROVED_DIR" -v "$SCREENS_APPROVED_DIR:$SCREENS_APPROVED_DIR"} \
  -v "$root:/work" -v /work/frontend/node_modules -v /work/tests/e2e/screens/node_modules -w /work \
  "$image" bash -c '
    give_back() {
      for p in /work/requirements/screens/approved /work/tests/e2e/screens/test-results /work/tests/e2e/screens/playwright-report ${SCREENS_APPROVED_DIR:-}; do
        [ -e "$p" ] && chown -R "$OWNER" "$p"
      done
      return 0
    }
    trap give_back EXIT
    set -e
    (cd frontend && npm ci --no-audit --no-fund --loglevel=error)
    cd tests/e2e/screens && npm ci --no-audit --no-fund --loglevel=error
    cd /work/tests/e2e/screens && npx playwright test "$@"
  ' bash "$@"
