# ?? �e�� Vue.js ��X���n

## ?? �z�� API �t�m���A

? **CORS �w���T�t�m** - �䴩�z�� Netlify ����
? **JWT �w�]�w** - Audience ���V�z���e�ݰ�W
? **���d�ˬd���I** - �i�Ω�ʱ� API ���A

## ?? API ���p�����᪺�e�ݾ�X�B�J

### 1. ?? �]�w API ��¦ URL

�b�z�� Vue.js �M�פ��إ� API �t�m�ɮסG

**�Ы� `src/config/api.js`**
```javascript
// API �t�m
const API_CONFIG = {
  // �}�o���� (���a)
  development: {
    baseURL: 'https://localhost:7106',
    timeout: 30000
  },
  
  // �Ͳ����� (Railway ���p�᪺ URL)
  production: {
    baseURL: 'https://your-app-name.up.railway.app',  // ?? ���p���s�� URL
    timeout: 30000
  }
}

// �ھ����Ҧ۰ʿ��
const currentConfig = API_CONFIG[process.env.NODE_ENV] || API_CONFIG.production

export default {
  baseURL: currentConfig.baseURL,
  timeout: currentConfig.timeout,
  
  // API ���I
  endpoints: {
    // �|������
    auth: {
      login: '/api/members/login',
      register: '/api/members/register',
      profile: '/api/members/profile',
      googleLogin: '/api/members/google-login'
    },
    
    // �ӫ~����
    products: {
      list: '/api/products',
      detail: '/api/products',
      categories: '/api/categories'
    },
    
    // �ʪ�������
    cart: {
      get: '/api/cart',
      add: '/api/cart/add',
      update: '/api/cart/update',
      remove: '/api/cart/remove',
      clear: '/api/cart/clear'
    },
    
    // ���b����
    checkout: {
      validate: '/api/checkout/validate',
      summary: '/api/checkout/summary',
      createOrder: '/api/checkout/create-order',
      paymentMethods: '/api/checkout/payment-methods'
    },
    
    // �u�f�����
    coupons: {
      validate: '/api/checkout/validate-coupon',
      list: '/api/coupons'
    },
    
    // �t�Ϊ��A
    health: '/health'
  }
}
```

### 2. ?? �]�w Axios �d�I��

**�Ы� `src/utils/http.js`**
```javascript
import axios from 'axios'
import apiConfig from '@/config/api'

// �Ы� axios ���
const http = axios.create({
  baseURL: apiConfig.baseURL,
  timeout: apiConfig.timeout,
  headers: {
    'Content-Type': 'application/json'
  }
})

// �ШD�d�I�� - �۰ʲK�[ JWT Token
http.interceptors.request.use(
  (config) => {
    // �q localStorage ��� token
    const token = localStorage.getItem('auth_token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    
    console.log(`?? API �ШD: ${config.method?.toUpperCase()} ${config.url}`)
    return config
  },
  (error) => {
    console.error('? �ШD���~:', error)
    return Promise.reject(error)
  }
)

// �^���d�I�� - �B�z���~�M token �L��
http.interceptors.response.use(
  (response) => {
    console.log(`? API �^��: ${response.config.url} - ${response.status}`)
    return response
  },
  (error) => {
    console.error('? API ���~:', error.response?.data || error.message)
    
    // Token �L���B�z
    if (error.response?.status === 401) {
      localStorage.removeItem('auth_token')
      localStorage.removeItem('user_info')
      
      // ���ɦV��n�J����
      window.location.href = '/login'
    }
    
    return Promise.reject(error)
  }
)

export default http
```

### 3. ??? �Ы� API �A��

