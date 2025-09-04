# 會員持有優惠券查詢端點 - 測試說明

## ?? 端點資訊

**路由**: `GET /api/Members/{memberId}/MemberCoupons`

**暫時方案說明**: 這是暫時方案，存在 IDOR 風險。設計時已將查詢方法獨立封裝，之後可無痛切換到 claims 版 `/api/Members/me/MemberCoupons`。

## ?? 查詢參數

| 參數名稱 | 類型 | 預設值 | 說明 |
|---------|------|-------|------|
| `activeOnly` | bool | false | 只回「目前可用」的持有券 |
| `status` | string | "" | 狀態篩選: active\|used\|expired\|cancelled |
| `page` | int | 1 | 頁碼（<1 視為 1） |
| `pageSize` | int | 20 | 每頁筆數（最大 100） |

### 「目前可用」定義（activeOnly=true 時同時滿足）：
- `Member_Coupons.Status = 'active'`
- `Coupons.Is_Active = 1`
- 現在時間介於 `Coupons.Start_At` 與 `Coupons.Expired_At`（含邊界）
- 若 `Coupons.Usage_Limit` 有值：`Coupons.Used_Count < Coupons.Usage_Limit`

### 排序規則：
- 主要依 `Coupons.Expired_At` 由近到遠
- 同到期日時，`Status='active'` 優先

## ?? 回傳 DTO 欄位

### 會員持有層（Member_Coupons）
- `MemberCouponId`: 會員優惠券記錄ID
- `Status`: 持有狀態
- `AssignedAt`: 分配時間
- `UsedAt`: 使用時間（可空）
- `OrderId`: 使用的訂單ID（可空）
- `VerificationCode`: 驗證碼

### 券定義層（Coupons）
- `CouponId`: 優惠券ID
- `Title`: 優惠券名稱
- `DiscountType`: 折扣類型
- `DiscountAmount`: 折扣金額/比例
- `MinSpend`: 最低消費（可空）
- `StartAt`: 開始時間
- `ExpiredAt`: 結束時間
- `IsActive`: 是否啟用
- `UsageLimit`: 使用上限（可空）
- `UsedCount`: 已使用次數
- `SellersId`: 廠商ID（可空）
- `CategoryId`: 分類ID（可空）
- `ApplicableLevelId`: 適用等級ID（可空）

### 衍生欄位
- `Source`: 來源（platform\|seller）
- `SellerName`: 廠商名稱（可空）
- `FormattedDiscount`: 格式化折扣顯示
- `ValidityPeriod`: 有效期間
- `UsageInfo`: 使用情況
- `IsCurrentlyActive`: 是否目前可用

## ?? 測試範例請求

### 範例 1: 查詢目前可用的優惠券
```http
GET /api/Members/123/MemberCoupons?activeOnly=true&page=1&pageSize=10
```

**期望結果**: 只返回該會員目前可以使用的優惠券

### 範例 2: 查詢已使用的優惠券
```http
GET /api/Members/123/MemberCoupons?status=used&page=1&pageSize=20
```

**期望結果**: 只返回該會員已使用的優惠券記錄

### 範例 3: 分頁查詢所有優惠券
```http
GET /api/Members/123/MemberCoupons?page=2&pageSize=15
```

**期望結果**: 返回該會員的所有優惠券，第2頁，每頁15筆

## ?? 回傳格式範例

```json
{
  "success": true,
  "message": "查詢會員優惠券成功",
  "data": [
    {
      "memberCouponId": 1,
      "status": "active",
      "assignedAt": "2024-01-15T10:30:00",
      "usedAt": null,
      "orderId": null,
      "verificationCode": "ABC123",
      "couponId": 10,
      "title": "新年特惠券",
      "discountType": "%數折扣",
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
      "formattedDiscount": "20% 折扣",
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

## ??? 錯誤處理

- **400 Bad Request**: 當 memberId ? 0
- **500 Internal Server Error**: 當資料庫查詢失敗

## ?? Swagger 標註

Controller 中已包含完整的 XML 文檔註解，支援 Swagger 自動生成 API 文檔。可直接在 Swagger UI 中進行測試。

**Swagger URL**: `https://localhost:7106/swagger` (開發環境)

## ?? 未來升級路徑

當要切換到 JWT claims 版本時：
1. 新增路由 `/api/Members/me/MemberCoupons`
2. 從 JWT claims 中取得 memberId
3. 呼叫相同的 `GetMemberCouponsInternal` 方法
4. DTO 和商業邏輯完全不變

## ?? 手動驗收清單

### 後端 API 測試
1. **Swagger 測試**:
   - 開啟 `https://localhost:7106/swagger`
   - 找到 `Members` 控制器下的 `GET /api/Members/{memberId}/MemberCoupons`
   - 測試各種參數組合

2. **Postman/Thunder Client 測試**:
   ```
   GET https://localhost:7106/api/Members/1/MemberCoupons
   GET https://localhost:7106/api/Members/1/MemberCoupons?activeOnly=true
   GET https://localhost:7106/api/Members/1/MemberCoupons?status=active&page=1&pageSize=10
   ```

3. **參數驗證測試**:
   - 測試 `memberId=0` 或負數 → 應回傳 400
   - 測試 `pageSize=200` → 應自動裁切為 100
   - 測試 `page=-1` → 應自動調整為 1

4. **資料正確性測試**:
   - 確認回傳的 `MemberCouponId` 對應正確的會員
   - 確認 `activeOnly=true` 時只回傳符合條件的券
   - 確認排序順序（依到期日由近到遠）
   - 確認分頁資訊正確（total, page, pageSize, totalPages）

### 前端整合測試（預備）
當前端實作完成後：
1. 登入會員帳號
2. 進入「我的優惠券」頁面
3. 檢查 Network 面板：
   - URL 格式正確 `/api/Members/{memberId}/MemberCoupons`
   - 查詢參數正確傳遞
   - 如有 token，應正確帶入 `Authorization: Bearer {token}`
4. 功能測試：
   - 「只看可用」切換功能
   - 狀態篩選下拉選單
   - 分頁切換
   - 優惠券卡片顯示正確資訊

## ?? 安全提醒

**IDOR 風險**: 目前任何人都可以修改 URL 中的 `{memberId}` 來查看其他會員的優惠券。這是暫時的實作方式。

**生產環境建議**: 
- 儘快切換到 JWT claims 版本 (`/api/Members/me/MemberCoupons`)
- 加入授權檢查，確保只能查看自己的優惠券
- 記錄敏感操作的存取日誌

## ?? 相關檔案

- **Controller**: `Team.API/Controllers/MembersController.cs`
- **DTO**: `Team.API/DTO/MyMemberCouponDto.cs`
- **分頁 DTO**: `Team.API/DTO/PagedResultDto.cs`
- **測試文檔**: `Team.API/README_MemberCoupons.md` (本檔案)