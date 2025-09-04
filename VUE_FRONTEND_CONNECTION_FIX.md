# ?? Vue �e�ݳs�� Railway API �״_���n

## ?? **�D�n���D**
�z�� Vue �e�ݵL�k�s���� Railway API ����]�G

### ? **�w�״_�����D**
1. **JWT �t�m���~** - `appsettings.json` ���� `Issuer` �M `Audience` �w��s�����T�� URL
2. **CORS �t�m** - �w�b `Program.cs` ���t�m�䴩�z�� Netlify ��W

### ?? **�ݭn�ˬd�� Vue �e�ݰt�m**

#### **1. API ��¦ URL �]�w**
�ˬd�z�� Vue �M�פ��� API ��¦ URL �O�_���T�G

```javascript
// �b�z�� Vue �M�פ��A��� API �t�m�ɮ�
// �i��b�H�U��m�G
// - src/config/api.js
// - src/utils/request.js  
// - src/api/index.js
// - .env �ɮ�

// �T�O API URL �]�w���G
const API_BASE_URL = 'https://jadeapi-production.up.railway.app'

// �Φb .env �ɮפ��G
VUE_APP_API_URL=https://jadeapi-production.up.railway.app
```

#### **2. Axios �� Fetch �t�m**
```javascript
// �p�G�ϥ� axios
import axios from 'axios'

const api = axios.create({
  baseURL: 'https://jadeapi-production.up.railway.app',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json'
  }
})

// �p�G�ݭn��a credentials
api.defaults.withCredentials = true
```

#### **3. �����ܼưt�m**
�b�z�� Vue �M�׮ڥؿ��ЫةΧ�s�o���ɮסG

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

## ?? **�ߧY�״_�B�J**

### **�Ĥ@�B�G���s���p Railway**
```bash
git add .
git commit -m "?? Fix JWT configuration for Railway deployment"
git push origin main
```

### **�ĤG�B�G��s Vue �e�ݰt�m**
1. �b�z�� Vue �M�פ���� API �t�m
2. �N API URL ��s���G`https://jadeapi-production.up.railway.app`
3. ���s�c�ةM���p�e��

### **�ĤT�B�G���ճs��**
�ϥΥH�U���եN�X�ˬd�s���G

```javascript
// �b Vue �M�פ����� API �s��
async function testAPIConnection() {
  try {
    const response = await fetch('https://jadeapi-production.up.railway.app/health')
    const data = await response.json()
    console.log('API �s�����\:', data)
  } catch (error) {
    console.error('API �s������:', error)
  }
}

// �b�s��������x����
testAPIConnection()
```

## ?? **�`�����D�E�_**

### **CORS ���~**
�p�G�ݨ� CORS ���~�A�ˬd�G
- API �� CORS �t�m�O�_�]�t�z���e�ݰ�W
- �e�ݽШD�O�_���T�]�w headers

### **404 ���~**  
�p�G API �^�� 404�G
- �ˬd API ���I���|�O�_���T
- �T�{ Railway ���p�O�_���\

### **500 ���~**
�p�G API �^�� 500�G
- �ˬd Railway ��x
- �T�{��Ʈw�s���O�_���`

## ?? **���㪺 Vue �M�� API �t�m�d��**

```javascript
// src/utils/request.js
import axios from 'axios'

// �ھ����ҳ]�w API URL
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

// �ШD�d�I��
request.interceptors.request.use(
  config => {
    // �K�[�{�� token
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

// �T���d�I��
request.interceptors.response.use(
  response => response,
  error => {
    console.error('API �ШD���~:', error)
    if (error.response?.status === 401) {
      // �B�z�{�ҥ���
      localStorage.removeItem('token')
      // ���w�V��n�J����
    }
    return Promise.reject(error)
  }
)

export default request
```

## ? **�״_�T�{�M��**

- [ ] Railway API JWT �t�m�w��s
- [ ] Vue �M�� API URL �w��s
- [ ] �e�ݤw���s�c�ةM���p
- [ ] CORS �]�w���T
- [ ] ���� API �s�����\
- [ ] �{�ҥ\�ॿ�`
- [ ] �Ҧ� API ���I�i���`�X��

## ?? **���\�з�**

��z�����״_��A���ӯ���G

1. ? �b�s��������x�ݨ즨�\�� API �ШD
2. ? �e�ݥi�H���`�n�J/���U  
3. ? �ӫ~�C��i�H���`���J
4. ? �ʪ����\�ॿ�`�B�@
5. ? �L CORS ���~

�����o�ǨB�J��A�z���������δN�৹���B��F�I??