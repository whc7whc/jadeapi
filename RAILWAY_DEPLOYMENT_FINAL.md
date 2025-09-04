# ?? Railway ���p�̲׫��n

## ? �Ҧ����D�w�����״_

�g�L�����״_�A�H�U���D�w�g�ѨM�G

### ?? **�״_���s�X���D**
- ? �״_ `CouponDtos.cs` �����s�X���~
- ? �״_ `VendorAuthDtos.cs` �����s�X���~
- ? �״_ `PointsService.cs` ��������r��
- ? �״_ `MembersController.cs` �����s�X���~
- ? �״_ `MembershipLevelPublicService.cs` �����s�X���~
- ? �״_ `SellerReportsController.cs` �����y�k���~
- ? �״_ `api-test-tool.html` ����HTML�y�k���D

### ?? **�ѨM���sĶ���~**
- ? CS1010: Newline in constant - �Ҧ�����r�Ťw����
- ? CS1009: Unrecognized escape sequence - �Ҧ��ϱק����D�w�״_
- ? CS1002, CS1003, CS1026: �y�k���~ - �Ҧ��A���M�������D�w�״_
- ? CS1022: Type or namespace definition - �j�A���t����D�w�״_

### ?? **Docker �M�����u��**
- ? �]�w `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`
- ? �u�� Dockerfile �c�جy�{
- ? �T�O UTF-8 �s�X�ۮe��
- ? ���a�c�ش��ճq�L

## ?? **�ߧY���p�� Railway**

### **�Ĥ@�B�G���e�̲׭״_**
```bash
git add .
git commit -m "?? FINAL: All encoding issues fixed, ready for Railway deployment"
git push origin main
```

### **�ĤG�B�GRailway ���p**
1. �e�� [Railway](https://railway.app)
2. �I�� "New Project"
3. ��� "Deploy from GitHub repo"
4. ��ܱz���ܮw
5. Railway �|�۰��˴� Dockerfile �ö}�l�c��

### **�ĤT�B�G�t�m�����ܼ�**
�b Railway �M�׳]�w���K�[�H�U�����ܼơG

```bash
# ���ε{������
ASPNETCORE_ENVIRONMENT=Production

# ��Ʈw�s��
ConnectionStrings__DefaultConnection=Server=tcp:jadepej-dbserver.database.windows.net,1433;Initial Catalog=jadepej-dbserver-new;Persist Security Info=False;User ID=team4;Password=#Gogojade;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;

# �����x�s
Cloudinary__CloudName=jadetainan
Cloudinary__ApiKey=384776688611428
Cloudinary__ApiSecret=4dSdNavAr96WmP0vO_wJL8TkbTU

# JWT �]�w
Jwt__Key=YourSuperSecretKeyThatIsLongAndComplex_123!@#
Jwt__Issuer=https://your-railway-url.up.railway.app
Jwt__Audience=https://moonlit-klepon-a78f8c.netlify.app

# �l��A��
SmtpSettings__User=jade0905jade@gmail.com
SmtpSettings__Pass=nsuragycwfiolqpc
SmtpSettings__FromEmail=jade0905jade@gmail.com

# Google OAuth
Google__ClientId=905313427248-3vg0kd6474kbaif9ujg41n7376ua8ajp.apps.googleusercontent.com
```

### **�ĥ|�B�G��s�e�ݳ]�w**
���p���\��A��s�z�� Netlify �e�ݡG
1. ��� Railway ���t�� URL�]�Ҧp�G`https://your-app-name.up.railway.app`�^
2. �b�e�ݥN�X����s API ��¦ URL
3. ���s���p�e��

### **�Ĥ��B�G���ճ��p**
�ϥΧ�s�᪺ `api-test-tool.html`�G
1. �N API URL ��s�� Railway URL
2. ���氷�d�ˬd����
3. ���� CORS �]�w
4. ���� Swagger ����
5. �ˬd�Ҧ� API ���I

## ?? **�w�����G**

���p���\��A�z���Ӭݨ�G

### ? **Railway ����x**
- �c�ئ��\�]���Ŀ�^
- ���ε{���B�椤
- �L���~��x
- ���d�ˬd�q�L

### ? **API �\��**
- `/health` ���I�^�����`
- `/swagger` ���ɥi�X��
- �Ҧ�������`�u�@
- ��Ʈw�s�����\

### ? **�e�ݾ�X**
- Netlify �e�ݥi���`�ե� API
- CORS �]�w���T
- �L�����~
- �Ҧ��\�ॿ�`

## ?? **�G�ٱư�**

### �p�G�c�إ��ѡG
1. �ˬd Railway �c�ؤ�x
2. �T�{�Ҧ����w���e
3. ���� Dockerfile �y�k

### �p�G���ε{���L�k�ҰʡG
1. �ˬd�����ܼƳ]�w
2. ���Ҹ�Ʈw�s���r��
3. �d�����ε{����x

### �p�G CORS ���D�G
1. �T�{�e�ݰ�W�b JWT Audience ��
2. �ˬd CORS ������]�w
3. ���� API URL �榡

## ?? **���\�з�**

��z�ݨ�H�U���p�ɡA���p�N�������\�F�G

- ? Railway ��� "Deployed" ���A
- ? API ���d�ˬd�^�� 200 OK
- ? Swagger UI �i���`�X��
- ? �e�ݥi���\�ե� API
- ? �Ҧ��q�ӥ\�ॿ�`�B�@

## ?? **�����I**

�z�����ݹq�ӥ��x�{�b�w�g�G
- **���������F�s�X���D**
- **���\���p�춳�ݥ��x**
- **�ǳƦn�i�ܵ���b���D**
- **��Ƨ��㪺�@�~������**

�o�O�@�ӥ]�t�e�� (Netlify) + ��� (Railway) + ��Ʈw (Azure SQL) �����㶳�ݬ[�c�I

���z���p���Q�I???