# ?? API ���ݳ��p���n (Docker �c�ذ��D�״_��)

## ?? �̷s���D���R�P�ѨM���

**Railway ���p���ѭ�]**: 
1. ~~`railway.toml` ���榡���~~~ ? �w�ץ�
2. ~~����r�Žs�X���D~~ ? �w�ץ�  
3. **Docker �c�ذt�m���D** ? �w�̷s�ץ�

**�̷s�ѨM���**: 
1. �u�ƤF Dockerfile �� Railway �M�Ϊ���
2. �]�w `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` �קK���a�ư��D
3. ²�ƺc�جy�{�A���������n��������
4. �T�O�Ҧ��t�m���ϥ� UTF-8 �s�X

## ?? �ߧY���p�B�J

### **�B�J 1: ���e�̷s�״_**git add .
git commit -m "Fix Docker build issues for Railway deployment"
git push origin main
### **�B�J 2: Railway ���s���p**
1. �n�J [Railway](https://railway.app)
2. �p�G���{�����ءA�I�� "Redeploy" 
3. �p�G�S���A�Ыطs���بós�� GitHub �ܮw

### **�B�J 3: �ʱ��c�عL�{**
�b Railway ����x���G
- �I�� "Build Logs" �d�ݺc�ضi��
- �T�{ Docker �c�ئ��\
- �������ε{���Ұ�

### **�B�J 4: �t�m�����ܼ�**# ���n�������ܼ�
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Server=tcp:jadepej-dbserver.database.windows.net,1433;Initial Catalog=jadepej-dbserver-new;Persist Security Info=False;User ID=team4;Password=#Gogojade;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;

# Cloudinary �]�w
Cloudinary__CloudName=jadetainan
Cloudinary__ApiKey=384776688611428
Cloudinary__ApiSecret=4dSdNavAr96WmP0vO_wJL8TkbTU

# JWT �]�w
Jwt__Key=YourSuperSecretKeyThatIsLongAndComplex_123!@#
Jwt__Issuer=https://your-railway-domain.up.railway.app
Jwt__Audience=https://moonlit-klepon-a78f8c.netlify.app
## ?? **�̷s Dockerfile �u��**
# Railway optimized Dockerfile for .NET 8 API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Set globalization to invariant mode to avoid locale issues
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Copy project file and restore
COPY Team.API/Team.API.csproj Team.API/
RUN dotnet restore Team.API/Team.API.csproj

# Copy source and build
COPY Team.API/ Team.API/
WORKDIR /src/Team.API
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Set environment for Railway
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENV ASPNETCORE_ENVIRONMENT=Production

# Copy published app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Team.API.dll"]
## ?? **���p�e����**

### ���a Docker ����# �c�ش���
docker build -t team-api-test .

# �B�����
docker run --rm -p 8080:8080 team-api-test

# ���հ��d���I
curl http://localhost:8080/health
### �ϥδ��դu��
�}�� `api-test-tool.html` �ô��աG
1. ��s API URL ���z�� Railway URL
2. ����Ҧ�����
3. �T�{�Ҧ��\�ॿ�`

## ?? **�G�ٱư����n**

### **���D 1: Docker �c�إ���**
**�g��**: `Failed to build an image`
**�ѨM���**:# �ˬd Dockerfile �y�k
docker build --no-cache -t test .

# �T�{�M�׵��c
ls -la Team.API/
### **���D 2: ���ε{���L�k�Ұ�**
**�g��**: �c�ئ��\�����ε{���Y��
**�ѨM���**:
- �ˬd Railway Deploy Logs
- �T�{�����ܼƳ]�w���T
- ���Ҹ�Ʈw�s���r��

### **���D 3: ���d�ˬd����**
**�g��**: `/health` ���I�L�^��
**�ѨM���**:# ���հ򥻺��I
curl https://your-app.up.railway.app/
curl https://your-app.up.railway.app/health
## ? **���p���\�ˬd�M��**

���p������A�T�{�H�U���ءG

### **�򥻥\��**
- [ ] Railway �c�ئ��\�]�L Docker ���~�^
- [ ] ���ε{�����\�Ұ�
- [ ] ���d�ˬd���I���` (`/health`)
- [ ] �ں��I���` (`/`)

### **API �\��**
- [ ] Swagger ���ɥi�X�� (`/swagger`)
- [ ] ��Ʈw�s�����`
- [ ] �{�Ҩt�Υ��`�B�@
- [ ] CORS �t�m���T

### **�e�ݾ�X**
- [ ] �e�ݥi�H���\�ե� API
- [ ] JWT �{�ҥ��`�B�@
- [ ] �ӫ~ API ���`
- [ ] �ʪ����\�ॿ�`

## ?? **�e�ݰt�m��s**

���p���\��A��s�z�� Vue.js �e�ݡG
// �b Vue.js �t�m����s API URL
const API_BASE_URL = 'https://your-actual-railway-url.up.railway.app'

// ���ճs��
fetch(`${API_BASE_URL}/health`)
  .then(response => response.json())
  .then(data => console.log('API �s�����\:', data))
## ?? **�Y�ɺʱ�**

### Railway ����x�ʱ�
- **Build Logs**: �d�ݺc�عL�{
- **Deploy Logs**: �d�ݳ��p��x  
- **Application Logs**: �d�ݹB��ɤ�x
- **Metrics**: CPU�B���s�ϥα��p

### ���d�ˬd# �w���ˬd API ���A
curl -f https://your-app.up.railway.app/health

# �ˬd Swagger ����
curl -f https://your-app.up.railway.app/swagger/v1/swagger.json
## ?? **���p���\�I**

�p�G���Ӧ����n�ާ@�A�z�� API �{�b���ӡG

? **�b Railway ���\�B��**  
? **�䴩�z�� Netlify �e��**  
? **���ѧ��㪺�q�� API �\��**  
? **�ǳƦn���z���@�~���A��**  

---

**�嫬���\�� Railway URL**:
- API ��¦: `https://your-app-name.up.railway.app`
- ���d�ˬd: `https://your-app-name.up.railway.app/health`
- API ����: `https://your-app-name.up.railway.app/swagger`

## ?? **�ݭn��U�H**

�p�G���p���M���ѡG
1. �ˬd Railway Build Logs ��������~�T��
2. �T�{�Ҧ���󳣬O UTF-8 �s�X
3. ���� Dockerfile �b���a�i�H���\�c��
4. �ˬd�O�_����|���̿ඵ��

**�O��**: Railway �����p�q�`�ݭn 5-10 �����A�Э@�ߵ��ݺc�ا����I