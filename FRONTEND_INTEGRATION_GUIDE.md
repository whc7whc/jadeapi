# ?? 前端 Vue.js 整合指南

## ?? 您的 API 配置狀態

? **CORS 已正確配置** - 支援您的 Netlify 網站
? **JWT 已設定** - Audience 指向您的前端域名
? **健康檢查端點** - 可用於監控 API 狀態

## ?? API 部署完成後的前端整合步驟

### 1. ?? 設定 API 基礎 URL

在您的 Vue.js 專案中建立 API 配置檔案：

**創建 `src/config/api.js`**
```javascript
// API 配置
const API_CONFIG = {
  // 開發環境 (本地)
  development: {
    baseURL: 'https://localhost:7106',
    timeout: 30000
  },
  
  // 生產環境 (Railway 部署後的 URL)
  production: {
    baseURL: 'https://your-app-name.up.railway.app',  // ?? 部署後更新此 URL
    timeout: 30000
  }
}

// 根據環境自動選擇
const currentConfig = API_CONFIG[process.env.NODE_ENV] || API_CONFIG.production

export default {
  baseURL: currentConfig.baseURL,
  timeout: currentConfig.timeout,
  
  // API 端點
  endpoints: {
    // 會員相關
    auth: {
      login: '/api/members/login',
      register: '/api/members/register',
      profile: '/api/members/profile',
      googleLogin: '/api/members/google-login'
    },
    
    // 商品相關
    products: {
      list: '/api/products',
      detail: '/api/products',
      categories: '/api/categories'
    },
    
    // 購物車相關
    cart: {
      get: '/api/cart',
      add: '/api/cart/add',
      update: '/api/cart/update',
      remove: '/api/cart/remove',
      clear: '/api/cart/clear'
    },
    
    // 結帳相關
    checkout: {
      validate: '/api/checkout/validate',
      summary: '/api/checkout/summary',
      createOrder: '/api/checkout/create-order',
      paymentMethods: '/api/checkout/payment-methods'
    },
    
    // 優惠券相關
    coupons: {
      validate: '/api/checkout/validate-coupon',
      list: '/api/coupons'
    },
    
    // 系統狀態
    health: '/health'
  }
}
```

### 2. ?? 設定 Axios 攔截器

**創建 `src/utils/http.js`**
```javascript
import axios from 'axios'
import apiConfig from '@/config/api'

// 創建 axios 實例
const http = axios.create({
  baseURL: apiConfig.baseURL,
  timeout: apiConfig.timeout,
  headers: {
    'Content-Type': 'application/json'
  }
})

// 請求攔截器 - 自動添加 JWT Token
http.interceptors.request.use(
  (config) => {
    // 從 localStorage 獲取 token
    const token = localStorage.getItem('auth_token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    
    console.log(`?? API 請求: ${config.method?.toUpperCase()} ${config.url}`)
    return config
  },
  (error) => {
    console.error('? 請求錯誤:', error)
    return Promise.reject(error)
  }
)

// 回應攔截器 - 處理錯誤和 token 過期
http.interceptors.response.use(
  (response) => {
    console.log(`? API 回應: ${response.config.url} - ${response.status}`)
    return response
  },
  (error) => {
    console.error('? API 錯誤:', error.response?.data || error.message)
    
    // Token 過期處理
    if (error.response?.status === 401) {
      localStorage.removeItem('auth_token')
      localStorage.removeItem('user_info')
      
      // 重導向到登入頁面
      window.location.href = '/login'
    }
    
    return Promise.reject(error)
  }
)

export default http
```

### 3. ??? 創建 API 服務

