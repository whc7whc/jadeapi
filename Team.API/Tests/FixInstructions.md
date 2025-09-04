# 🎯 **修正完成！現在請執行以下步驟：**

## **Step 1: 執行修正腳本**
在 SQL Server Management Studio 中執行：
```sql
-- 執行 Team.API\Tests\FixExistingData.sql 
```

## **Step 2: 重新啟動 API**
1. 停止 Team.API 專案（如果正在執行）
2. 重新啟動 Team.API 專案 (F5)
3. 等待 Swagger UI 開啟

## **Step 3: 測試修正後的 API**

### **🧪 測試順序：**

1. **GET** `/api/Coupons/UserAvailable/1`
   - **預期結果**：應該回傳你的優惠券列表，不再是空陣列

2. **POST** `/api/Carts/user/1/items` 
   ```json
   {
     "productId": 1,
     "attributeValueId": 1,
     "quantity": 2
   }
   ```

3. **POST** `/api/Carts/user/1/coupon`
   ```json
   {
     "couponCode": "1"
   }
   ```
   - **預期結果**：應該成功套用優惠券，不再顯示 "您沒有此優惠券，無法使用"

## **Step 4: 檢查日誌**
如果仍有問題，請檢查 API 的控制台輸出，會有詳細的診斷日誌：
- 用戶在 Member_Coupons 表中的記錄數量
- Coupons 表中啟用的優惠券數量

---

## **🔧 修正內容說明：**

### **主要問題：**
- Entity Framework 使用 PascalCase 屬性名稱 (`mc.MemberId`)
- 你的資料庫使用 snake_case 欄位名稱 (`Member_Id`)
- 這導致查詢無法找到資料

### **解決方案：**
- 改用原生 SQL 查詢，直接使用正確的 snake_case 欄位名稱
- 手動處理 DataReader 來確保資料正確映射
- 加入詳細的診斷日誌來幫助排除問題

### **新的查詢邏輯：**
```sql
SELECT c.Id, c.Title, c.Discount_Type, ... 
FROM Member_Coupons mc
INNER JOIN Coupons c ON mc.Coupon_Id = c.Id
WHERE mc.Member_Id = @userId
  AND mc.Status != 'used' 
  AND mc.Used_At IS NULL
  AND c.Is_Active = 1
  AND c.Start_At <= @now
  AND c.Expired_At >= @now
```

---

## **✅ 成功標準：**
- `GET /api/Coupons/UserAvailable/1` 回傳優惠券資料（不是空陣列）
- `POST /api/Carts/user/1/coupon` 成功套用優惠券
- 購物車總額正確計算折扣

**現在請執行上述步驟，應該可以成功解決問題！** 🚀