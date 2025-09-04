#!/bin/bash

# Team API �ֳt���p�}��
echo "?? �ǳƳ��p Team API �춳��..."

# �ˬd Docker �O�_�w��
if ! command -v docker &> /dev/null; then
    echo "? Docker ���w�ˡA�Х��w�� Docker"
    exit 1
fi

# �ˬd .NET �O�_�w��
if ! command -v dotnet &> /dev/null; then
    echo "? .NET SDK ���w�ˡA�Х��w�� .NET 8 SDK"
    exit 1
fi

echo "? �����ˬd����"

# �c�� Docker �M��
echo "?? �c�� Docker �M��..."
docker build -t team-api .

if [ $? -eq 0 ]; then
    echo "? Docker �M���c�ئ��\"
else
    echo "? Docker �M���c�إ���"
    exit 1
fi

# ���ե��a�B��
echo "?? ���ե��a�B��..."
docker run -d -p 8080:8080 --name team-api-test team-api

# �������αҰ�
sleep 10

# ���հ��d�ˬd
echo "?? ���հ��d�ˬd..."
if curl -f http://localhost:8080/health > /dev/null 2>&1; then
    echo "? ���d�ˬd�q�L"
else
    echo "? ���d�ˬd����"
    docker logs team-api-test
    docker stop team-api-test
    docker rm team-api-test
    exit 1
fi

# �M�z���ծe��
docker stop team-api-test
docker rm team-api-test

echo "?? ���a���է����I"
echo ""
echo "?? �U�@�B���p�춳�ݡG"
echo "1. �N�N�X���e�� GitHub"
echo "2. �b Railway.app �Ыطs����"
echo "3. �s���z�� GitHub �ܮw"
echo "4. �t�m�����ܼơ]�Ѧ� DEPLOYMENT_GUIDE.md�^"
echo "5. ���ݦ۰ʳ��p����"
echo ""
echo "?? �ԲӨB�J�аѦ� DEPLOYMENT_GUIDE.md"