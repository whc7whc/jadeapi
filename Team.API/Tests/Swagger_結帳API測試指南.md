# 🎯 **Swagger 結帳 API 測試指南**

## 📋 **測試前準備**

### 1. 啟動 API 服務
```bash
# 確保 API 專案正在運行
cd Team.API
dotnet run
```

### 2. 開啟 Swagger UI
```
瀏覽器訪問：https://localhost:7000/swagger
(端口可能不同，請查看控制台輸出)
```

### 3. 準備測試資料
在開始測試前，確保資料庫中有：
- ✅ 會員資料（Member ID: 1）
- ✅ 商品資料（Product ID）
- ✅ 購物車資料（Cart 和 CartItems）
- ✅ 優惠券資料（Coupon 和 MemberCoupon）

---

## 🚀 **完整測試流程**

### **階段一：結帳前準備** 🛒

#### **步驟 1.1：驗證購物車狀態**
```
端點：POST /api/Checkout/validate/{memberId}
會員ID：1
```
**測試目的：** 確認購物車可以結帳
**預期結果：** `isValid: true` 且包含結帳摘要

#### **步驟 1.2：取得結帳摘要**
```
端點：GET /api/Checkout/summary/{memberId}
會員ID：1
參數：
  - couponCode: (可選)
  - usedPoints: 0
```
**測試目的：** 查看詳細金額計算
**預期結果：** 包含商品明細、運費、總金額等

#### **步驟 1.3：取得配送方式**
```
端點：GET /api/Checkout/delivery-methods/{memberId}
會員ID：1
```
**測試目的：** 查看可用配送選項
**預期結果：** 返回標準配送、快速配送、門市取貨等選項

#### **步驟 1.4：取得付款方式**
```
端點：GET /api/Checkout/payment-methods/{memberId}
會員ID：1
```
**測試目的：** 查看可用付款選項
**預期結果：** 返回信用卡、ATM、Line Pay、貨到付款等選項

#### **步驟 1.5：計算運費**
```
端點：GET /api/Checkout/shipping-fee/{memberId}
會員ID：1
參數：deliveryMethod=standard
```
**測試目的：** 確認運費計算正確
**預期結果：** 標準配送 60 元

---

### **階段二：優惠券與點數** 🎫

#### **步驟 2.1：驗證優惠券**
```
端點：POST /api/Checkout/validate-coupon/{memberId}
會員ID：1
Body (raw JSON)：
"您的優惠券代碼"
```
**測試目的：** 確認優惠券可用性
**預期結果：** 返回優惠券資訊和折扣金額

#### **步驟 2.2：取得可用點數**
```
端點：GET /api/Checkout/available-points/{memberId}
會員ID：1
```
**測試目的：** 查看會員點數餘額
**預期結果：** 返回會員目前點數

#### **步驟 2.3：計算最大點數抵扣**
```
端點：GET /api/Checkout/max-points-deduction/{memberId}
會員ID：1
參數：subtotal=1000
```
**測試目的：** 確認點數抵扣限制
**預期結果：** 最多可抵扣 30% (300元 或 可用點數較小值)

---

### **階段三：建立訂單** 📦

#### **步驟 3.1：完整結帳**
```
端點：POST /api/Checkout/create-order
Body (raw JSON)：
{
  "memberId": 1,
  "recipientName": "測試用戶",
  "phoneNumber": "0912345678",
  "city": "台北市",
  "district": "中正區",
  "addressDetail": "測試地址123號",
  "deliveryMethod": "standard",
  "paymentMethod": "credit_card",
  "couponCode": "",
  "usedPoints": 0,
  "note": "測試訂單"
}
```
**測試目的：** 建立完整訂單
**預期結果：** 返回訂單ID和訂單編號

#### **步驟 3.2：快速結帳（可選）**
```
端點：POST /api/Checkout/quick-checkout
Body (raw JSON)：
{
  "memberId": 1,
  "productId": 1,
  "attributeValueId": 1,
  "quantity": 1,
  "deliveryInfo": {
    "memberId": 1,
    "recipientName": "測試用戶",
    "phoneNumber": "0912345678",
    "city": "台北市",
    "district": "中正區",
    "addressDetail": "測試地址123號",
    "deliveryMethod": "express",
    "paymentMethod": "linepay"
  }
}
```
**測試目的：** 測試立即購買功能

