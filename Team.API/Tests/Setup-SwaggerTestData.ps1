# 🚀 設置 Swagger 測試資料說明
# 由於資料庫連線問題，請手動執行以下步驟

Write-Host "🚀 Swagger 測試資料設置說明" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""

Write-Host "❗ 偵測到資料庫連線問題，請手動設置：" -ForegroundColor Yellow
Write-Host ""

Write-Host "📋 手動設置步驟：" -ForegroundColor Cyan
Write-Host ""
Write-Host "1️⃣  開啟 SQL Server Management Studio (SSMS)" -ForegroundColor White
Write-Host ""
Write-Host "2️⃣  連線到您的 SQL Server 實例" -ForegroundColor White
Write-Host ""
Write-Host "3️⃣  選擇或建立 'Team' 資料庫" -ForegroundColor White
Write-Host ""
Write-Host "4️⃣  開啟檔案：$((Get-Location).Path)\Tests\SwaggerTestData.sql" -ForegroundColor White
Write-Host ""
Write-Host "5️⃣  執行 SQL 腳本 (F5 或點擊執行按鈕)" -ForegroundColor White
Write-Host ""

Write-Host "🔍 SQL 檔案位置：" -ForegroundColor Cyan
Write-Host "   $(Resolve-Path "Tests\SwaggerTestData.sql")" -ForegroundColor Yellow
Write-Host ""

Write-Host "✅ 執行完成後，您應該會看到：" -ForegroundColor Green
Write-Host "   ✅ 建立測試用戶: test@example.com (ID: 1)" -ForegroundColor White
Write-Host "   ✅ 建立測試優惠券 1: 10% 折扣券" -ForegroundColor White
Write-Host "   ✅ 建立測試優惠券 2: 滿減 $50" -ForegroundColor White
Write-Host "   ✅ 分配優惠券給測試用戶" -ForegroundColor White
Write-Host ""

Write-Host "🧪 設置完成後，請執行以下測試：" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. 啟動 Team.API 專案 (在 Visual Studio 中按 F5)" -ForegroundColor White
Write-Host "2. 瀏覽器開啟: https://localhost:7106/swagger/index.html" -ForegroundColor White
Write-Host "3. 按照以下順序測試：" -ForegroundColor White
Write-Host ""

Write-Host "📝 完整測試流程：" -ForegroundColor Cyan
Write-Host ""
Write-Host "步驟 1: 取得空購物車" -ForegroundColor Yellow
Write-Host "  GET /api/Carts/user/1" -ForegroundColor White
Write-Host ""

Write-Host "步驟 2: 加入商品到購物車" -ForegroundColor Yellow
Write-Host "  POST /api/Carts/user/1/items" -ForegroundColor White
Write-Host "  Request Body:" -ForegroundColor Gray
Write-Host '  {' -ForegroundColor Gray
Write-Host '    "productId": 1,' -ForegroundColor Gray
Write-Host '    "attributeValueId": 1,' -ForegroundColor Gray
Write-Host '    "quantity": 2' -ForegroundColor Gray
Write-Host '  }' -ForegroundColor Gray
Write-Host ""

Write-Host "步驟 3: 取得可用優惠券" -ForegroundColor Yellow
Write-Host "  GET /api/Coupons/UserAvailable/1" -ForegroundColor White
Write-Host ""

Write-Host "步驟 4: 套用優惠券" -ForegroundColor Yellow
Write-Host "  POST /api/Carts/user/1/coupon" -ForegroundColor White
Write-Host "  Request Body:" -ForegroundColor Gray
Write-Host '  {' -ForegroundColor Gray
Write-Host '    "couponCode": "1"' -ForegroundColor Gray
Write-Host '  }' -ForegroundColor Gray
Write-Host ""

Write-Host "步驟 5: 檢查購物車 (應該看到折扣)" -ForegroundColor Yellow
Write-Host "  GET /api/Carts/user/1" -ForegroundColor White
Write-Host ""

Write-Host "🎯 預期結果：" -ForegroundColor Cyan
Write-Host "  • 購物車總額: $1000 (2個商品 × $500)" -ForegroundColor White
Write-Host "  • 10% 折扣: -$100" -ForegroundColor White
Write-Host "  • 最終金額: $900" -ForegroundColor White
Write-Host "  • 運費: $0 (滿千免運)" -ForegroundColor White
Write-Host ""

Write-Host "❓ 如果遇到 '您沒有此優惠券，無法使用' 錯誤：" -ForegroundColor Red
Write-Host "   → 確認 SQL 腳本執行成功" -ForegroundColor White
Write-Host "   → 檢查 MemberCoupons 表中是否有資料" -ForegroundColor White
Write-Host "   → 重新執行 SQL 腳本" -ForegroundColor White
Write-Host ""

# 檢查 SQL 檔案是否存在
if (Test-Path "Tests\SwaggerTestData.sql") {
    Write-Host "✅ SQL 檔案準備就緒，可以在 SSMS 中執行" -ForegroundColor Green
} else {
    Write-Host "❌ 找不到 SQL 檔案，請檢查檔案路徑" -ForegroundColor Red
}

Write-Host ""
Read-Host "按任意鍵繼續..."