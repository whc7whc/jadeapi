# �|���I�ơ]JCoin�^�d�߻P���� API - ���ջ���

## ?? ���I�M��

### �d�l�B
**����**: `GET /api/Members/{memberId}/Points/Balance`

**�^��**: `{ memberId, balance, lastUpdatedAt }`

�ӷ��GMember_Stats.Total_Points�]���D�^�A�Y�d�L��Ʀ^ balance=0�C

### �d���v�]���� + �z��^
**����**: `GET /api/Members/{memberId}/Points/History?type=&dateFrom=&dateTo=&page=&pageSize=`

**�z��Ѽ�**:
- `type`�]�i�šF���\�Gsignin|used|refund|earned|expired|adjustment�^
- `dateFrom`/`dateTo`�]�H Created_At �z��^
- `page`�]�w�] 1�^
- `pageSize`�]�w�] 20�A�̤j 100�^

**�Ƨ�**: Created_At DESC

**�^��**: �����e�� `{ items, total, page, pageSize }`�A�C���t�GId, Type, Amount, Note, Expired_At, Transaction_Id, Created_At, Verification_Code

### �[�I�]Earn / �վ�^
**����**: `POST /api/Members/{memberId}/Points/Earn`

**�ШD**: `{ amount (>0), type ("earned" �� "adjustment"), note?, expiredAt?, transactionId?, verificationCode? }`

**�޿�**:
- amount > 0�Ftype �����b�զW��
- �h���G�Y verificationCode �w�s�b�� Points_Log �N������^���\���G�]�����^
- ����G�s�W Points_Log�]+amount�^�A�P�B�w�����W Member_Stats.Total_Points = Total_Points + amount
- ���ѰO Points_Log_Error

### ���I�]Use�^
**����**: `POST /api/Members/{memberId}/Points/Use`

**�ШD**: `{ amount (>0), note?, transactionId (�q��s����), verificationCode? }`

**����**:
- Ū Member_Stats.Total_Points�A���i�p�� amount
- verificationCode �����B�z�]�Y���ơA������^�J�����G�^

**���**:
- �s�W Points_Log�]type=used�Aamount=���ưO���A���^���ЦP�ɱa�W direction:"debit"�^
- ��l��s�GUPDATE Member_Stats SET Total_Points = Total_Points - @amount WHERE Member_Id=@memberId AND Total_Points >= @amount�F�ˬd���v�T�C��==1
- �Y UPDATE ���� �� �^ 409/400 �ðO Points_Log_Error

### �^�ɡ]Refund�^
**����**: `POST /api/Members/{memberId}/Points/Refund`

**�ШD**: `{ amount (>0), sourceTransactionId, note?, verificationCode? }`

**����**: verificationCode �h��

**���**: �g Points_Log�]refund�^�A�P�B�[�^ Total_Points

### ����妸�]�ȰO�����I�A�Ω�Ƶ{�^
**����**: `POST /api/Members/{memberId}/Points/Expire`

**�ШD**: `{ amount (>0), note?, verificationCode? }`

�Ȧb�A�̦��u������I�v�ݨD�ɨϥΡG�g expired ��x�A�æP�B���� Total_Points�]�P Use �ۦP���w�� UPDATE�^

## ?? ���սd�ҽШD

### 1. �d�߷|���I�ƾl�B
```http
GET /api/Members/1/Points/Balance
```

**����^��**:
```json
{
  "memberId": 1,
  "balance": 1000,
  "lastUpdatedAt": "2024-01-20T10:30:00"
}
```

### 2. �d���I�ƾ��v�]�u�ݤw�ϥΡ^
```http
GET /api/Members/1/Points/History?type=used&page=1&pageSize=10
```

**����^��**:
```json
{
  "success": true,
  "message": "�d���I�ƾ��v���\",
  "data": [
    {
      "id": 123,
      "type": "used",
      "amount": 100,
      "note": "�ʶR�ӫ~",
      "expiredAt": null,
      "transactionId": "ORDER-123",
      "createdAt": "2024-01-20T14:30:00",
      "verificationCode": "VERIFY-ABC"
    }
  ],
  "totalCount": 5,
  "currentPage": 1,
  "itemsPerPage": 10,
  "totalPages": 1
}
```

### 3. �[�I�]�ʪ��^�X�^
```http
POST /api/Members/1/Points/Earn
Content-Type: application/json

{
  "amount": 50,
  "type": "earned",
  "note": "�ʪ��^�X",
  "transactionId": "ORDER-456",
  "verificationCode": "EARN-XYZ"
}
```

**����^��**:
```json
{
  "memberId": 1,
  "beforeBalance": 1000,
  "changeAmount": 50,
  "afterBalance": 1050,
  "type": "earned",
  "transactionId": "ORDER-456",
  "verificationCode": "EARN-XYZ",
  "createdAt": "2024-01-20T15:00:00"
}
```

### 4. ���I�]�ϥ��I�ơ^
```http
POST /api/Members/1/Points/Use
Content-Type: application/json

{
  "amount": 200,
  "note": "�ʶR�ӫ~���",
  "transactionId": "ORDER-789",
  "verificationCode": "USE-DEF"
}
```

