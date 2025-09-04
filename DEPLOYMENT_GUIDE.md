# ?? API ���ݳ��p���n (�w�ץ��s�X���D)

�z�� Team API �w�g�ץ��÷ǳƦn���p�춳�ݥ��x�F�I

## ?? ���D�ץ�

**Railway ���p���ѭ�]**: 
1. ~~`railway.toml` ���榡���~~~ ? �w�ץ�
2. **����r�Žs�X���D** ? �w�ץ�

**�ѨM���**: 
1. Railway �|�۰��˴� Dockerfile�A���ݭn railway.toml ���
2. �״_�F�Ҧ����N�X��������r�Žs�X���D
3. �K�[�F UTF-8 �����ܼƨ� Dockerfile

## ?? ���˥��x

### 1. Railway (����) ?
- **�K�O�B��**: 500�p��/��
- **�u�I**: �۰��˴� Dockerfile�A�䴩 GitHub �۰ʳ��p
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

#### �B�J 1: ���e�ץ��᪺�N�Xgit add .
git commit -m "Fix encoding issues and optimize for Railway deployment"
git push origin main
#### �B�J 2: �Ыطs����
1. �n�J Railway
2. �I�� "New Project"
3. ��� "Deploy from GitHub repo"
4. ��ܱz���ܮw

#### �B�J 3: ���ݦ۰��˴�
Railway �|�۰ʡG
- �˴��� Dockerfile
- �}�l�c�� Docker �M��
- �۰ʳ]�w�ݤf

#### �B�J 4: �t�m�����ܼ�
�b Railway �M�׳]�w �� Variables ���A�K�[�H�U�����ܼơG
# �򥻰t�m (PORT �|�۰ʳ]�w�A���ݭn��ʲK�[)
ASPNETCORE_ENVIRONMENT=Production

# ��Ʈw�s���r�� (�z�w���� Azure SQL)
ConnectionStrings__DefaultConnection=Server=tcp:jadepej-dbserver.database.windows.net,1433;Initial Catalog=jadepej-dbserver-new;Persist Security Info=False;User ID=team4;Password=#Gogojade;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;

# Cloudinary �]�w
Cloudinary__CloudName=jadetainan
Cloudinary__ApiKey=384776688611428
Cloudinary__ApiSecret=4dSdNavAr96WmP0vO_wJL8TkbTU

# SMTP �]�w
SmtpSettings__User=jade0905jade@gmail.com
SmtpSettings__Pass=nsuragycwfiolqpc
SmtpSettings__FromEmail=jade0905jade@gmail.com

# Google OAuth
Google__ClientId=905313427248-3vg0kd6474kbaif9ujg41n7376ua8ajp.apps.googleusercontent.com

# JWT �]�w (���p���s Issuer ����� URL)
Jwt__Key=YourSuperSecretKeyThatIsLongAndComplex_123!@#
Jwt__Issuer=https://your-railway-domain.up.railway.app
Jwt__Audience=https://moonlit-klepon-a78f8c.netlify.app

# ��ɪ��y (�p�ݭn)
Ecpay__MerchantID=your_merchant_id
Ecpay__HashKey=your_hash_key
Ecpay__HashIV=your_hash_iv
Ecpay__BaseUrl=https://payment-stage.ecpay.com.tw
#### �B�J 5: ���p����
1. Railway �|�۰ʺc�بó��p
2. ���ݳ��p�����]�q�` 5-10 �����^
3. ����z�� API URL�]�Ҧp�G`https://your-app-name.up.railway.app`�^

## ?? �e�ݾ�X

### ��s�e�� API ��¦ URL
�N�z Vue.js �e�ݪ� API ��¦ URL ��s�� Railway ���p�� URL�G
// �b�z�� Vue.js �t�m��
const API_BASE_URL = 'https://your-app-name.up.railway.app'

// �Φb�����ܼƤ�
VUE_APP_API_URL=https://your-app-name.up.railway.app
### ��s JWT Issuer
���p������A�O�o�^�� Railway Variables ��s�GJwt__Issuer=https://your-actual-railway-url.up.railway.app
## ?? ���ճ��p

### 1. ���d�ˬdcurl https://your-app-name.up.railway.app/health
�w���^���G
{
  "status": "Healthy",
  "timestamp": "2024-01-01T00:00:00.000Z",
  "version": "1.0.0",
  "environment": "Production"
}
### 2. API ����
�X�ݡG`https://your-app-name.up.railway.app/swagger`

