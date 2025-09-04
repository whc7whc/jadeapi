# ?? API 雲端部署指南

您的 Team API 已經準備好部署到雲端平台了！以下是三個推薦的免費部署選項：

## ?? 推薦平台

### 1. Railway (推薦) ?
- **免費額度**: 500小時/月
- **優點**: 簡單易用，支援 GitHub 自動部署
- **適合**: 長期運行的 API 服務

### 2. Render
- **免費額度**: 750小時/月，但會休眠
- **優點**: 靜態 IP，良好的日誌功能
- **缺點**: 免費版會在30分鐘無活動後休眠

### 3. Azure App Service
- **免費額度**: F1 層級免費
- **優點**: Microsoft 生態系，與 Azure SQL 整合良好
- **缺點**: 配置較複雜

## ?? Railway 部署步驟 (推薦)

### 準備工作
1. 創建 [Railway](https://railway.app) 帳號
2. 將代碼推送到 GitHub 倉庫

### 部署步驟

#### 步驟 1: 創建新項目
1. 登入 Railway
2. 點擊 "New Project"
3. 選擇 "Deploy from GitHub repo"
4. 選擇您的倉庫

#### 步驟 2: 配置環境變數
在 Railway 專案設定中，添加以下環境變數：

```bash
# 基本配置
ASPNETCORE_ENVIRONMENT=Production
PORT=8080

# 資料庫連接字串 (您已有的 Azure SQL)
ConnectionStrings__DefaultConnection=Server=tcp:jadepej-dbserver.database.windows.net,1433;Initial Catalog=jadepej-dbserver-new;Persist Security Info=False;User ID=team4;Password=#Gogojade;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;

# Cloudinary 設定
CLOUDINARY_CLOUD_NAME=jadetainan
CLOUDINARY_API_KEY=384776688611428
CLOUDINARY_API_SECRET=4dSdNavAr96WmP0vO_wJL8TkbTU

# SMTP 設定
SMTP_USER=jade0905jade@gmail.com
SMTP_PASS=nsuragycwfiolqpc
SMTP_FROM_EMAIL=jade0905jade@gmail.com

# Google OAuth
GOOGLE_CLIENT_ID=905313427248-3vg0kd6474kbaif9ujg41n7376ua8ajp.apps.googleusercontent.com

# JWT 設定
JWT_SECRET_KEY=YourSuperSecretKeyThatIsLongAndComplex_123!@#
JWT_ISSUER=https://your-railway-domain.railway.app

# 綠界金流 (如需要)
ECPAY_MERCHANT_ID=your_merchant_id
ECPAY_HASH_KEY=your_hash_key
ECPAY_HASH_IV=your_hash_iv
ECPAY_BASE_URL=https://payment-stage.ecpay.com.tw
```

#### 步驟 3: 部署
1. Railway 會自動檢測 Dockerfile 並開始構建
2. 等待部署完成（通常 3-5 分鐘）
3. 獲取您的 API URL（例如：`https://your-app-name.railway.app`）

## ?? 前端整合

### 更新前端 API 基礎 URL
將您 Vue.js 前端的 API 基礎 URL 更新為 Railway 部署的 URL：

```javascript
// 在您的 Vue.js 配置中
const API_BASE_URL = 'https://your-app-name.railway.app'

// 或在環境變數中
VUE_APP_API_URL=https://your-app-name.railway.app
```

### CORS 配置已完成
API 已配置為允許來自您的 Netlify 網站的請求：
- `https://moonlit-klepon-a78f8c.netlify.app`

## ?? 測試部署

### 1. 健康檢查
```bash
curl https://your-app-name.railway.app/health
```

預期回應：
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-01T00:00:00.000Z",
  "version": "1.0.0",
  "environment": "Production"
}
```

### 2. API 文檔
訪問：`https://your-app-name.railway.app/swagger`

### 3. 測試 API 端點
```bash
# 測試會員註冊或登入
curl -X POST https://your-app-name.railway.app/api/members/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!","name":"測試用戶"}'
```

## ?? 監控與維護

### Railway 監控
- **Dashboard**: 查看 CPU、內存使用情況
- **Logs**: 實時查看應用程式日誌
- **Metrics**: 監控請求數量和回應時間

### 日誌查看
在 Railway 控制台中可以查看：
- 應用程式啟動日誌
- HTTP 請求日誌
- 錯誤日誌

## ?? 安全性建議

### 1. 環境變數安全
- 所有敏感資訊都通過環境變數配置
- 不要在代碼中硬編碼密鑰

### 2. HTTPS
- Railway 自動提供 HTTPS
- 確保前端只使用 HTTPS 調用 API

### 3. CORS 配置
- 已配置為只允許您的前端域名
- 定期檢查和更新允許的域名

## ?? 優化建議

### 1. 效能優化
- 考慮啟用 API 響應壓縮
- 實施適當的緩存策略
- 監控資料庫查詢效能

### 2. 成本控制
- 監控 Railway 使用時間
- 考慮在必要時升級到付費方案
- 優化代碼以降低 CPU 使用率

### 3. 備份策略
- 定期備份 Azure SQL 資料庫
- 保持代碼倉庫的完整性

## ?? 故障排除

### 常見問題
1. **部署失敗**: 檢查 Dockerfile 和代碼語法
2. **資料庫連接失敗**: 驗證連接字串和防火牆設定
3. **CORS 錯誤**: 確認前端域名在允許列表中
4. **502/503 錯誤**: 檢查應用程式是否監聽正確端口 (8080)

### 偵錯步驟
1. 查看 Railway 部署日誌
2. 檢查環境變數配置
3. 測試健康檢查端點
4. 驗證資料庫連接

## ? 完成清單

部署完成後，請確認：

- [ ] API 健康檢查正常
- [ ] Swagger 文檔可訪問
- [ ] 資料庫連接正常
- [ ] 前端可以成功調用 API
- [ ] CORS 配置正確
- [ ] 環境變數都已設定
- [ ] 日誌顯示正常運行

## ?? 恭喜！

您的 API 現已成功部署到雲端！您的前端網站 `https://moonlit-klepon-a78f8c.netlify.app` 現在可以使用部署的 API 來展示您的作品集了。

---

**部署後的 API URL 示例**:
- 健康檢查: `https://your-app.railway.app/health`
- API 文檔: `https://your-app.railway.app/swagger`
- API 端點: `https://your-app.railway.app/api/...`

記得將新的 API URL 更新到您的前端應用程式配置中！