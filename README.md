# ??? Team API - 電商後端服務

這是一個功能完整的 .NET 8 Web API 專案，為電商平台提供後端服務，支援購物車、結帳、會員系統、優惠券等核心功能。

## ? 主要功能

- ?? **會員系統** - 註冊、登入、JWT 認證
- ?? **購物車** - 商品加入、移除、數量調整
- ?? **結帳系統** - 完整的結帳流程，支援優惠券和點數
- ??? **優惠券系統** - 折扣碼驗證和管理
- ?? **點數系統** - 會員點數累積和抵扣
- ?? **訂單管理** - 訂單建立、狀態追蹤
- ?? **金流整合** - 綠界支付整合
- ?? **圖片上傳** - Cloudinary 雲端儲存
- ?? **郵件服務** - SMTP 郵件通知

## ?? 快速開始

### 本地開發

1. **克隆專案**
   ```bash
   git clone <your-repo-url>
   cd Team.API
   ```

2. **還原套件**
   ```bash
   dotnet restore
   ```

3. **設定資料庫連接**
   更新 `appsettings.Development.json` 中的連接字串

4. **運行專案**
   ```bash
   dotnet run
   ```

5. **訪問 API**
   - API: `https://localhost:7106`
   - Swagger: `https://localhost:7106/swagger`

## ?? 雲端部署

### 一鍵部署到 Railway

1. **推送代碼到 GitHub**
2. **連接 Railway**
   - 訪問 [Railway.app](https://railway.app)
   - 創建新項目並連接 GitHub 倉庫
3. **配置環境變數** (參考 `DEPLOYMENT_GUIDE.md`)
4. **自動部署完成**

### 快速測試部署

**Windows:**
```bash
deploy.bat
```

**Linux/macOS:**
```bash
chmod +x deploy.sh
./deploy.sh
```

## ?? 環境配置

### 必要環境變數

```env
# 資料庫
ConnectionStrings__DefaultConnection=your_database_connection

# JWT
JWT_SECRET_KEY=your_secret_key
JWT_ISSUER=your_api_url

# Cloudinary
CLOUDINARY_CLOUD_NAME=your_cloud_name
CLOUDINARY_API_KEY=your_api_key
CLOUDINARY_API_SECRET=your_api_secret

# 郵件服務
SMTP_USER=your_email
SMTP_PASS=your_password
```

## ?? API 文檔

部署後可通過以下端點訪問：

- **健康檢查**: `GET /health`
- **API 文檔**: `GET /swagger`
- **會員 API**: `GET /api/members`
- **購物車 API**: `GET /api/cart`
- **結帳 API**: `GET /api/checkout`

## ?? 前端整合

此 API 已配置 CORS 以支援以下前端：
- `https://moonlit-klepon-a78f8c.netlify.app` (您的 Vue.js 作品集)
- `http://localhost:3000` (本地開發)

### 前端使用範例

```javascript
// 設定 API 基礎 URL
const API_BASE_URL = 'https://your-api.railway.app'

// 會員登入
const login = async (email, password) => {
  const response = await fetch(`${API_BASE_URL}/api/members/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  })
  return response.json()
}
```

## ??? 技術架構

- **框架**: ASP.NET Core 8.0
- **資料庫**: Azure SQL Database
- **認證**: JWT Bearer Token
- **圖片儲存**: Cloudinary
- **郵件服務**: Gmail SMTP
- **支付**: 綠界 ECPay
- **部署**: Docker + Railway

## ?? 專案結構

```
Team.API/
├── Controllers/          # API 控制器
├── Models/              # 資料模型
├── Services/            # 業務邏輯服務
├── DTOs/               # 資料傳輸物件
├── Payments/           # 支付整合
├── wwwroot/            # 靜態檔案
└── Program.cs          # 應用程式進入點
```

## ?? 安全性

- ? JWT 認證授權
- ? HTTPS 強制使用
- ? CORS 跨域保護
- ? 輸入驗證
- ? SQL 注入防護
- ? 環境變數保護敏感資訊

## ?? 監控與日誌

- 應用程式健康檢查端點
- 結構化日誌記錄
- 錯誤處理和回報
- 效能監控

## ?? 貢獻

歡迎提交 Issue 和 Pull Request！

## ?? 授權

此專案僅供學習和作品集展示使用。

---

**?? 此 API 已準備好為您的電商前端提供強大的後端支援！**