### 3. ���ծں��Icurl https://your-app-name.up.railway.app/
## ?? �ʱ��P���@

### Railway ����x
- **Build Logs**: �d�ݺc�عL�{
- **Deploy Logs**: �d�ݳ��p��x
- **Application Logs**: �d�ݹB��ɤ�x
- **Metrics**: �ʱ� CPU�B���s�B�����ϥα��p

### ��x�d��
�b Railway ����x���i�H�d�ݡG
- �c�ؤ�x�]�ˬd Docker �c�عL�{�^
- ���ε{���Ұʤ�x
- HTTP �ШD��x
- ���~��x

## ?? �G�ٱư�

### �`�����D�P�ѨM���

#### 1. �c�إ��� - �s�X���~
**�g��**: CS1009, CS1002, CS1010 ���sĶ���~
**�ѨM**: 
- ? �w�״_�Ҧ�����r�Žs�X���D
- ? �K�[�F UTF-8 �����ܼƨ� Dockerfile
- �ˬd�O�_����L�S��r��

#### 2. ���ε{���L�k�Ұ�
**�g��**: Deploy ���q���ѡA���ε{���Y��
**�ѨM**:
- �ˬd�����ܼưt�m
- �d�� Application Logs
- ���Ҹ�Ʈw�s���r��

#### 3. �L�k�X�� API
**�g��**: 502 Bad Gateway �γs���W��
**�ѨM**:
- �T�{���ε{����ť���T�ݤf
- �ˬd���d�ˬd���I
- �d�ݨ�����]�w

#### 4. CORS ���~
**�g��**: �e�ݵL�k�ե� API
**�ѨM**:
- �T�{�e�ݰ�W�b CORS ���\�C��
- �ˬd CORS �����n��t�m

### �����B�J
1. �d�� Railway Build Logs
2. �d�� Railway Deploy Logs  
3. �ˬd�����ܼưt�m
4. ���հ��d�ˬd���I: `/health`
5. ���ծں��I: `/`
6. ���Ҹ�Ʈw�s��

## ?? �w�����ˬd�M��

���p������A�нT�{�G

- [ ] �Ҧ��ӷP��T���ϥ������ܼ�
- [ ] JWT �K�_�w��s���j�K�X
- [ ] ��Ʈw�s���ϥΥ[�K�s��
- [ ] CORS �u���\�H������W
- [ ] Swagger ���ɤw�A��O�@�]�i��^
- [ ] �S���w�s�X������r��

## ? ���p�����ˬd�M��

- [ ] Railway �c�ئ��\�]�L�s�X���~�^
- [ ] ���ε{�����\�Ұ�
- [ ] ���d�ˬd���I���` (`/health`)
- [ ] �ں��I���` (`/`)
- [ ] Swagger ���ɥi�X�� (`/swagger`)
- [ ] ��Ʈw�s�����`
- [ ] �e�ݥi�H���\�ե� API
- [ ] JWT �{�ҥ��`�B�@
- [ ] CORS �t�m���T
- [ ] �Ҧ������ܼƳ��w�]�w

## ?? ���ߡI

�z�� API �{�w���\�״_�s�X���D�÷ǳƳ��p�� Railway�I

**�״_�����D**:
- ? �����F�Ҧ�����r�Žs�X���D
- ? �u�ƤF Dockerfile �H�䴩 UTF-8
- ? �����F�����D�� railway.toml ���
- ? �T�O�Ҧ����N�X�ϥέ^�����

**�U�@�B**:
1. ���e�ץ��᪺�N�X�� GitHub
2. �b Railway ���s�Ыض���
3. �t�m�����ܼ�
4. ���ճ��p�᪺ API
5. ��s�e�� Vue.js ���ε{���� API URL

---

**�嫬�� Railway ���p URL �榡**:
- API ��¦ URL: `https://your-app-name.up.railway.app`
- ���d�ˬd: `https://your-app-name.up.railway.app/health`  
- API ����: `https://your-app-name.up.railway.app/swagger`
- API ���I: `https://your-app-name.up.railway.app/api/...`

�O�o�N�s�� API URL ��s��z���e�����ε{���t�m���I