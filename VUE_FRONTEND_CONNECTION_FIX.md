# ?? Vue 前端連接 Railway API 修復指南

## ?? **主要問題**
您的 Vue 前端無法連接到 Railway API 的原因：

### ? **已修復的問題**
1. **JWT 配置錯誤** - `appsettings.json` 中的 `Issuer` 和 `Audience` 已更新為正確的 URL
2. **CORS 配置** - 已在 `Program.cs` 中配置支援您的 Netlify 域名

### ?? **需要檢查的 Vue 前端配置**

#### **1. API 基礎 URL 設定**
檢查您的 Vue 專案中的 API 基礎 URL 是否正確：

```javascript
// 在您的 Vue 專案中，找到 API 配置檔案
// 可能在以下位置：
// - src/config/api.js
// - src/utils/request.js  
// - src/api/index.js
// - .env 檔案

// 確保 API URL 設定為：
const API_BASE_URL = 'https://jadeapi-production.up.railway.app'

// 或在 .env 檔案中：
VUE_APP_API_URL=https://jadeapi-production.up.railway.app
```

#### **2. Axios 或 Fetch 配置**
```javascript
// 如果使用 axios
import axios from 'axios'

const api = axios.create({
  baseURL: 'https://jadeapi-production.up.railway.app',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json'
  }
})

// 如果需要攜帶 credentials
api.defaults.withCredentials = true
```

#### **3. 環境變數配置**
在您的 Vue 專案根目錄創建或更新這些檔案：

**.env.development**
```
VUE_APP_API_URL=http://localhost:7106
VUE_APP_ENV=development
```

**.env.production**
```
VUE_APP_API_URL=https://jadeapi-production.up.railway.app
VUE_APP_ENV=production
```

## ?? **立即修復步驟**

### **第一步：重新部署 Railway**
```bash
git add .
git commit -m "?? Fix JWT configuration for Railway deployment"
git push origin main
```

### **第二步：更新 Vue 前端配置**
1. 在您的 Vue 專案中找到 API 配置
2. 將 API URL 更新為：`https://jadeapi-production.up.railway.app`
3. 重新構建和部署前端

### **第三步：測試連接**
使用以下測試代碼檢查連接：

```javascript
// 在 Vue 專案中測試 API 連接
async function testAPIConnection() {
  try {
    const response = await fetch('https://jadeapi-production.up.railway.app/health')
    const data = await response.json()
    console.log('API 連接成功:', data)
  } catch (error) {
    console.error('API 連接失敗:', error)
  }
}

// 在瀏覽器控制台執行
testAPIConnection()
```

## ?? **常見問題診斷**

### **CORS 錯誤**
如果看到 CORS 錯誤，檢查：
- API 的 CORS 配置是否包含您的前端域名
- 前端請求是否正確設定 headers

### **404 錯誤**  
如果 API 回傳 404：
- 檢查 API 端點路徑是否正確
- 確認 Railway 部署是否成功

### **500 錯誤**
如果 API 回傳 500：
- 檢查 Railway 日誌
- 確認資料庫連接是否正常

## ?? **完整的 Vue 專案 API 配置範例**

```javascript
// src/utils/request.js
import axios from 'axios'

// 根據環境設定 API URL
const baseURL = process.env.NODE_ENV === 'production' 
  ? 'https://jadeapi-production.up.railway.app'
  : process.env.VUE_APP_API_URL || 'http://localhost:7106'

const request = axios.create({
  baseURL: baseURL,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json'
  }
})

// 請求攔截器
request.interceptors.request.use(
  config => {
    // 添加認證 token
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  error => {
    return Promise.reject(error)
  }
)

// 響應攔截器
request.interceptors.response.use(
  response => response,
  error => {
    console.error('API 請求錯誤:', error)
    if (error.response?.status === 401) {
      // 處理認證失敗
      localStorage.removeItem('token')
      // 重定向到登入頁面
    }
    return Promise.reject(error)
  }
)

export default request
```

## ? **修復確認清單**

- [ ] Railway API JWT 配置已更新
- [ ] Vue 專案 API URL 已更新
- [ ] 前端已重新構建和部署
- [ ] CORS 設定正確
- [ ] 測試 API 連接成功
- [ ] 認證功能正常
- [ ] 所有 API 端點可正常訪問

## ?? **成功標準**

當您完成修復後，應該能夠：

1. ? 在瀏覽器控制台看到成功的 API 請求
2. ? 前端可以正常登入/註冊  
3. ? 商品列表可以正常載入
4. ? 購物車功能正常運作
5. ? 無 CORS 錯誤

完成這些步驟後，您的全端應用就能完美運行了！??