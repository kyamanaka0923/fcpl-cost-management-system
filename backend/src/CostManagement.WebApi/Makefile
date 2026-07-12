# AWS SAM の makefile ビルダー用。`sam build`(infra/template.yaml の
# Metadata.BuildMethod: makefile)から呼ばれ、.NET 10 を provided.al2023 向けに
# self-contained 発行して bootstrap 実行ファイルを $(ARTIFACTS_DIR) に出力する。
# コンテナは使わない(zip デプロイ)。ローカルの通常ビルド・テストには影響しない。

build-CostApiFunction:
	dotnet publish CostManagement.WebApi.csproj \
		-c Release \
		-r linux-arm64 \
		-p:LambdaPublish=true \
		-o "$(ARTIFACTS_DIR)"
