# 會員點數（JCoin）查詢與異動 API - 測試說明

## ?? 端點清單

### 查餘額
**路由**: `GET /api/Members/{memberId}/Points/Balance`

**回傳**: `{ memberId, balance, lastUpdatedAt }`

來源：Member_Stats.Total_Points（為主），若查無資料回 balance=0。

### 查歷史（分頁 + 篩選）
**路由**: `GET /api/Members/{memberId}/Points/History?type=&dateFrom=&dateTo=&page=&pageSize=`

**篩選參數**:
- `type`（可空；允許：signin|used|refund|earned|expired|adjustment）
- `dateFrom`/`dateTo`（以 Created_At 篩選）
- `page`（預設 1）
- `pageSize`（預設 20，最大 100）

**排序**: Created_At DESC

**回傳**: 分頁容器 `{ items, total, page, pageSize }`，每筆含：Id, Type, Amount, Note, Expired_At, Transaction_Id, Created_At, Verification_Code

### 加點（Earn / 調整）
**路由**: `POST /api/Members/{memberId}/Points/Earn`

**請求**: `{ amount (>0), type ("earned" 或 "adjustment"), note?, expiredAt?, transactionId?, verificationCode? }`

**邏輯**:
- amount > 0；type 必須在白名單
- 去重：若 verificationCode 已存在於 Points_Log 就直接返回成功結果（冪等）
- 交易：新增 Points_Log（+amount），同步安全遞增 Member_Stats.Total_Points = Total_Points + amount
- 失敗記 Points_Log_Error

### 扣點（Use）
**路由**: `POST /api/Members/{memberId}/Points/Use`

**請求**: `{ amount (>0), note?, transactionId (訂單編號等), verificationCode? }`

**驗證**:
- 讀 Member_Stats.Total_Points，不可小於 amount
- verificationCode 冪等處理（若重複，直接返回既有結果）

**交易**:
- 新增 Points_Log（type=used，amount=正數記錄，但回應請同時帶上 direction:"debit"）
- 原子更新：UPDATE Member_Stats SET Total_Points = Total_Points - @amount WHERE Member_Id=@memberId AND Total_Points >= @amount；檢查受影響列數==1
- 若 UPDATE 失敗 → 回 409/400 並記 Points_Log_Error

### 回補（Refund）
**路由**: `POST /api/Members/{memberId}/Points/Refund`

**請求**: `{ amount (>0), sourceTransactionId, note?, verificationCode? }`

**冪等**: verificationCode 去重

**交易**: 寫 Points_Log（refund），同步加回 Total_Points

### 到期批次（僅記錄端點，用於排程）
**路由**: `POST /api/Members/{memberId}/Points/Expire`

**請求**: `{ amount (>0), note?, verificationCode? }`

僅在你們有「到期扣點」需求時使用：寫 expired 日誌，並同步扣除 Total_Points（與 Use 相同的安全 UPDATE）

## ?? 測試範例請求

### 1. 查詢會員點數餘額
```http
GET /api/Members/1/Points/Balance
```

**期望回傳**:
```json
{
  "memberId": 1,
  "balance": 1000,
  "lastUpdatedAt": "2024-01-20T10:30:00"
}
```

### 2. 查詢點數歷史（只看已使用）
```http
GET /api/Members/1/Points/History?type=used&page=1&pageSize=10
```

**期望回傳**:
```json
{
  "success": true,
  "message": "查詢點數歷史成功",
  "data": [
    {
      "id": 123,
      "type": "used",
      "amount": 100,
      "note": "購買商品",
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

### 3. 加點（購物回饋）
```http
POST /api/Members/1/Points/Earn
Content-Type: application/json

{
  "amount": 50,
  "type": "earned",
  "note": "購物回饋",
  "transactionId": "ORDER-456",
  "verificationCode": "EARN-XYZ"
}
```

**期望回傳**:
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

### 4. 扣點（使用點數）
```http
POST /api/Members/1/Points/Use
Content-Type: application/json

{
  "amount": 200,
  "note": "購買商品折抵",
  "transactionId": "ORDER-789",
  "verificationCode": "USE-DEF"
}
```

**期望回傳**:
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

### 5. 回補點數
```http
POST /api/Members/1/Points/Refund
Content-Type: application/json