---

### **階段四：訂單確認** ✅

#### **步驟 4.1：取得訂單確認**
```
端點：GET /api/Checkout/order-confirmation
參數：
  - orderId: (步驟3.1得到的訂單ID)
  - memberId: 1
```
**測試目的：** 查看訂單詳細資訊
**預期結果：** 完整的訂單明細、金額計算、商品清單

---

### **階段五：付款處理** 💳

#### **步驟 5.1：處理付款**
```
端點：POST /api/Checkout/process-payment/{orderId}
路徑參數：orderId (步驟3.1得到的訂單ID)
Body (raw JSON)：
{
  "method": "credit_card",
  "cardNumber": "4111111111111111",
  "expiryMonth": "12",
  "expiryYear": "2025",
  "cvv": "123",
  "cardholderName": "測試用戶"
}
```
**測試目的：** 模擬付款處理
**預期結果：** 返回付款資訊和交易ID

#### **步驟 5.2：確認付款**
```
端點：POST /api/Checkout/confirm-payment
參數：
  - orderId: (步驟3.1得到的訂單ID)
  - transactionId: (步驟5.1得到的交易ID)
```
**測試目的：** 確認付款完成
**預期結果：** 訂單狀態更新為已確認

---

### **階段六：其他功能** 🛠️

#### **步驟 6.1：取得預計配送日期**
```
端點：GET /api/Checkout/estimated-delivery
參數：deliveryMethod=standard
```
**測試目的：** 查看配送時間估算

---

## 🔄 **實際結帳流程對應**

### **🏪 真實電商結帳流程**

```
客戶購物 → 加入購物車 → 結帳頁面 → 填寫資訊 → 付款 → 訂單完成
    ↓           ↓           ↓          ↓        ↓        ↓
   瀏覽商品     CartAPI    CheckoutAPI  表單驗證   金流API   訂單確認
```

### **📋 詳細對應關係**

| **實際流程步驟** | **對應 API** | **用戶體驗** |
|-----------------|-------------|-------------|
| **1. 進入結帳頁** | `validate/{memberId}` | 檢查購物車是否可結帳 |
| **2. 選擇配送方式** | `delivery-methods/{memberId}` | 顯示配送選項給用戶選擇 |
| **3. 選擇付款方式** | `payment-methods/{memberId}` | 顯示付款選項給用戶選擇 |
| **4. 輸入優惠券** | `validate-coupon/{memberId}` | 即時驗證優惠券並計算折扣 |
| **5. 使用點數** | `available-points/{memberId}` | 顯示可用點數並限制使用量 |
| **6. 即時計算金額** | `summary/{memberId}` | 每次變更都重新計算總金額 |
| **7. 確認訂單** | `create-order` | 用戶點擊「確認訂單」按鈕 |
| **8. 跳轉付款** | `process-payment/{orderId}` | 導向第三方金流頁面 |
| **9. 付款完成回調** | `confirm-payment` | 金流回調確認付款狀態 |
| **10. 顯示訂單確認** | `order-confirmation` | 顯示訂單成功頁面 |

### **🎭 用戶使用情境**

#### **情境一：一般購物流程**
```
1. 用戶瀏覽商品 → 加入購物車
2. 點擊「結帳」→ 呼叫 validate API
3. 填寫收件資訊 → 選擇配送方式
4. 輸入優惠券 → 即時驗證並更新金額
5. 選擇使用點數 → 重新計算總金額  
6. 確認訂單 → 呼叫 create-order API
7. 進行付款 → 呼叫 process-payment API
8. 付款完成 → 呼叫 confirm-payment API
9. 查看訂單 → 呼叫 order-confirmation API
```

