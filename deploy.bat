@echo off
REM Team API �ֳt���p�}�� (Windows)
echo ?? �ǳƳ��p Team API �춳��...

REM �ˬd Docker �O�_�w��
docker --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ? Docker ���w�ˡA�Х��w�� Docker Desktop
    pause
    exit /b 1
)

REM �ˬd .NET �O�_�w��
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ? .NET SDK ���w�ˡA�Х��w�� .NET 8 SDK
    pause
    exit /b 1
)

echo ? �����ˬd����

REM �c�� Docker �M��
echo ?? �c�� Docker �M��...
docker build -t team-api .

if %errorlevel% neq 0 (
    echo ? Docker �M���c�إ���
    pause
    exit /b 1
)

echo ? Docker �M���c�ئ��\

REM ���ե��a�B��
echo ?? ���ե��a�B��...
docker run -d -p 8080:8080 --name team-api-test team-api

REM �������αҰ�
timeout /t 10 /nobreak >nul

REM ���հ��d�ˬd
echo ?? ���հ��d�ˬd...
curl -f http://localhost:8080/health >nul 2>&1
if %errorlevel% neq 0 (
    echo ? ���d�ˬd����
    docker logs team-api-test
    docker stop team-api-test
    docker rm team-api-test
    pause
    exit /b 1
)

echo ? ���d�ˬd�q�L

REM �M�z���ծe��
docker stop team-api-test
docker rm team-api-test

echo ?? ���a���է����I
echo.
echo ?? �U�@�B���p�춳�ݡG
echo 1. �N�N�X���e�� GitHub
echo 2. �b Railway.app �Ыطs����
echo 3. �s���z�� GitHub �ܮw
echo 4. �t�m�����ܼơ]�Ѧ� DEPLOYMENT_GUIDE.md�^
echo 5. ���ݦ۰ʳ��p����
echo.
echo ?? �ԲӨB�J�аѦ� DEPLOYMENT_GUIDE.md
echo.
echo �����N���~��...
pause >nul