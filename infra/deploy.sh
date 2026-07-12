#!/usr/bin/env bash
# 総合原価管理システムを AWS(Lambda + EFS + S3 + CloudFront)へデプロイする。
# 前提: AWS CLI / AWS SAM CLI / .NET 10 SDK / Node.js が入っていて、AWS 認証情報が設定済み。
#
#   ./deploy.sh            # ビルド → デプロイ → フロント配信 → キャッシュ無効化
#   STACK=my-stack ./deploy.sh
set -euo pipefail
cd "$(dirname "$0")"

STACK="${STACK:-fcpl-cost}"
REGION="${REGION:-ap-northeast-1}"

echo "==> 1/4 SAM ビルド(.NET 10 を provided.al2023 向けに self-contained 発行)"
sam build

echo "==> 2/4 SAM デプロイ(スタック: ${STACK})"
sam deploy --stack-name "${STACK}" --region "${REGION}"

echo "==> スタック出力を取得"
outputs=$(aws cloudformation describe-stacks --stack-name "${STACK}" --region "${REGION}" \
  --query "Stacks[0].Outputs" --output json)
bucket=$(echo "${outputs}" | python3 -c "import sys,json;print(next(o['OutputValue'] for o in json.load(sys.stdin) if o['OutputKey']=='FrontendBucketName'))")
dist=$(echo "${outputs}" | python3 -c "import sys,json;print(next(o['OutputValue'] for o in json.load(sys.stdin) if o['OutputKey']=='DistributionId'))")
url=$(echo "${outputs}" | python3 -c "import sys,json;print(next(o['OutputValue'] for o in json.load(sys.stdin) if o['OutputKey']=='CloudFrontUrl'))")

echo "==> 3/4 フロントをビルドして S3 (${bucket}) へ配信"
(cd ../frontend && npm ci && npm run build)
aws s3 sync ../frontend/dist "s3://${bucket}" --delete --region "${REGION}"

echo "==> 4/4 CloudFront (${dist}) のキャッシュを無効化"
aws cloudfront create-invalidation --distribution-id "${dist}" --paths "/*" >/dev/null

echo ""
echo "完了。アプリURL: ${url}"
