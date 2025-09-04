#!/bin/bash

# Team API 快速部署腳本
echo "?? 準備部署 Team API 到雲端..."

# 檢查 Docker 是否安裝
if ! command -v docker &> /dev/null; then
    echo "? Docker 未安裝，請先安裝 Docker"
    exit 1
fi

# 檢查 .NET 是否安裝
if ! command -v dotnet &> /dev/null; then
    echo "? .NET SDK 未安裝，請先安裝 .NET 8 SDK"
    exit 1
fi

echo "? 環境檢查完成"

# 構建 Docker 映像
echo "?? 構建 Docker 映像..."
docker build -t team-api .

if [ $? -eq 0 ]; then
    echo "? Docker 映像構建成功"
else
    echo "? Docker 映像構建失敗"
    exit 1
fi

# 測試本地運行
echo "?? 測試本地運行..."
docker run -d -p 8080:8080 --name team-api-test team-api

# 等待應用啟動
sleep 10

# 測試健康檢查
echo "?? 測試健康檢查..."
if curl -f http://localhost:8080/health > /dev/null 2>&1; then
    echo "? 健康檢查通過"
else
    echo "? 健康檢查失敗"
    docker logs team-api-test
    docker stop team-api-test
    docker rm team-api-test
    exit 1
fi

# 清理測試容器
docker stop team-api-test
docker rm team-api-test

echo "?? 本地測試完成！"
echo ""
echo "?? 下一步部署到雲端："
echo "1. 將代碼推送到 GitHub"
echo "2. 在 Railway.app 創建新項目"
echo "3. 連接您的 GitHub 倉庫"
echo "4. 配置環境變數（參考 DEPLOYMENT_GUIDE.md）"
echo "5. 等待自動部署完成"
echo ""
echo "?? 詳細步驟請參考 DEPLOYMENT_GUIDE.md"