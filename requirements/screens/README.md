# 画面要件(requirements/screens/)

工程 [B] の成果物のうち、画面の部分。**画面を、状態ごとの見た目で決める**(PROC-DEV-001 3.1.3)。
要件(EARS)が「何をするか」を決め、ここは「どう見えるか」を決める。ゲート1 で要件と一緒に承認する。

```
requirements/screens/
  README.md                       この文書
  screens.yaml                    画面の一覧(画面 ID・ルート・項目・状態・操作・要件)
  fixtures/<SCR-xxx>/<状態>.json  状態ごとの API の応答
  approved/<SCR-xxx>-<状態>.png   承認した画面(人だけが書く)
```

## screens.yaml

```yaml
screens:
  - id: SCR-001                     # SCR-<3 桁>。使い回さない
    title: 費目マスタ
    route: /cost-elements           # 画面を開く URL(frontend/src/App.tsx のルート)
    page: frontend/src/pages/CostElementsPage.tsx
    requirements: [FR-3.1, FR-3.3]  # requirements/ か docs/REQUIREMENTS.md にある要件
    items:                          # 画面に出るもの
      - 費目コードと費目名の表
    operations:                     # 画面でできる操作(API はメソッドとパス)
      - name: 費目を追加する
        request: POST /api/cost-elements
    states:                         # 状態ごとに fixtures と承認した画面が 1 つずつ要る
      - name: 通常
        shows: 費目の表に 2 行が出る    # この状態で何が見えるか(承認するときの見どころ)
```

- 状態は少なくとも「通常」を置く。データが空のとき・API がエラーのとき・境界の値のときに見た目が変わるなら、その状態も置く
- 状態の名前は、fixtures のファイル名と承認した画面のファイル名に使う
- 文言・色・配置の決まりは、既存の画面に合わせる(エラーはトースト、差異の色、ダークモード。`feature-dev` スキル)

## fixtures

状態ごとに 1 ファイル。画面が呼ぶ API の応答を、`"<メソッド> <パス>"` をキーにして並べる。パスは OpenAPI の書き方(`{id}` はどの値にも当たる)。

```json
{
  "GET /api/cost-elements": {
    "status": 200,
    "body": [{ "code": "PERSONNEL", "name": "人件費" }]
  }
}
```

- `body` は、`docs/api/openapi.json` のその応答のスキーマに合わせる(CI の「画面要件と fixtures」が検査する)
- 画面が fixtures にない API を呼んだら、画面の差分の検査は落ちる(呼ぶ API を漏れなく書く)

## 承認した画面(approved/)

- 画面の差分の検査(`tests/e2e/screens`)が、fixtures で開いた画面のスクリーンショットを、ここの画像と 1 画素ずつ比べる
- **画像を作って置くのは人だけ。** エージェントはここに書けない(フックが止める)
  1. `req/<n>-…` ブランチで `bash tools/factory/update-approved-screens.sh` を実行する(CI と同じコンテナで撮る。Docker が要る)
  2. できた画像を開いて、`screens.yaml` の `shows` どおりに見えるかを確かめる
  3. 画像をコミットする。PR のマージがゲート1 の承認になる
- 画面のコード(`frontend/src`)を変えて見た目が変わる [D] 実装の PR は、画面の差分の検査で落ちる。見た目を変えるのは、要件([B])で承認した画面を先に変えてから