{
  "amount": 100,
  "sourceTransactionId": "ORDER-789",
  "note": "訂單取消退點",
  "verificationCode": "REFUND-GHI"
}
```

## ?? 驗收測試清單

### ? 查餘額功能
1. **正常查詢**: 會員ID=1，有點數記錄 → 回傳正確餘額
2. **新會員**: 會員ID=999，無點數記錄 → 回傳 balance=0
3. **無效會員ID**: 會員ID=0 → 回傳 400 錯誤

### ? 查歷史功能
1. **基本分頁**: page=1, pageSize=10 → 正確分頁資訊
2. **類型篩選**: type=used → 只回傳 used 類型記錄
3. **日期篩選**: dateFrom/dateTo → 只回傳指定日期範圍記錄
4. **排序驗證**: 確認按 CreatedAt DESC 排序
5. **空結果**: 無記錄時正確回傳空陣列

### ? 加點功能
1. **正常加點**: amount=100, type=earned → 成功加點並更新 MemberStats
2. **冪等性**: 相同 verificationCode 重複請求 → 回傳相同結果，不重複加點
3. **無效類型**: type=invalid → 回傳 400 錯誤
4. **無效金額**: amount=0 → 回傳 400 錯誤

### ? 扣點功能
1. **正常扣點**: 餘額足夠時 → 成功扣點並更新 MemberStats
2. **餘額不足**: amount > 當前餘額 → 回傳 409 Conflict
3. **冪等性**: 相同 verificationCode 重複請求 → 回傳相同結果，不重複扣點
4. **併發安全**: 多個併發扣點請求 → 確保餘額不會負數

### ? 回補功能
1. **正常回補**: 有效的 sourceTransactionId → 成功回補點數
2. **冪等性**: 相同 verificationCode 重複請求 → 回傳相同結果

### ? 錯誤處理
1. **系統錯誤**: 資料庫連線失敗 → 記錄到 Points_Log_Error
2. **輸入驗證**: 無效參數 → 回傳詳細錯誤訊息
3. **日誌記錄**: 所有操作都有適當的日誌記錄

## ??? 測試工具使用

### Swagger UI 測試
1. 啟動 API 專案
2. 造訪 `https://localhost:7106/swagger`
3. 找到 **Members** 控制器下的點數相關端點
4. 逐一測試每個端點的各種情境

### Postman 測試
導入以下環境變數：
```
API_BASE_URL = https://localhost:7106
MEMBER_ID = 1
```

### 資料庫驗證
測試後檢查以下資料表：
1. **Member_Stats**: TotalPoints 是否正確更新
2. **Points_Log**: 是否正確記錄每筆異動
3. **Points_Log_Error**: 錯誤是否正確記錄

## ?? 安全提醒

**IDOR 風險**: 目前任何人都可以修改 URL 中的 `{memberId}` 來操作其他會員的點數。這是暫時的實作方式。

**生產環境建議**: 
- 儘快切換到 JWT claims 版本 (`/api/Members/me/Points/...`)
- 加入授權檢查，確保只能操作自己的點數
- 記錄敏感操作的存取日誌
- 設定點數操作的額度限制

## ?? 未來升級路徑

當要切換到 JWT claims 版本時：
1. 新增路由 `/api/Members/me/Points/...`
2. 從 JWT claims 中取得 memberId
3. 呼叫相同的服務層方法
4. DTO 和商業邏輯完全不變

## ?? 商業規則驗證

### 類型白名單
- ? signin, used, refund, earned, expired, adjustment
- ? 其他類型回傳 400

### 金額驗證
- ? 正整數
- ? 0 或負值回傳 400

### 冪等性
- ? 帶 verificationCode，遇到已存在記錄 → 回傳舊結果
- ? 不帶則視為每請求一筆

### 併發安全
- ? 所有「扣點」使用單一 UPDATE...WHERE Total_Points >= amount 檢查條件
- ? 確保餘額不會負數

### 時區一致性
- ? 與專案一致使用 DateTime.Now

---

## ?? 相關檔案

- **Controller**: `Team.API/Controllers/MembersController.cs`
- **Service**: `Team.API/Services/PointsService.cs`
- **DTO**: `Team.API/DTO/PointsDto.cs`
- **測試文檔**: `Team.API/README_Points.md` (本檔案)