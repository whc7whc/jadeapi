# �|�������u�f��d�ߺ��I - ���ջ���

## ?? ���I��T

**����**: `GET /api/Members/{memberId}/MemberCoupons`

**�Ȯɤ�׻���**: �o�O�Ȯɤ�סA�s�b IDOR ���I�C�]�p�ɤw�N�d�ߤ�k�W�߫ʸˡA����i�L�h������ claims �� `/api/Members/me/MemberCoupons`�C

## ?? �d�߰Ѽ�

| �ѼƦW�� | ���� | �w�]�� | ���� |
|---------|------|-------|------|
| `activeOnly` | bool | false | �u�^�u�ثe�i�Ρv�������� |
| `status` | string | "" | ���A�z��: active\|used\|expired\|cancelled |
| `page` | int | 1 | ���X�]<1 ���� 1�^ |
| `pageSize` | int | 20 | �C�����ơ]�̤j 100�^ |

### �u�ثe�i�Ρv�w�q�]activeOnly=true �ɦP�ɺ����^�G
- `Member_Coupons.Status = 'active'`
- `Coupons.Is_Active = 1`
- �{�b�ɶ����� `Coupons.Start_At` �P `Coupons.Expired_At`�]�t��ɡ^
- �Y `Coupons.Usage_Limit` ���ȡG`Coupons.Used_Count < Coupons.Usage_Limit`

### �ƧǳW�h�G
- �D�n�� `Coupons.Expired_At` �Ѫ�컷
- �P�����ɡA`Status='active'` �u��

## ?? �^�� DTO ���

### �|�������h�]Member_Coupons�^
- `MemberCouponId`: �|���u�f��O��ID
- `Status`: �������A
- `AssignedAt`: ���t�ɶ�
- `UsedAt`: �ϥήɶ��]�i�š^
- `OrderId`: �ϥΪ��q��ID�]�i�š^
- `VerificationCode`: ���ҽX

### ��w�q�h�]Coupons�^
- `CouponId`: �u�f��ID
- `Title`: �u�f��W��
- `DiscountType`: �馩����
- `DiscountAmount`: �馩���B/���
- `MinSpend`: �̧C���O�]�i�š^
- `StartAt`: �}�l�ɶ�
- `ExpiredAt`: �����ɶ�
- `IsActive`: �O�_�ҥ�
- `UsageLimit`: �ϥΤW���]�i�š^
- `UsedCount`: �w�ϥΦ���
- `SellersId`: �t��ID�]�i�š^
- `CategoryId`: ����ID�]�i�š^
- `ApplicableLevelId`: �A�ε���ID�]�i�š^

### �l�����
- `Source`: �ӷ��]platform\|seller�^
- `SellerName`: �t�ӦW�١]�i�š^
- `FormattedDiscount`: �榡�Ƨ馩���
- `ValidityPeriod`: ���Ĵ���
- `UsageInfo`: �ϥα��p
- `IsCurrentlyActive`: �O�_�ثe�i��

## ?? ���սd�ҽШD

### �d�� 1: �d�ߥثe�i�Ϊ��u�f��
```http
GET /api/Members/123/MemberCoupons?activeOnly=true&page=1&pageSize=10
```

**���浲�G**: �u��^�ӷ|���ثe�i�H�ϥΪ��u�f��

### �d�� 2: �d�ߤw�ϥΪ��u�f��
```http
GET /api/Members/123/MemberCoupons?status=used&page=1&pageSize=20
```

**���浲�G**: �u��^�ӷ|���w�ϥΪ��u�f��O��

### �d�� 3: �����d�ߩҦ��u�f��
```http
GET /api/Members/123/MemberCoupons?page=2&pageSize=15
```

**���浲�G**: ��^�ӷ|�����Ҧ��u�f��A��2���A�C��15��

## ?? �^�Ǯ榡�d��

