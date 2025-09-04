# ??? Team API - �q�ӫ�ݪA��

�o�O�@�ӥ\�৹�㪺 .NET 8 Web API �M�סA���q�ӥ��x���ѫ�ݪA�ȡA�䴩�ʪ����B���b�B�|���t�ΡB�u�f�鵥�֤ߥ\��C

## ? �D�n�\��

- ?? **�|���t��** - ���U�B�n�J�BJWT �{��
- ?? **�ʪ���** - �ӫ~�[�J�B�����B�ƶq�վ�
- ?? **���b�t��** - ���㪺���b�y�{�A�䴩�u�f��M�I��
- ??? **�u�f��t��** - �馩�X���ҩM�޲z
- ?? **�I�ƨt��** - �|���I�Ʋֿn�M�覩
- ?? **�q��޲z** - �q��إߡB���A�l��
- ?? **���y��X** - ��ɤ�I��X
- ?? **�Ϥ��W��** - Cloudinary �����x�s
- ?? **�l��A��** - SMTP �l��q��

## ?? �ֳt�}�l

### ���a�}�o

1. **�J���M��**
   ```bash
   git clone <your-repo-url>
   cd Team.API
   ```

2. **�٭�M��**
   ```bash
   dotnet restore
   ```

3. **�]�w��Ʈw�s��**
   ��s `appsettings.Development.json` �����s���r��

4. **�B��M��**
   ```bash
   dotnet run
   ```

5. **�X�� API**
   - API: `https://localhost:7106`
   - Swagger: `https://localhost:7106/swagger`

## ?? ���ݳ��p

### �@�䳡�p�� Railway

1. **���e�N�X�� GitHub**
2. **�s�� Railway**
   - �X�� [Railway.app](https://railway.app)
   - �Ыطs���بós�� GitHub �ܮw
3. **�t�m�����ܼ�** (�Ѧ� `DEPLOYMENT_GUIDE.md`)
4. **�۰ʳ��p����**

### �ֳt���ճ��p

**Windows:**
```bash
deploy.bat
```

**Linux/macOS:**
```bash
chmod +x deploy.sh
./deploy.sh
```

## ?? ���Ұt�m

### ���n�����ܼ�

```env
# ��Ʈw
ConnectionStrings__DefaultConnection=your_database_connection

# JWT
JWT_SECRET_KEY=your_secret_key
JWT_ISSUER=your_api_url

# Cloudinary
CLOUDINARY_CLOUD_NAME=your_cloud_name
CLOUDINARY_API_KEY=your_api_key
CLOUDINARY_API_SECRET=your_api_secret

# �l��A��
SMTP_USER=your_email
SMTP_PASS=your_password
```

## ?? API ����

���p��i�q�L�H�U���I�X�ݡG

- **���d�ˬd**: `GET /health`
- **API ����**: `GET /swagger`
- **�|�� API**: `GET /api/members`
- **�ʪ��� API**: `GET /api/cart`
- **���b API**: `GET /api/checkout`

## ?? �e�ݾ�X

�� API �w�t�m CORS �H�䴩�H�U�e�ݡG
- `https://moonlit-klepon-a78f8c.netlify.app` (�z�� Vue.js �@�~��)
- `http://localhost:3000` (���a�}�o)

### �e�ݨϥνd��

```javascript
// �]�w API ��¦ URL
const API_BASE_URL = 'https://your-api.railway.app'

// �|���n�J
const login = async (email, password) => {
  const response = await fetch(`${API_BASE_URL}/api/members/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  })
  return response.json()
}
```

## ??? �޳N�[�c

- **�ج[**: ASP.NET Core 8.0
- **��Ʈw**: Azure SQL Database
- **�{��**: JWT Bearer Token
- **�Ϥ��x�s**: Cloudinary
- **�l��A��**: Gmail SMTP
- **��I**: ��� ECPay
- **���p**: Docker + Railway

## ?? �M�׵��c

```
Team.API/
�u�w�w Controllers/          # API ���
�u�w�w Models/              # ��Ƽҫ�
�u�w�w Services/            # �~���޿�A��
�u�w�w DTOs/               # ��ƶǿ骫��
�u�w�w Payments/           # ��I��X
�u�w�w wwwroot/            # �R�A�ɮ�
�|�w�w Program.cs          # ���ε{���i�J�I
```

## ?? �w����

- ? JWT �{�ұ��v
- ? HTTPS �j��ϥ�
- ? CORS ���O�@
- ? ��J����
- ? SQL �`�J���@
- ? �����ܼƫO�@�ӷP��T

## ?? �ʱ��P��x

- ���ε{�����d�ˬd���I
- ���c�Ƥ�x�O��
- ���~�B�z�M�^��
- �į�ʱ�

## ?? �^�m

�w�ﴣ�� Issue �M Pull Request�I

## ?? ���v

���M�׶ȨѾǲߩM�@�~���i�ܨϥΡC

---

**?? �� API �w�ǳƦn���z���q�ӫe�ݴ��ѱj�j����ݤ䴩�I**