**創建 `src/services/apiService.js`**
```javascript
import http from '@/utils/http'
import apiConfig from '@/config/api'

export const apiService = {
  // 會員服務
  auth: {
    // 登入
    async login(email, password) {
      const response = await http.post(apiConfig.endpoints.auth.login, {
        email,
        password
      })
      return response.data
    },
    
    // 註冊
    async register(userData) {
      const response = await http.post(apiConfig.endpoints.auth.register, userData)
      return response.data
    },
    
    // Google 登入
    async googleLogin(googleToken) {
      const response = await http.post(apiConfig.endpoints.auth.googleLogin, {
        token: googleToken
      })
      return response.data
    },
    
    // 獲取會員資料
    async getProfile() {
      const response = await http.get(apiConfig.endpoints.auth.profile)
      return response.data
    }
  },

  // 商品服務
  products: {
    // 獲取商品列表
    async getList(params = {}) {
      const response = await http.get(apiConfig.endpoints.products.list, { params })
      return response.data
    },
    
    // 獲取商品詳情
    async getDetail(productId) {
      const response = await http.get(`${apiConfig.endpoints.products.detail}/${productId}`)
      return response.data
    }
  },

  // 購物車服務
  cart: {
    // 獲取購物車
    async get() {
      const response = await http.get(apiConfig.endpoints.cart.get)
      return response.data
    },
    
    // 加入購物車
    async add(productId, quantity, attributeValueId) {
      const response = await http.post(apiConfig.endpoints.cart.add, {
        productId,
        quantity,
        attributeValueId
      })
      return response.data
    },
    
    // 更新購物車
    async update(cartItemId, quantity) {
      const response = await http.put(apiConfig.endpoints.cart.update, {
        cartItemId,
        quantity
      })
      return response.data
    },
    
    // 移除商品
    async remove(cartItemId) {
      const response = await http.delete(`${apiConfig.endpoints.cart.remove}/${cartItemId}`)
      return response.data
    }
  },

  // 結帳服務
  checkout: {
    // 驗證購物車
    async validate(memberId) {
      const response = await http.post(`${apiConfig.endpoints.checkout.validate}/${memberId}`)
      return response.data
    },
    
    // 獲取結帳摘要
    async getSummary(memberId, params = {}) {
      const response = await http.get(`${apiConfig.endpoints.checkout.summary}/${memberId}`, { params })
      return response.data
    },
    
    // 建立訂單
    async createOrder(orderData) {
      const response = await http.post(apiConfig.endpoints.checkout.createOrder, orderData)
      return response.data
    }
  },

  // 系統服務
  system: {
    // 健康檢查
    async healthCheck() {
      const response = await http.get(apiConfig.endpoints.health)
      return response.data
    }
  }
}

export default apiService
```

### 4. ?? Vue 組件使用範例

**登入組件範例 `src/views/Login.vue`**
```vue
<template>
  <div class="login-container">
    <form @submit.prevent="handleLogin" class="login-form">
      <h2>會員登入</h2>
      
      <div class="form-group">
        <input 
          v-model="loginForm.email" 
          type="email" 
          placeholder="電子郵件" 
          required 
        />
      </div>
      
      <div class="form-group">
        <input 
          v-model="loginForm.password" 
          type="password" 
          placeholder="密碼" 
          required 
        />
      </div>
      
      <button type="submit" :disabled="loading" class="login-btn">
        {{ loading ? '登入中...' : '登入' }}
      </button>
      
      <div v-if="error" class="error-message">
        {{ error }}
      </div>
    </form>
  </div>
</template>

<script>
import { apiService } from '@/services/apiService'

export default {
  name: 'Login',
  data() {
    return {
      loginForm: {
        email: '',
        password: ''
      },
      loading: false,
      error: null
    }
  },
  methods: {
    async handleLogin() {
      this.loading = true
      this.error = null
      
      try {
        const response = await apiService.auth.login(
          this.loginForm.email, 
          this.loginForm.password
        )
        
        if (response.success) {
          // 儲存 token 和用戶資訊
          localStorage.setItem('auth_token', response.data.token)
          localStorage.setItem('user_info', JSON.stringify(response.data.user))
          
          // 重導向到首頁
          this.$router.push('/')
          
          this.$toast.success('登入成功！')
        } else {
          this.error = response.message || '登入失敗'
        }
      } catch (error) {
        this.error = error.response?.data?.message || '登入時發生錯誤'
        console.error('登入錯誤:', error)
      } finally {
        this.loading = false
      }
    }
  }
}
</script>
```