**�Ы� `src/services/apiService.js`**
```javascript
import http from '@/utils/http'
import apiConfig from '@/config/api'

export const apiService = {
  // �|���A��
  auth: {
    // �n�J
    async login(email, password) {
      const response = await http.post(apiConfig.endpoints.auth.login, {
        email,
        password
      })
      return response.data
    },
    
    // ���U
    async register(userData) {
      const response = await http.post(apiConfig.endpoints.auth.register, userData)
      return response.data
    },
    
    // Google �n�J
    async googleLogin(googleToken) {
      const response = await http.post(apiConfig.endpoints.auth.googleLogin, {
        token: googleToken
      })
      return response.data
    },
    
    // ����|�����
    async getProfile() {
      const response = await http.get(apiConfig.endpoints.auth.profile)
      return response.data
    }
  },

  // �ӫ~�A��
  products: {
    // ����ӫ~�C��
    async getList(params = {}) {
      const response = await http.get(apiConfig.endpoints.products.list, { params })
      return response.data
    },
    
    // ����ӫ~�Ա�
    async getDetail(productId) {
      const response = await http.get(`${apiConfig.endpoints.products.detail}/${productId}`)
      return response.data
    }
  },

  // �ʪ����A��
  cart: {
    // ����ʪ���
    async get() {
      const response = await http.get(apiConfig.endpoints.cart.get)
      return response.data
    },
    
    // �[�J�ʪ���
    async add(productId, quantity, attributeValueId) {
      const response = await http.post(apiConfig.endpoints.cart.add, {
        productId,
        quantity,
        attributeValueId
      })
      return response.data
    },
    
    // ��s�ʪ���
    async update(cartItemId, quantity) {
      const response = await http.put(apiConfig.endpoints.cart.update, {
        cartItemId,
        quantity
      })
      return response.data
    },
    
    // �����ӫ~
    async remove(cartItemId) {
      const response = await http.delete(`${apiConfig.endpoints.cart.remove}/${cartItemId}`)
      return response.data
    }
  },

  // ���b�A��
  checkout: {
    // �����ʪ���
    async validate(memberId) {
      const response = await http.post(`${apiConfig.endpoints.checkout.validate}/${memberId}`)
      return response.data
    },
    
    // ������b�K�n
    async getSummary(memberId, params = {}) {
      const response = await http.get(`${apiConfig.endpoints.checkout.summary}/${memberId}`, { params })
      return response.data
    },
    
    // �إ߭q��
    async createOrder(orderData) {
      const response = await http.post(apiConfig.endpoints.checkout.createOrder, orderData)
      return response.data
    }
  },

  // �t�ΪA��
  system: {
    // ���d�ˬd
    async healthCheck() {
      const response = await http.get(apiConfig.endpoints.health)
      return response.data
    }
  }
}

export default apiService
```

### 4. ?? Vue �ե�ϥνd��

**�n�J�ե�d�� `src/views/Login.vue`**
```vue
<template>
  <div class="login-container">
    <form @submit.prevent="handleLogin" class="login-form">
      <h2>�|���n�J</h2>
      
      <div class="form-group">
        <input 
          v-model="loginForm.email" 
          type="email" 
          placeholder="�q�l�l��" 
          required 
        />
      </div>
      
      <div class="form-group">
        <input 
          v-model="loginForm.password" 
          type="password" 
          placeholder="�K�X" 
          required 
        />
      </div>
      
      <button type="submit" :disabled="loading" class="login-btn">
        {{ loading ? '�n�J��...' : '�n�J' }}
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
          // �x�s token �M�Τ��T
          localStorage.setItem('auth_token', response.data.token)
          localStorage.setItem('user_info', JSON.stringify(response.data.user))
          
          // ���ɦV�쭺��
          this.$router.push('/')
          
          this.$toast.success('�n�J���\�I')
        } else {
          this.error = response.message || '�n�J����'
        }
      } catch (error) {
        this.error = error.response?.data?.message || '�n�J�ɵo�Ϳ��~'
        console.error('�n�J���~:', error)
      } finally {
        this.loading = false
      }
    }
  }
}
</script>
```

