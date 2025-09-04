# ?? Railway ���p�̲׽T�{

## ? �Ҧ����D�w�״_����

�ڤw�g�����F�H�U����״_�G

### ?? **Docker �c���u��**
- ? �����Ҧ���������M�r��
- ? �]�w `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`
- ? ²�� Dockerfile �� Railway �M�Ϊ���
- ? �u�� `.dockerignore` �t�m

### ?? **�s�X���D�״_**
- ? �״_ `VendorAuthController.cs` ����r��
- ? �״_ `PaymentCallbackController.cs` ����r��
- ? �״_ `PaymentsController.cs` �������
- ? �״_ `PointsService.cs` �������
- ? �״_ `Dockerfile` �������
- ? �״_ `.dockerignore` �������
- ? �״_ `api-test-tool.html` HTML ���D

### ??? **�M�z�t�m���**
- ? ���������D�� `railway.toml`
- ? �T�O Railway �۰��˴� Dockerfile
- ? ���a�c�ش��ճq�L

## ?? **�ߧY���泡�p**

### **�Ĥ@�B�G���e�״_**
```bash
git add .
git commit -m "?? Final fix: All encoding issues resolved for Railway deployment"
git push origin main
```

### **�ĤG�B�GRailway ���p**
1. �n�J [Railway](https://railway.app)
2. �Ыطs���ةέ��s���p�{������
3. �s���z�� GitHub �ܮw
4. ���ݦ۰ʺc�ا���

### **�ĤT�B�G�t�m�����ܼ�**
```bash
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Server=tcp:jadepej-dbserver.database.windows.net,1433;Initial Catalog=jadepej-dbserver-new;Persist Security Info=False;User ID=team4;Password=#Gogojade;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;

Cloudinary__CloudName=jadetainan
Cloudinary__ApiKey=384776688611428
Cloudinary__ApiSecret=4dSdNavAr96WmP0vO_wJL8TkbTU

Jwt__Key=YourSuperSecretKeyThatIsLongAndComplex_123!@#
Jwt__Issuer=https://your-railway-url.up.railway.app
Jwt__Audience=https://moonlit-klepon-a78f8c.netlify.app

SmtpSettings__User=jade0905jade@gmail.com
SmtpSettings__Pass=nsuragycwfiolqpc
SmtpSettings__FromEmail=jade0905jade@gmail.com

Google__ClientId=905313427248-3vg0kd6474kbaif9ujg41n7376ua8ajp.apps.googleusercontent.com
```

## ?? **�w�����G**

�o���״_��A�z���Ӭݨ�G

1. **? Railway Build ���\**
   - Docker �M���c�ا���
   - �L�s�X���~
   - �L�y�k���~

2. **? ���ε{���Ұ�**
   - .NET 8 API ���`�B��
   - ��ť Port 8080
   - ���d�ˬd�^�����`

3. **? API �\�ॿ�`**
   - `/health` ���I���`
   - `/swagger` ���ɥi�X��
   - CORS �t�m���T
   - ��Ʈw�s�����`

## ?? **���p�����**

�ϥΧ�s�᪺ `api-test-tool.html`�G
1. ��s API URL ���z�� Railway URL
2. �����������
3. �T�{�Ҧ��\�ॿ�`

## ?? **�H�߫���**

- ? ���a `dotnet build` ���\
- ? �Ҧ��s�X���D�w�״_
- ? Docker �t�m�w�u��
- ? �e�ݾ�X�t�m����
- ? ���դu��ǳƴN��

## ?? **�ǳƦn�F�I**

�z�� API �{�b�w�g�G
- **���������F�s�X���D**
- **�u�ƤF Railway ���p�t�m**
- **�ǳƦn�䴩�z�� Netlify �e��**
- **��Ƨ��㪺�q�ӥ\��**

���ڭ̶}�l���p�a�I??