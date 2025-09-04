# ?? API ���ݳ��p���n

�z�� Team API �w�g�ǳƦn���p�춳�ݥ��x�F�I�H�U�O�T�ӱ��˪��K�O���p�ﶵ�G

## ?? ���˥��x

### 1. Railway (����) ?
- **�K�O�B��**: 500�p��/��
- **�u�I**: ²����ΡA�䴩 GitHub �۰ʳ��p
- **�A�X**: �����B�檺 API �A��

### 2. Render
- **�K�O�B��**: 750�p��/��A���|��v
- **�u�I**: �R�A IP�A�}�n����x�\��
- **���I**: �K�O���|�b30�����L���ʫ��v

### 3. Azure App Service
- **�K�O�B��**: F1 �h�ŧK�O
- **�u�I**: Microsoft �ͺA�t�A�P Azure SQL ��X�}�n
- **���I**: �t�m������

## ?? Railway ���p�B�J (����)

### �ǳƤu�@
1. �Ы� [Railway](https://railway.app) �b��
2. �N�N�X���e�� GitHub �ܮw

### ���p�B�J

#### �B�J 1: �Ыطs����
1. �n�J Railway
2. �I�� "New Project"
3. ��� "Deploy from GitHub repo"
4. ��ܱz���ܮw

#### �B�J 2: �t�m�����ܼ�
�b Railway �M�׳]�w���A�K�[�H�U�����ܼơG

```bash
# �򥻰t�m
ASPNETCORE_ENVIRONMENT=Production
PORT=8080

# ��Ʈw�s���r�� (�z�w���� Azure SQL)
ConnectionStrings__DefaultConnection=Server=tcp:jadepej-dbserver.database.windows.net,1433;Initial Catalog=jadepej-dbserver-new;Persist Security Info=False;User ID=team4;Password=#Gogojade;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;

# Cloudinary �]�w
CLOUDINARY_CLOUD_NAME=jadetainan
CLOUDINARY_API_KEY=384776688611428
CLOUDINARY_API_SECRET=4dSdNavAr96WmP0vO_wJL8TkbTU

# SMTP �]�w
SMTP_USER=jade0905jade@gmail.com
SMTP_PASS=nsuragycwfiolqpc
SMTP_FROM_EMAIL=jade0905jade@gmail.com

# Google OAuth
GOOGLE_CLIENT_ID=905313427248-3vg0kd6474kbaif9ujg41n7376ua8ajp.apps.googleusercontent.com

# JWT �]�w
JWT_SECRET_KEY=YourSuperSecretKeyThatIsLongAndComplex_123!@#
JWT_ISSUER=https://your-railway-domain.railway.app

# ��ɪ��y (�p�ݭn)
ECPAY_MERCHANT_ID=your_merchant_id
ECPAY_HASH_KEY=your_hash_key
ECPAY_HASH_IV=your_hash_iv
ECPAY_BASE_URL=https://payment-stage.ecpay.com.tw
```

#### �B�J 3: ���p
1. Railway �|�۰��˴� Dockerfile �ö}�l�c��
2. ���ݳ��p�����]�q�` 3-5 �����^
3. ����z�� API URL�]�Ҧp�G`https://your-app-name.railway.app`�^

## ?? �e�ݾ�X

### ��s�e�� API ��¦ URL
�N�z Vue.js �e�ݪ� API ��¦ URL ��s�� Railway ���p�� URL�G

```javascript
// �b�z�� Vue.js �t�m��
const API_BASE_URL = 'https://your-app-name.railway.app'

// �Φb�����ܼƤ�
VUE_APP_API_URL=https://your-app-name.railway.app
```

### CORS �t�m�w����
API �w�t�m�����\�Ӧ۱z�� Netlify �������ШD�G
- `https://moonlit-klepon-a78f8c.netlify.app`

## ?? ���ճ��p

### 1. ���d�ˬd
```bash
curl https://your-app-name.railway.app/health
```

�w���^���G
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-01T00:00:00.000Z",
  "version": "1.0.0",
  "environment": "Production"
}
```

### 2. API ����
�X�ݡG`https://your-app-name.railway.app/swagger`

### 3. ���� API ���I
```bash
# ���շ|�����U�εn�J
curl -X POST https://your-app-name.railway.app/api/members/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!","name":"���եΤ�"}'
```

## ?? �ʱ��P���@

### Railway �ʱ�
- **Dashboard**: �d�� CPU�B���s�ϥα��p
- **Logs**: ��ɬd�����ε{����x
- **Metrics**: �ʱ��ШD�ƶq�M�^���ɶ�

### ��x�d��
�b Railway ����x���i�H�d�ݡG
- ���ε{���Ұʤ�x
- HTTP �ШD��x
- ���~��x

## ?? �w���ʫ�ĳ

### 1. �����ܼƦw��
- �Ҧ��ӷP��T���q�L�����ܼưt�m
- ���n�b�N�X���w�s�X�K�_

### 2. HTTPS
- Railway �۰ʴ��� HTTPS
- �T�O�e�ݥu�ϥ� HTTPS �ե� API

### 3. CORS �t�m
- �w�t�m���u���\�z���e�ݰ�W
- �w���ˬd�M��s���\����W

## ?? �u�ƫ�ĳ

### 1. �į��u��
- �Ҽ{�ҥ� API �T�����Y
- ��I�A���w�s����
- �ʱ���Ʈw�d�߮į�

### 2. ��������
- �ʱ� Railway �ϥήɶ�
- �Ҽ{�b���n�ɤɯŨ�I�O���
- �u�ƥN�X�H���C CPU �ϥβv

### 3. �ƥ�����
- �w���ƥ� Azure SQL ��Ʈw
- �O���N�X�ܮw�������

## ?? �G�ٱư�

### �`�����D
1. **���p����**: �ˬd Dockerfile �M�N�X�y�k
2. **��Ʈw�s������**: ���ҳs���r��M������]�w
3. **CORS ���~**: �T�{�e�ݰ�W�b���\�C��
4. **502/503 ���~**: �ˬd���ε{���O�_��ť���T�ݤf (8080)

### �����B�J
1. �d�� Railway ���p��x
2. �ˬd�����ܼưt�m
3. ���հ��d�ˬd���I
4. ���Ҹ�Ʈw�s��

## ? �����M��

���p������A�нT�{�G

- [ ] API ���d�ˬd���`
- [ ] Swagger ���ɥi�X��
- [ ] ��Ʈw�s�����`
- [ ] �e�ݥi�H���\�ե� API
- [ ] CORS �t�m���T
- [ ] �����ܼƳ��w�]�w
- [ ] ��x��ܥ��`�B��

## ?? ���ߡI

�z�� API �{�w���\���p�춳�ݡI�z���e�ݺ��� `https://moonlit-klepon-a78f8c.netlify.app` �{�b�i�H�ϥγ��p�� API �Ӯi�ܱz���@�~���F�C

---

**���p�᪺ API URL �ܨ�**:
- ���d�ˬd: `https://your-app.railway.app/health`
- API ����: `https://your-app.railway.app/swagger`
- API ���I: `https://your-app.railway.app/api/...`

�O�o�N�s�� API URL ��s��z���e�����ε{���t�m���I