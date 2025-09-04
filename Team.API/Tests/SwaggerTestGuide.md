# 🚀 Swagger 購物車和優惠券 API 測試指南

## 📋 **測試前準備**

### 1. 執行測試資料 SQL
在 SQL Server Management Studio 中執行：
```sql
-- 執行 Team.API\Tests\SwaggerTestData.sql
```

### 2. 啟動 API 專案
- 設定 `Team.API` 為啟動專案
- 按 F5 啟動
- 瀏覽器會開啟：`https://localhost:7106/swagger/index.html`

---

## 🛒 **完整購物車測試流程**

### **Step 1: 取得空購物車**
- **API**: `GET /api/Carts/user/{userId}`
- **參數**: `userId = 1`
- **預期結果**: 空購物車，Total = 60 (運費)

### **Step 2: 加入商品到購物車**
- **API**: `POST /api/Carts/user/{userId}/items`
- **參數**: `userId = 1`
- **Request Body**:
```json
{
  "productId": 1,
  "attributeValueId": 1,
  "quantity": 2
}
```
- **預期結果**: 商品成功加入，Subtotal = 1000, Total = 1000 (滿千免運)

### **Step 3: 取得可用優惠券清單**
- **API**: `GET /api/Coupons/UserAvailable/{userId}`
- **參數**: `userId = 1`
- **預期結果**: 回傳 2 張優惠券 (ID: 1, 2)

### **Step 4: 套用優惠券**
- **API**: `POST /api/Carts/user/{userId}/coupon`
- **參數**: `userId = 1`
- **Request Body**:
```json
{
  "couponCode": "1"
}
```
- **預期結果**: 10% 折扣，Discount = 100, Total = 900

### **Step 5: 驗證購物車**
- **API**: `POST /api/Carts/user/{userId}/validate`
- **參數**: `userId = 1`
- **預期結果**: IsValid = true

### **Step 6: 移除優惠券**
- **API**: `DELETE /api/Carts/user/{userId}/coupon`
- **參數**: `userId = 1`
- **預期結果**: 折扣移除，Total = 1000

### **Step 7: 清空購物車**
- **API**: `DELETE /api/Carts/user/{userId}`
- **參數**: `userId = 1`
- **預期結果**: 購物車清空，Total = 60

---

## 🎫 **優惠券功能測試**

### **測試不同優惠券類型**

#### **10% 折扣券 (ID: 1)**
```json
{
  "couponCode": "1"
}
```
- 條件：滿 $100
- 效果：10% 折扣

#### **滿減券 (ID: 2)**
```json
{
  "couponCode": "2"
}
```
- 條件：滿 $300
- 效果：減 $50

---

## ❌ **錯誤測試案例**

### **1. 無效優惠券 ID**
```json
{
  "couponCode": "999"
}
```
**預期**: 400 錯誤，"找不到指定的優惠券"

### **2. 金額不符合條件**
- 購物車總額 < $100，使用折扣券
- 購物車總額 < $300，使用滿減券
**預期**: 400 錯誤，"最低消費金額需達..."

### **3. JSON 格式錯誤**
```json
{
  "couponCode": "1"   // ❌ 缺少結尾括號
```
**預期**: 400 錯誤，JSON 解析錯誤

---

## 🔧 **常見問題解決**

### **Q: "找不到指定的優惠券"**
**A**: 確保執行了測試資料 SQL，並且 MemberCoupons 表中有關聯資料

### **Q: "您沒有此優惠券，無法使用"**
**A**: 檢查 MemberCoupons 表，確保用戶擁有該優惠券

### **Q: JSON 解析錯誤**
**A**: 檢查 JSON 格式，確保所有括號都正確配對

### **Q: "商品不存在或已下架"**
**A**: 確保 Products 表中有測試商品，且 IsActive = 1

---

## 📊 **測試資料概覽**

| 項目 | ID | 說明 |
|------|----|----|
| 用戶 | 1 | test@example.com |
| 商品 | 1 | 測試商品，價格 $500 |
| 屬性值 | 1 | 標準款 |
| 優惠券1 | 1 | 10%折扣，滿$100 |
| 優惠券2 | 2 | 滿減$50，滿$300 |

---

## ✅ **成功標準**

- [ ] 空購物車載入成功
- [ ] 商品成功加入購物車
- [ ] 可取得用戶可用優惠券
- [ ] 優惠券成功套用且金額正確
- [ ] 優惠券成功移除
- [ ] 購物車驗證通過
- [ ] 購物車成功清空

---

## 🎯 **進階測試**

1. **更新商品數量**
2. **移除特定商品**
3. **測試庫存檢查**
4. **測試不同用戶隔離**
5. **批量操作測試**

完成基本測試後，可以嘗試這些進階功能！