**�ʪ����ե�d�� `src/views/Cart.vue`**
```vue
<template>
  <div class="cart-container">
    <h2>�ʪ���</h2>
    
    <div v-if="loading" class="loading">
      ���J��...
    </div>
    
    <div v-else-if="cartItems.length === 0" class="empty-cart">
      �ʪ����O�Ū�
    </div>
    
    <div v-else>
      <div v-for="item in cartItems" :key="item.id" class="cart-item">
        <img :src="item.product.imageUrl" :alt="item.product.name" />
        <div class="item-info">
          <h3>{{ item.product.name }}</h3>
          <p>����: ${{ item.price }}</p>
          <div class="quantity-controls">
            <button @click="updateQuantity(item.id, item.quantity - 1)">-</button>
            <span>{{ item.quantity }}</span>
            <button @click="updateQuantity(item.id, item.quantity + 1)">+</button>
          </div>
        </div>
        <button @click="removeItem(item.id)" class="remove-btn">����</button>
      </div>
      
      <div class="cart-summary">
        <p>�`�p: ${{ totalAmount }}</p>
        <button @click="goToCheckout" class="checkout-btn">���b</button>
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
        console.error('���J�ʪ�������:', error)
        this.$toast.error('���J�ʪ�������')
      } finally {
        this.loading = false
      }
    },
    
    async updateQuantity(cartItemId, newQuantity) {
      if (newQuantity < 1) return
      
      try {
        const response = await apiService.cart.update(cartItemId, newQuantity)
        if (response.success) {
          await this.loadCart() // ���s���J�ʪ���
        }
      } catch (error) {
        console.error('��s�ƶq����:', error)
        this.$toast.error('��s����')
      }
    },
    
    async removeItem(cartItemId) {
      try {
        const response = await apiService.cart.remove(cartItemId)
        if (response.success) {
          await this.loadCart() // ���s���J�ʪ���
          this.$toast.success('�ӫ~�w����')
        }
      } catch (error) {
        console.error('�����ӫ~����:', error)
        this.$toast.error('��������')
      }
    },
    
    goToCheckout() {
      this.$router.push('/checkout')
    }
  }
}
</script>
```

### 5. ?? ���A�޲z (Vuex/Pinia)

**�ϥ� Pinia ���d�� `src/stores/auth.js`**
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
    // ��l�� - �q localStorage ��_���A
    initialize() {
      const token = localStorage.getItem('auth_token')
      const userInfo = localStorage.getItem('user_info')
      
      if (token && userInfo) {
        this.token = token
        this.user = JSON.parse(userInfo)
        this.isAuthenticated = true
      }
    },
    
    // �n�J
    async login(email, password) {
      try {
        const response = await apiService.auth.login(email, password)
        
        if (response.success) {
          this.token = response.data.token
          this.user = response.data.user
          this.isAuthenticated = true
          
          // �O�s�� localStorage
          localStorage.setItem('auth_token', this.token)
          localStorage.setItem('user_info', JSON.stringify(this.user))
          
          return { success: true }
        } else {
          return { success: false, message: response.message }
        }
      } catch (error) {
        return { 
          success: false, 
          message: error.response?.data?.message || '�n�J����' 
        }
      }
    },
    
    // �n�X
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

## ?? ���p�᪺��s�B�J

### 1. ��� Railway API URL
���p������A�z�|��o�@�������o�˪� URL�G
```
https://your-app-name.up.railway.app
```

### 2. ��s�e�ݰt�m
�b `src/config/api.js` ����s production baseURL�G
```javascript
production: {
  baseURL: 'https://your-actual-railway-url.up.railway.app', // ?? ��s����� URL
  timeout: 30000
}
```

### 3. ���� API �s��
```javascript
// �b�s��������x����
fetch('https://your-railway-url.up.railway.app/health')
  .then(response => response.json())
  .then(data => console.log('API ���A:', data))
```

## ?? �����X�ˬd�M��

���p������A���ˬd�G

- [ ] API ���d�ˬd���`
- [ ] CORS ���\�e�ݰ�W
- [ ] JWT �{�ҥ��`�B�@
- [ ] �|���n�J/���U�\��
- [ ] �ʪ��� CRUD �ާ@
- [ ] �ӫ~�s���\��
- [ ] ���b�y�{
- [ ] ���~�B�z���`

## ?? ���ߡI

�����o�ǨB�J��A�z�� Vue.js �e�ݴN�i�H�����a�P Railway ���p�� API �t�X�u�@�F�I

�z���@�~������ `https://moonlit-klepon-a78f8c.netlify.app` �N�֦����㪺�q�ӥ\��C