```json
{
  "success": true,
  "message": "�d�߷|���u�f�馨�\",
  "data": [
    {
      "memberCouponId": 1,
      "status": "active",
      "assignedAt": "2024-01-15T10:30:00",
      "usedAt": null,
      "orderId": null,
      "verificationCode": "ABC123",
      "couponId": 10,
      "title": "�s�~�S�f��",
      "discountType": "%�Ƨ馩",
      "discountAmount": 20,
      "minSpend": 1000,
      "startAt": "2024-01-01T00:00:00",
      "expiredAt": "2024-12-31T23:59:59",
      "isActive": true,
      "usageLimit": 100,
      "usedCount": 25,
      "sellersId": null,
      "categoryId": 1,
      "applicableLevelId": 2,
      "source": "platform",
      "sellerName": null,
      "formattedDiscount": "20% �馩",
      "validityPeriod": "2024-01-01 ~ 2024-12-31",
      "usageInfo": "25/100",
      "isCurrentlyActive": true
    }
  ],
  "totalCount": 25,
  "currentPage": 1,
  "itemsPerPage": 20,
  "totalPages": 2,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

## ??? ���~�B�z

- **400 Bad Request**: �� memberId ? 0
- **500 Internal Server Error**: ���Ʈw�d�ߥ���

## ?? Swagger �е�

Controller ���w�]�t���㪺 XML ���ɵ��ѡA�䴩 Swagger �۰ʥͦ� API ���ɡC�i�����b Swagger UI ���i����աC

**Swagger URL**: `https://localhost:7106/swagger` (�}�o����)

## ?? ���ӤɯŸ��|

��n������ JWT claims �����ɡG
1. �s�W���� `/api/Members/me/MemberCoupons`
2. �q JWT claims �����o memberId
3. �I�s�ۦP�� `GetMemberCouponsInternal` ��k
4. DTO �M�ӷ~�޿觹������

## ?? ����禬�M��

### ��� API ����
1. **Swagger ����**:
   - �}�� `https://localhost:7106/swagger`
   - ��� `Members` ����U�� `GET /api/Members/{memberId}/MemberCoupons`
   - ���զU�ذѼƲզX

2. **Postman/Thunder Client ����**:
   ```
   GET https://localhost:7106/api/Members/1/MemberCoupons
   GET https://localhost:7106/api/Members/1/MemberCoupons?activeOnly=true
   GET https://localhost:7106/api/Members/1/MemberCoupons?status=active&page=1&pageSize=10
   ```

3. **�Ѽ����Ҵ���**:
   - ���� `memberId=0` �έt�� �� ���^�� 400
   - ���� `pageSize=200` �� ���۰ʵ����� 100
   - ���� `page=-1` �� ���۰ʽվ㬰 1

4. **��ƥ��T�ʴ���**:
   - �T�{�^�Ǫ� `MemberCouponId` �������T���|��
   - �T�{ `activeOnly=true` �ɥu�^�ǲŦX���󪺨�
   - �T�{�ƧǶ��ǡ]�̨����Ѫ�컷�^
   - �T�{������T���T�]total, page, pageSize, totalPages�^

### �e�ݾ�X���ա]�w�ơ^
��e�ݹ�@������G
1. �n�J�|���b��
2. �i�J�u�ڪ��u�f��v����
3. �ˬd Network ���O�G
   - URL �榡���T `/api/Members/{memberId}/MemberCoupons`
   - �d�߰Ѽƥ��T�ǻ�
   - �p�� token�A�����T�a�J `Authorization: Bearer {token}`
4. �\����աG
   - �u�u�ݥi�Ρv�����\��
   - ���A�z��U�Կ��
   - ��������
   - �u�f��d����ܥ��T��T

## ?? �w������

**IDOR ���I**: �ثe����H���i�H�ק� URL ���� `{memberId}` �Ӭd�ݨ�L�|�����u�f��C�o�O�Ȯɪ���@�覡�C

**�Ͳ����ҫ�ĳ**: 
- ���֤����� JWT claims ���� (`/api/Members/me/MemberCoupons`)
- �[�J���v�ˬd�A�T�O�u��d�ݦۤv���u�f��
- �O���ӷP�ާ@���s����x

## ?? �����ɮ�

- **Controller**: `Team.API/Controllers/MembersController.cs`
- **DTO**: `Team.API/DTO/MyMemberCouponDto.cs`
- **���� DTO**: `Team.API/DTO/PagedResultDto.cs`
- **���դ���**: `Team.API/README_MemberCoupons.md` (���ɮ�)