**購物車組件範例 `src/views/Cart.vue`**
```vue
<template>
  <div class="cart-container">
    <h2>購物車</h2>
    
    <div v-if="loading" class="loading">
      載入中...
    </div>
    
    <div v-else-if="cartItems.length === 0" class="empty-cart">
      購物車是空的
    </div>
    
    <div v-else>
      <div v-for="item in cartItems" :key="item.id" class="cart-item">
        <img :src="item.product.imageUrl" :alt="item.product.name" />
        <div class="item-info">
          <h3>{{ item.product.name }}</h3>
          <p>價格: ${{ item.price }}</p>
          <div class="quantity-controls">
            <button @click="updateQuantity(item.id, item.quantity - 1)">-</button>
            <span>{{ item.quantity }}</span>
            <button @click="updateQuantity(item.id, item.quantity + 1)">+</button>
          </div>
        </div>
        <button @click="removeItem(item.id)" class="remove-btn">移除</button>
      </div>
      
      <div class="cart-summary">
        <p>總計: ${{ totalAmount }}</p>
        <button @click="goToCheckout" class="checkout-btn">結帳</button>
      </div>
    </div>
  </div>
</template>

<script>
import { apiService } from '@/services/apiService'

export default {
  name: 'Cart',
  data() {
    return {
      cartItems: [],
      loading: false
    }
  },
  computed: {
    totalAmount() {
      return this.cartItems.reduce((total, item) => {
        return total + (item.price * item.quantity)
      }, 0)
    }
  },
  async mounted() {
    await this.loadCart()
  },
  methods: {
    async loadCart() {
      this.loading = true
      try {
        const response = await apiService.cart.get()
        if (response.success) {
          this.cartItems = response.data.items || []
        }
      } catch (error) {
        console.error('載入購物車失敗:', error)
        this.$toast.error('載入購物車失敗')
      } finally {
        this.loading = false
      }
    },
    
    async updateQuantity(cartItemId, newQuantity) {
      if (newQuantity < 1) return
      
      try {
        const response = await apiService.cart.update(cartItemId, newQuantity)
        if (response.success) {
          await this.loadCart() // 重新載入購物車
        }
      } catch (error) {
        console.error('更新數量失敗:', error)
        this.$toast.error('更新失敗')
      }
    },
    
    async removeItem(cartItemId) {
      try {
        const response = await apiService.cart.remove(cartItemId)
        if (response.success) {
          await this.loadCart() // 重新載入購物車
          this.$toast.success('商品已移除')
        }
      } catch (error) {
        console.error('移除商品失敗:', error)
        this.$toast.error('移除失敗')
      }
    },
    
    goToCheckout() {
      this.$router.push('/checkout')
    }
  }
}
</script>
```

### 5. ?? 狀態管理 (Vuex/Pinia)

**使用 Pinia 的範例 `src/stores/auth.js`**
```javascript
import { defineStore } from 'pinia'
import { apiService } from '@/services/apiService'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    user: null,
    token: null,
    isAuthenticated: false
  }),
  
  getters: {
    isLoggedIn: (state) => !!state.token && !!state.user
  },
  
  actions: {
    // 初始化 - 從 localStorage 恢復狀態
    initialize() {
      const token = localStorage.getItem('auth_token')
      const userInfo = localStorage.getItem('user_info')
      
      if (token && userInfo) {
        this.token = token
        this.user = JSON.parse(userInfo)
        this.isAuthenticated = true
      }
    },
    
    // 登入
    async login(email, password) {
      try {
        const response = await apiService.auth.login(email, password)
        
        if (response.success) {
          this.token = response.data.token
          this.user = response.data.user
          this.isAuthenticated = true
          
          // 保存到 localStorage
          localStorage.setItem('auth_token', this.token)
          localStorage.setItem('user_info', JSON.stringify(this.user))
          
          return { success: true }
        } else {
          return { success: false, message: response.message }
        }
      } catch (error) {
        return { 
          success: false, 
          message: error.response?.data?.message || '登入失敗' 
        }
      }
    },
    
    // 登出
    logout() {
      this.user = null
      this.token = null
      this.isAuthenticated = false
      
      localStorage.removeItem('auth_token')
      localStorage.removeItem('user_info')
    }
  }
})
```

## ?? 部署後的更新步驟

### 1. 獲取 Railway API URL
部署完成後，您會獲得一個類似這樣的 URL：
```
https://your-app-name.up.railway.app
```

### 2. 更新前端配置
在 `src/config/api.js` 中更新 production baseURL：
```javascript
production: {
  baseURL: 'https://your-actual-railway-url.up.railway.app', // ?? 更新為實際 URL
  timeout: 30000
}
```

### 3. 測試 API 連接
```javascript
// 在瀏覽器控制台測試
fetch('https://your-railway-url.up.railway.app/health')
  .then(response => response.json())
  .then(data => console.log('API 狀態:', data))
```

## ?? 完整整合檢查清單

部署完成後，請檢查：

- [ ] API 健康檢查正常
- [ ] CORS 允許前端域名
- [ ] JWT 認證正常運作
- [ ] 會員登入/註冊功能
- [ ] 購物車 CRUD 操作
- [ ] 商品瀏覽功能
- [ ] 結帳流程
- [ ] 錯誤處理正常

## ?? 恭喜！

完成這些步驟後，您的 Vue.js 前端就可以完美地與 Railway 部署的 API 配合工作了！

您的作品集網站 `https://moonlit-klepon-a78f8c.netlify.app` 將擁有完整的電商功能。