**����^��**:
```json
{
  "memberId": 1,
  "beforeBalance": 1050,
  "changeAmount": -200,
  "afterBalance": 850,
  "type": "used",
  "transactionId": "ORDER-789",
  "verificationCode": "USE-DEF",
  "createdAt": "2024-01-20T15:30:00"
}
```

### 5. �^���I��
```http
POST /api/Members/1/Points/Refund
Content-Type: application/json

{
  "amount": 100,
  "sourceTransactionId": "ORDER-789",
  "note": "�q������h�I",
  "verificationCode": "REFUND-GHI"
}
```

## ?? �禬���ղM��

### ? �d�l�B�\��
1. **���`�d��**: �|��ID=1�A���I�ưO�� �� �^�ǥ��T�l�B
2. **�s�|��**: �|��ID=999�A�L�I�ưO�� �� �^�� balance=0
3. **�L�ķ|��ID**: �|��ID=0 �� �^�� 400 ���~

### ? �d���v�\��
1. **�򥻤���**: page=1, pageSize=10 �� ���T������T
2. **�����z��**: type=used �� �u�^�� used �����O��
3. **����z��**: dateFrom/dateTo �� �u�^�ǫ��w����d��O��
4. **�Ƨ�����**: �T�{�� CreatedAt DESC �Ƨ�
5. **�ŵ��G**: �L�O���ɥ��T�^�ǪŰ}�C

### ? �[�I�\��
1. **���`�[�I**: amount=100, type=earned �� ���\�[�I�ç�s MemberStats
2. **������**: �ۦP verificationCode ���ƽШD �� �^�ǬۦP���G�A�����ƥ[�I
3. **�L������**: type=invalid �� �^�� 400 ���~
4. **�L�Ī��B**: amount=0 �� �^�� 400 ���~

### ? ���I�\��
1. **���`���I**: �l�B������ �� ���\���I�ç�s MemberStats
2. **�l�B����**: amount > ��e�l�B �� �^�� 409 Conflict
3. **������**: �ۦP verificationCode ���ƽШD �� �^�ǬۦP���G�A�����Ʀ��I
4. **�ֵo�w��**: �h�Өֵo���I�ШD �� �T�O�l�B���|�t��

### ? �^�ɥ\��
1. **���`�^��**: ���Ī� sourceTransactionId �� ���\�^���I��
2. **������**: �ۦP verificationCode ���ƽШD �� �^�ǬۦP���G

### ? ���~�B�z
1. **�t�ο��~**: ��Ʈw�s�u���� �� �O���� Points_Log_Error
2. **��J����**: �L�İѼ� �� �^�ǸԲӿ��~�T��
3. **��x�O��**: �Ҧ��ާ@�����A����x�O��

## ??? ���դu��ϥ�

### Swagger UI ����
1. �Ұ� API �M��
2. �y�X `https://localhost:7106/swagger`
3. ��� **Members** ����U���I�Ƭ������I
4. �v�@���ըC�Ӻ��I���U�ر���

### Postman ����
�ɤJ�H�U�����ܼơG
```
API_BASE_URL = https://localhost:7106
MEMBER_ID = 1
```

### ��Ʈw����
���ի��ˬd�H�U��ƪ�G
1. **Member_Stats**: TotalPoints �O�_���T��s
2. **Points_Log**: �O�_���T�O���C������
3. **Points_Log_Error**: ���~�O�_���T�O��

## ?? �w������

**IDOR ���I**: �ثe����H���i�H�ק� URL ���� `{memberId}` �Ӿާ@��L�|�����I�ơC�o�O�Ȯɪ���@�覡�C

**�Ͳ����ҫ�ĳ**: 
- ���֤����� JWT claims ���� (`/api/Members/me/Points/...`)
- �[�J���v�ˬd�A�T�O�u��ާ@�ۤv���I��
- �O���ӷP�ާ@���s����x
- �]�w�I�ƾާ@���B�׭���

## ?? ���ӤɯŸ��|

��n������ JWT claims �����ɡG
1. �s�W���� `/api/Members/me/Points/...`
2. �q JWT claims �����o memberId
3. �I�s�ۦP���A�ȼh��k
4. DTO �M�ӷ~�޿觹������

## ?? �ӷ~�W�h����

### �����զW��
- ? signin, used, refund, earned, expired, adjustment
- ? ��L�����^�� 400

### ���B����
- ? �����
- ? 0 �έt�Ȧ^�� 400

### ������
- ? �a verificationCode�A�J��w�s�b�O�� �� �^���µ��G
- ? ���a�h�����C�ШD�@��

### �ֵo�w��
- ? �Ҧ��u���I�v�ϥγ�@ UPDATE...WHERE Total_Points >= amount �ˬd����
- ? �T�O�l�B���|�t��

### �ɰϤ@�P��
- ? �P�M�פ@�P�ϥ� DateTime.Now

---

## ?? �����ɮ�

- **Controller**: `Team.API/Controllers/MembersController.cs`
- **Service**: `Team.API/Services/PointsService.cs`
- **DTO**: `Team.API/DTO/PointsDto.cs`
- **���դ���**: `Team.API/README_Points.md` (���ɮ�)