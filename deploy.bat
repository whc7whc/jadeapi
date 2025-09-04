@echo off
REM Team API 快速部署腳本 (Windows)
echo ?? 準備部署 Team API 到雲端...

REM 檢查 Docker 是否安裝
docker --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ? Docker 未安裝，請先安裝 Docker Desktop
    pause
    exit /b 1
)

REM 檢查 .NET 是否安裝
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ? .NET SDK 未安裝，請先安裝 .NET 8 SDK
    pause
    exit /b 1
)

echo ? 環境檢查完成

REM 構建 Docker 映像
echo ?? 構建 Docker 映像...
docker build -t team-api .

if %errorlevel% neq 0 (
    echo ? Docker 映像構建失敗
    pause
    exit /b 1
)

echo ? Docker 映像構建成功

REM 測試本地運行
echo ?? 測試本地運行...
docker run -d -p 8080:8080 --name team-api-test team-api

REM 等待應用啟動
timeout /t 10 /nobreak >nul

REM 測試健康檢查
echo ?? 測試健康檢查...
curl -f http://localhost:8080/health >nul 2>&1
if %errorlevel% neq 0 (
    echo ? 健康檢查失敗
    docker logs team-api-test
    docker stop team-api-test
    docker rm team-api-test
    pause
    exit /b 1
)

echo ? 健康檢查通過

REM 清理測試容器
docker stop team-api-test
docker rm team-api-test

echo ?? 本地測試完成！
echo.
echo ?? 下一步部署到雲端：
echo 1. 將代碼推送到 GitHub
echo 2. 在 Railway.app 創建新項目
echo 3. 連接您的 GitHub 倉庫
echo 4. 配置環境變數（參考 DEPLOYMENT_GUIDE.md）
echo 5. 等待自動部署完成
echo.
echo ?? 詳細步驟請參考 DEPLOYMENT_GUIDE.md
echo.
echo 按任意鍵繼續...
pause >nul