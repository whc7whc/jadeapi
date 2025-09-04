# ?? API 雲端部署指南 (已修正編碼問題)

您的 Team API 已經修正並準備好部署到雲端平台了！

## ?? 問題修正

**Railway 部署失敗原因**: 
1. ~~`railway.toml` 文件格式錯誤~~ ? 已修正
2. **中文字符編碼問題** ? 已修正

**解決方案**: 
1. Railway 會自動檢測 Dockerfile，不需要 railway.toml 文件
2. 修復了所有源代碼中的中文字符編碼問題
3. 添加了 UTF-8 環境變數到 Dockerfile

## ?? 推薦平台

### 1. Railway (推薦) ?
- **免費額度**: 500小時/月
- **優點**: 自動檢測 Dockerfile，支援 GitHub 自動部署
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

#### 步驟 1: 推送修正後的代碼git add .
git commit -m "Fix encoding issues and optimize for Railway deployment"
git push origin main
#### 步驟 2: 創建新項目
1. 登入 Railway
2. 點擊 "New Project"
3. 選擇 "Deploy from GitHub repo"
4. 選擇您的倉庫

#### 步驟 3: 等待自動檢測
Railway 會自動：
- 檢測到 Dockerfile
- 開始構建 Docker 映像
- 自動設定端口

#### 步驟 4: 配置環境變數
在 Railway 專案設定 → Variables 中，添加以下環境變數：
# 基本配置 (PORT 會自動設定，不需要手動添加)
ASPNETCORE_ENVIRONMENT=Production

# 資料庫連接字串 (您已有的 Azure SQL)
ConnectionStrings__DefaultConnection=Server=tcp:jadepej-dbserver.database.windows.net,1433;Initial Catalog=jadepej-dbserver-new;Persist Security Info=False;User ID=team4;Password=#Gogojade;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;

# Cloudinary 設定
Cloudinary__CloudName=jadetainan
Cloudinary__ApiKey=384776688611428
Cloudinary__ApiSecret=4dSdNavAr96WmP0vO_wJL8TkbTU

# SMTP 設定
SmtpSettings__User=jade0905jade@gmail.com
SmtpSettings__Pass=nsuragycwfiolqpc
SmtpSettings__FromEmail=jade0905jade@gmail.com

# Google OAuth
Google__ClientId=905313427248-3vg0kd6474kbaif9ujg41n7376ua8ajp.apps.googleusercontent.com

# JWT 設定 (部署後更新 Issuer 為實際 URL)
Jwt__Key=YourSuperSecretKeyThatIsLongAndComplex_123!@#
Jwt__Issuer=https://your-railway-domain.up.railway.app
Jwt__Audience=https://moonlit-klepon-a78f8c.netlify.app

# 綠界金流 (如需要)
Ecpay__MerchantID=your_merchant_id
Ecpay__HashKey=your_hash_key
Ecpay__HashIV=your_hash_iv
Ecpay__BaseUrl=https://payment-stage.ecpay.com.tw
#### 步驟 5: 部署完成
1. Railway 會自動構建並部署
2. 等待部署完成（通常 5-10 分鐘）
3. 獲取您的 API URL（例如：`https://your-app-name.up.railway.app`）

## ?? 前端整合

### 更新前端 API 基礎 URL
將您 Vue.js 前端的 API 基礎 URL 更新為 Railway 部署的 URL：
// 在您的 Vue.js 配置中
const API_BASE_URL = 'https://your-app-name.up.railway.app'

// 或在環境變數中
VUE_APP_API_URL=https://your-app-name.up.railway.app
### 更新 JWT Issuer
部署完成後，記得回到 Railway Variables 更新：Jwt__Issuer=https://your-actual-railway-url.up.railway.app
## ?? 測試部署

### 1. 健康檢查curl https://your-app-name.up.railway.app/health
預期回應：
{
  "status": "Healthy",
  "timestamp": "2024-01-01T00:00:00.000Z",
  "version": "1.0.0",
  "environment": "Production"
}
### 2. API 文檔
訪問：`https://your-app-name.up.railway.app/swagger`

### 3. 測試根端點curl https://your-app-name.up.railway.app/
## ?? 監控與維護

### Railway 控制台
- **Build Logs**: 查看構建過程
- **Deploy Logs**: 查看部署日誌
- **Application Logs**: 查看運行時日誌
- **Metrics**: 監控 CPU、內存、網路使用情況

### 日誌查看
在 Railway 控制台中可以查看：
- 構建日誌（檢查 Docker 構建過程）
- 應用程式啟動日誌
- HTTP 請求日誌
- 錯誤日誌

## ?? 故障排除

### 常見問題與解決方案

#### 1. 構建失敗 - 編碼錯誤
**症狀**: CS1009, CS1002, CS1010 等編譯錯誤
**解決**: 
- ? 已修復所有中文字符編碼問題
- ? 添加了 UTF-8 環境變數到 Dockerfile
- 檢查是否有其他特殊字符

#### 2. 應用程式無法啟動
**症狀**: Deploy 階段失敗，應用程式崩潰
**解決**:
- 檢查環境變數配置
- 查看 Application Logs
- 驗證資料庫連接字串

#### 3. 無法訪問 API
**症狀**: 502 Bad Gateway 或連接超時
**解決**:
- 確認應用程式監聽正確端口
- 檢查健康檢查端點
- 查看防火牆設定

#### 4. CORS 錯誤
**症狀**: 前端無法調用 API
**解決**:
- 確認前端域名在 CORS 允許列表中
- 檢查 CORS 中介軟體配置

### 偵錯步驟
1. 查看 Railway Build Logs
2. 查看 Railway Deploy Logs  
3. 檢查環境變數配置
4. 測試健康檢查端點: `/health`
5. 測試根端點: `/`
6. 驗證資料庫連接

## ?? 安全性檢查清單

部署完成後，請確認：

- [ ] 所有敏感資訊都使用環境變數
- [ ] JWT 密鑰已更新為強密碼
- [ ] 資料庫連接使用加密連接
- [ ] CORS 只允許信任的域名
- [ ] Swagger 文檔已適當保護（可選）
- [ ] 沒有硬編碼的中文字符

## ? 部署完成檢查清單

- [ ] Railway 構建成功（無編碼錯誤）
- [ ] 應用程式成功啟動
- [ ] 健康檢查端點正常 (`/health`)
- [ ] 根端點正常 (`/`)
- [ ] Swagger 文檔可訪問 (`/swagger`)
- [ ] 資料庫連接正常
- [ ] 前端可以成功調用 API
- [ ] JWT 認證正常運作
- [ ] CORS 配置正確
- [ ] 所有環境變數都已設定

## ?? 恭喜！

您的 API 現已成功修復編碼問題並準備部署到 Railway！

**修復的問題**:
- ? 移除了所有中文字符編碼問題
- ? 優化了 Dockerfile 以支援 UTF-8
- ? 移除了有問題的 railway.toml 文件
- ? 確保所有源代碼使用英文註釋

**下一步**:
1. 推送修正後的代碼到 GitHub
2. 在 Railway 重新創建項目
3. 配置環境變數
4. 測試部署後的 API
5. 更新前端 Vue.js 應用程式的 API URL

---

**典型的 Railway 部署 URL 格式**:
- API 基礎 URL: `https://your-app-name.up.railway.app`
- 健康檢查: `https://your-app-name.up.railway.app/health`  
- API 文檔: `https://your-app-name.up.railway.app/swagger`
- API 端點: `https://your-app-name.up.railway.app/api/...`

記得將新的 API URL 更新到您的前端應用程式配置中！