#### **情境二：立即購買流程**
```
1. 用戶在商品頁點擊「立即購買」
2. 直接跳到結帳頁 → 呼叫 quick-checkout API
3. 系統自動加入購物車並建立訂單
4. 後續付款流程與一般流程相同
```

---

## 🎯 **測試檢查點**

### **✅ 成功測試指標**

1. **資料驗證**
   - [ ] 會員資料正確載入
   - [ ] 購物車商品正確顯示
   - [ ] 庫存數量正確檢查

2. **金額計算**
   - [ ] 商品小計正確
   - [ ] 運費計算正確（滿1000免運）
   - [ ] 優惠券折扣正確
   - [ ] 點數抵扣正確（最多30%）
   - [ ] 總金額計算正確

3. **訂單處理**
   - [ ] 訂單成功建立
   - [ ] 訂單明細正確
   - [ ] 庫存正確扣除
   - [ ] 購物車正確清空
   - [ ] 優惠券狀態更新
   - [ ] 點數正確扣除

4. **付款流程**
   - [ ] 付款處理成功
   - [ ] 訂單狀態更新
   - [ ] 付款狀態更新

### **❌ 錯誤測試指標**

1. **邊界條件測試**
   - [ ] 購物車為空時的處理
   - [ ] 商品庫存不足時的處理
   - [ ] 優惠券過期時的處理
   - [ ] 點數不足時的處理

2. **異常情況測試**
   - [ ] 會員不存在時的處理
   - [ ] 商品已下架時的處理
   - [ ] 網路錯誤時的處理

---

## 📊 **測試結果記錄表**

| **階段** | **API端點** | **狀態** | **回應時間** | **備註** |
|---------|------------|---------|-------------|---------|
| 驗證 | `POST /validate/1` | ✅/❌ | _ms | |
| 摘要 | `GET /summary/1` | ✅/❌ | _ms | |
| 配送 | `GET /delivery-methods/1` | ✅/❌ | _ms | |
| 付款 | `GET /payment-methods/1` | ✅/❌ | _ms | |
| 優惠券 | `POST /validate-coupon/1` | ✅/❌ | _ms | |
| 點數 | `GET /available-points/1` | ✅/❌ | _ms | |
| 建立訂單 | `POST /create-order` | ✅/❌ | _ms | |
| 訂單確認 | `GET /order-confirmation` | ✅/❌ | _ms | |
| 處理付款 | `POST /process-payment/1` | ✅/❌ | _ms | |
| 確認付款 | `POST /confirm-payment` | ✅/❌ | _ms | |

---

## 🔧 **常見問題排除**

### **Q1: API 回應 404 錯誤**
**解決方法：** 確認 API 專案正在運行，檢查 URL 路徑是否正確

### **Q2: 會員ID找不到**
**解決方法：** 確認資料庫中存在對應的會員資料

### **Q3: 購物車為空**
**解決方法：** 先使用 Cart API 加入商品到購物車

### **Q4: 優惠券驗證失敗**
**解決方法：** 確認會員擁有該優惠券且未過期

### **Q5: 點數不足**
**解決方法：** 檢查會員點數餘額，調整使用數量

---

## 🎉 **測試完成後**

恭喜！您已完成結帳 API 的完整測試。這個 API 涵蓋了電商系統中最核心的結帳流程，包括：

- ✅ **購物車驗證** - 確保結帳前狀態正確
- ✅ **金額計算** - 精確的價格計算邏輯  
- ✅ **優惠券系統** - 完整的折扣處理
- ✅ **點數抵扣** - 靈活的點數使用
- ✅ **訂單管理** - 完整的訂單生命週期
- ✅ **付款整合** - 預留第三方金流整合
- ✅ **庫存管理** - 自動庫存扣除
- ✅ **通知系統** - 訂單確認通知

接下來您可以：
1. **整合前端頁面** - 建立 Razor Pages 結帳界面
2. **整合真實金流** - 串接綠界、藍新等付款服務
3. **優化使用者體驗** - 加入載入動畫、即時驗證等
4. **加強安全性** - 加入更多驗證和加密機制

🚀 **您的結帳系統已經準備好為用戶提供流暢的購物體驗了！**